using System.Text.Json;
using ClubStack.Application.Fiscalization;
using ClubStack.Application.Outbox;
using ClubStack.Application.Pos;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using ClubStack.Infrastructure.Pos;
using Microsoft.Extensions.Logging;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Writes the durable statement that a committed sale or refund owes a fiscal document,
/// together with the outbox message that will drive it.
///
/// Called from inside the sale/refund transaction. It never calls the fiscal device:
/// the device is contacted only later, by the outbox handler, so a device outage cannot
/// roll back or delay a completed sale.
/// </summary>
public interface IFiscalisationRequirementWriter
{
    /// <summary>
    /// Records a fiscalisation requirement for <paramref name="sale"/> (or for
    /// <paramref name="refund"/> when supplied) and enqueues the matching outbox event.
    /// Returns the new record's identifier.
    /// </summary>
    Task<Guid> RequireAsync(
        SaleEntity sale,
        SaleRefundEntity? refund,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalisationRequirementWriter : IFiscalisationRequirementWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ClubStackDbContext _context;
    private readonly IOutboxStore _outbox;
    private readonly ILogger<FiscalisationRequirementWriter> _logger;

    public FiscalisationRequirementWriter(
        ClubStackDbContext context,
        IOutboxStore outbox,
        ILogger<FiscalisationRequirementWriter> logger)
    {
        _context = context;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Guid> RequireAsync(
        SaleEntity sale,
        SaleRefundEntity? refund,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);

        var isCreditNote = refund is not null;
        var entity = new FiscalisationEntity
        {
            SaleId = sale.Id,
            SaleNumber = sale.SaleNumber,
            RefundId = refund?.Id,
            RefundNumber = refund?.RefundNumber,
            InvoiceNumber = isCreditNote ? refund!.RefundNumber : sale.SaleNumber,
            DocumentKind = isCreditNote
                ? nameof(Hardware.Fiscalization.FiscalDocumentKind.CreditNote)
                : nameof(Hardware.Fiscalization.FiscalDocumentKind.FiscalInvoice),
            OriginalInvoiceNumber = isCreditNote ? sale.SaleNumber : null,
            Status = nameof(Hardware.Fiscalization.FiscalisationState.Pending),
            TendersJson = SnapshotTenders(sale, refund),
            ValidationWarningsJson = "[]",
            CorrelationId = correlationId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        _context.Fiscalisations.Add(entity);
        await _outbox.EnqueueAsync(
            isCreditNote
                ? FiscalizationEventTypes.CreditNoteRequired
                : FiscalizationEventTypes.FiscalisationRequired,
            PosJson.Serialize(new FiscalisationRequiredEvent(
                entity.Id,
                sale.Id,
                refund?.Id,
                correlationId)),
            correlationId,
            cancellationToken);

        _logger.LogInformation(
            "Fiscalisation required for sale {SaleNumber}{RefundSuffix} (record {FiscalisationId}, status {Status}).",
            sale.SaleNumber,
            isCreditNote ? $" refund {refund!.RefundNumber}" : string.Empty,
            entity.Id,
            entity.Status);

        return entity.Id;
    }

    /// <summary>
    /// Preserves the exact ClubStack tender rows. The engine's own representation is a
    /// single aggregate currency line, so without this snapshot the audit trail could
    /// not show what the customer actually paid with (PM DECISION 2).
    /// </summary>
    private static string SnapshotTenders(SaleEntity sale, SaleRefundEntity? refund)
    {
        IEnumerable<object> tenders;

        if (refund is not null)
        {
            var refundPayments = JsonSerializer.Deserialize<IReadOnlyList<SaleRefundPaymentDto>>(
                refund.PaymentsJson,
                JsonOptions) ?? new List<SaleRefundPaymentDto>();

            tenders = refundPayments.Select(t => (object)new
            {
                t.PaymentMethodCode,
                t.PaymentMethodName,
                t.Kind,
                t.Amount,
                t.ExternalReference,
            });
        }
        else
        {
            tenders = sale.Payments
                .Where(p => !p.IsRefund)
                .Select(p => (object)new
                {
                    PaymentMethodCode = p.PaymentMethodCode,
                    PaymentMethodName = p.PaymentMethodName,
                    p.Kind,
                    p.Amount,
                    p.ExternalReference,
                });
        }

        return JsonSerializer.Serialize(tenders, JsonOptions);
    }
}
