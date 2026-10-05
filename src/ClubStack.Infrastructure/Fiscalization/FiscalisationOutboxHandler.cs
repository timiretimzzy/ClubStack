using System.Text.Json;
using ClubStack.Application.Fiscalization;
using ClubStack.Application.Outbox;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Outbox;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Drives a committed sale or refund to a fiscal document.
/// </summary>
public sealed class FiscalisationProcessor
{
    private const string FiscalisedState = nameof(FiscalisationState.Fiscalised);
    private const string PendingRetryState = nameof(FiscalisationState.PendingRetry);
    private const string FailedState = nameof(FiscalisationState.Failed);
    private const string SubmittedState = nameof(FiscalisationState.Submitted);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalisationProcessor> _logger;

    public FiscalisationProcessor(IServiceScopeFactory scopeFactory, ILogger<FiscalisationProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Invoked by the outbox worker, never inline with the sale. The rules it enforces:
    /// <list type="bullet">
    ///   <item>A document the engine already accepted is never re-sent. A record carrying
    ///   device identifiers is flushed through the engine's own offline queue instead of
    ///   being transacted again.</item>
    ///   <item>A sale is only marked <c>Fiscalised</c> when the engine reports success.
    ///   An offline acceptance is recorded as <c>PendingRetry</c> with its identifiers kept,
    ///   because a QR issued offline has not been accepted by FDMS.</item>
    ///   <item>No fiscal day is opened implicitly. A closed day defers the attempt rather
    ///   than silently starting a taxpayer day.</item>
    /// </list>
    /// </summary>
    public async Task ProcessAsync(FiscalisationRequiredEvent payload, Guid messageId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();

        // Tracked: this record is mutated below (status, attempts, device receipt) and
        // saved. Loading it AsNoTracking silently discarded every write.
        var record = await context.Fiscalisations
            .FirstOrDefaultAsync(f => f.Id == payload.FiscalisationId, cancellationToken);

        if (record is null)
        {
            _logger.LogError(
                "Fiscalisation record {FiscalisationId} referenced by outbox message {MessageId} no longer exists",
                payload.FiscalisationId,
                messageId);
            return;
        }

        // Already fiscalised: the work this message asked for is done. Returning marks
        // the message processed and prevents any duplicate document.
        if (string.Equals(record.Status, FiscalisedState, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Fiscalisation {FiscalisationId} is already {Status}; outbox message {MessageId} is a no-op",
                record.Id,
                record.Status,
                messageId);
            return;
        }

        var gateway = scope.ServiceProvider.GetRequiredService<IFiscalisationGateway>();

        // The engine already holds this document: flush its own queue rather than
        // transacting the same invoice number a second time.
        if (!string.IsNullOrWhiteSpace(record.ReceiptGlobalNo) || record.PendingSync)
        {
            _logger.LogInformation(
                "Fiscalisation {FiscalisationId} was accepted offline as receipt {ReceiptGlobalNo}; flushing the engine queue instead of re-transacting",
                record.Id,
                record.ReceiptGlobalNo);

            await FlushQueueAsync(context, record, gateway, cancellationToken);
            return;
        }

        var sale = await context.Sales
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == record.SaleId, cancellationToken);

        if (sale is null)
        {
            await FailAsync(context, record, "The sale for this fiscalisation could not be loaded.", cancellationToken);
            return;
        }

        var refund = record.RefundId is { } refundId
            ? await context.SaleRefunds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken)
            : null;

        var status = await gateway.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.FiscalDayState != FiscalDayState.Opened)
        {
            // Do not open a taxpayer day implicitly. The attempt is deferred so the
            // document is still owed once an authorised operator opens the day.
            await DeferAsync(
                context,
                record,
                $"The fiscal device day is {status.FiscalDayStatus ?? "not open"}; the document was not submitted.",
                cancellationToken);
            return;
        }

        var configuration = await gateway.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);

        FiscalDocument document;
        try
        {
            document = FiscalDocumentBuilder.Build(sale, refund, configuration);
        }
        catch (FiscalDocumentBuildException ex)
        {
            // The stored sale/refund cannot produce an honest document. This will not fix
            // itself on retry, so it is a permanent failure an operator must resolve —
            // not a deferral, and not an exception allowed to escape the processor.
            await FailAsync(context, record, ex.Message, cancellationToken);
            return;
        }

        record.AttemptCount += 1;
        record.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        record.SubmittedAtUtc = record.SubmittedAtUtc ?? record.LastAttemptAtUtc;
        record.DeviceId = status.DeviceId;
        record.DeviceSerialNumber = status.SerialNumber;
        record.Status = SubmittedState;

        FiscalResult result;
        try
        {
            result = await gateway.FiscaliseAsync(document, cancellationToken).ConfigureAwait(false);
        }
        catch (FiscalisationException ex)
        {
            await DeferAsync(context, record, ex.Message, cancellationToken);
            return;
        }

        ApplyResult(context, record, result);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Fiscalisation {FiscalisationId} for {InvoiceNumber} reached {Status} (receipt {ReceiptGlobalNo}, pending sync {PendingSync})",
            record.Id,
            record.InvoiceNumber,
            record.Status,
            record.ReceiptGlobalNo,
            record.PendingSync);
    }

    private static void ApplyResult(ClubStackDbContext context, FiscalisationEntity record, FiscalResult result)
    {
        record.FiscalDayNo = result.FiscalDayNo ?? record.FiscalDayNo;
        record.ReceiptGlobalNo = result.ReceiptGlobalNo ?? record.ReceiptGlobalNo;
        record.ReceiptCounter = result.ReceiptCounter ?? record.ReceiptCounter;
        record.ReceiptId = result.ReceiptId ?? record.ReceiptId;
        record.OperationId = result.OperationId ?? record.OperationId;
        record.DeviceSignature = result.DeviceSignature ?? record.DeviceSignature;
        record.ServerSignature = result.ServerSignature ?? record.ServerSignature;
        record.QrUrl = result.QrUrl ?? record.QrUrl;
        record.DeviceId = result.DeviceId ?? record.DeviceId;
        record.DeviceSerialNumber = result.DeviceSerialNumber ?? record.DeviceSerialNumber;
        record.PaymentMapping = result.PaymentMapping ?? record.PaymentMapping;
        record.Code = result.Code ?? record.Code;
        record.Message = result.Message ?? record.Message;
        record.PendingSync = result.PendingSync;
        record.ValidationWarningsJson = System.Text.Json.JsonSerializer.Serialize(
            result.ValidationWarnings,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (!result.Success)
        {
            record.Status = FailedState;
            return;
        }

        // An offline acceptance has a receipt and a QR but no FDMS acknowledgement, so
        // it is not treated as fiscalised. Identifiers are kept so the engine's own
        // queue can be flushed later without re-transacting.
        record.Status = result.PendingSync ? PendingRetryState : FiscalisedState;
        if (!result.PendingSync)
        {
            record.FiscalisedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static async Task FlushQueueAsync(
        ClubStackDbContext context,
        FiscalisationEntity record,
        IFiscalisationGateway gateway,
        CancellationToken cancellationToken)
    {
        try
        {
            var stillPending = await gateway.SubmitPendingAsync(cancellationToken).ConfigureAwait(false);
            record.Code = "engine-queue-flushed";
            record.Message = $"Engine offline queue flushed; documents still pending: {stillPending}.";
            record.PendingSync = !string.Equals(stillPending, "0", StringComparison.Ordinal);
            record.Status = record.PendingSync ? PendingRetryState : FiscalisedState;
            if (!record.PendingSync)
            {
                record.FiscalisedAtUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (FiscalisationException ex)
        {
            record.Status = PendingRetryState;
            record.Code = "engine-queue-failed";
            record.Message = ex.Message;
        }

        record.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeferAsync(
        ClubStackDbContext context,
        FiscalisationEntity record,
        string reason,
        CancellationToken cancellationToken)
    {
        record.Status = PendingRetryState;
        record.Code = "deferred";
        record.Message = reason;
        record.ErrorInfo = reason.Length > 2000 ? reason[..2000] : reason;
        record.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task FailAsync(
        ClubStackDbContext context,
        FiscalisationEntity record,
        string reason,
        CancellationToken cancellationToken)
    {
        record.Status = FailedState;
        record.Code = "sale-missing";
        record.Message = reason;
        record.ErrorInfo = reason;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Adapts <see cref="FiscalisationProcessor"/> to the outbox for fiscal invoices.
/// The dispatcher matches event types exactly, so invoices and credit notes need one
/// handler each even though the processing logic is identical.
/// </summary>
public sealed class FiscalisationOutboxHandler : IOutboxMessageHandler
{
    private readonly FiscalisationProcessor _processor;
    private readonly ILogger<FiscalisationOutboxHandler> _logger;

    public FiscalisationOutboxHandler(FiscalisationProcessor processor, ILogger<FiscalisationOutboxHandler> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string HandledEventType => FiscalizationEventTypes.FiscalisationRequired;

    public Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
        => FiscalisationHandlerSupport.DispatchAsync(_processor, _logger, message, cancellationToken);
}

/// <summary>Outbox handler for fiscal credit notes raised by refunds.</summary>
public sealed class FiscalisationCreditNoteHandler : IOutboxMessageHandler
{
    private readonly FiscalisationProcessor _processor;
    private readonly ILogger<FiscalisationCreditNoteHandler> _logger;

    public FiscalisationCreditNoteHandler(FiscalisationProcessor processor, ILogger<FiscalisationCreditNoteHandler> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string HandledEventType => FiscalizationEventTypes.CreditNoteRequired;

    public Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
        => FiscalisationHandlerSupport.DispatchAsync(_processor, _logger, message, cancellationToken);
}

internal static class FiscalisationHandlerSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// An unreadable payload is logged and swallowed rather than thrown: a malformed
    /// message must not be retried ten times against a device that can never satisfy it.
    /// </summary>
    public static async Task DispatchAsync(
        FiscalisationProcessor processor,
        ILogger logger,
        OutboxMessageData message,
        CancellationToken cancellationToken)
    {
        FiscalisationRequiredEvent? payload;
        try
        {
            payload = JsonSerializer.Deserialize<FiscalisationRequiredEvent>(message.PayloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Fiscalisation outbox message {MessageId} has an unreadable payload", message.Id);
            return;
        }

        if (payload is null)
        {
            logger.LogError("Fiscalisation outbox message {MessageId} produced no payload", message.Id);
            return;
        }

        await processor.ProcessAsync(payload, message.Id, cancellationToken).ConfigureAwait(false);
    }
}
