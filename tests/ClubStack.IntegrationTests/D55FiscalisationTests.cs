using System.Text.Json;
using ClubStack.Application.Fiscalization;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Inventory;
using ClubStack.Application.Outbox;
using ClubStack.Application.Platform;
using ClubStack.Application.Pos;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Outbox;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Scriptable stand-in for the FiscalStackZW device. Every behaviour the adapter can
/// produce is expressed here so the D5.5 state machine, idempotency and outbox
/// integration can be verified deterministically without a physical device.
///
/// The real engine is exercised separately against the corrected DLL; see
/// <see cref="FiscalStackDeviceLoadTests"/>.
/// </summary>
internal sealed class FakeFiscalisationGateway : IFiscalisationGateway
{
    public FiscalDayState DayState { get; set; } = FiscalDayState.Opened;

    public bool PendingSync { get; set; }

    public bool ThrowOnFiscalise { get; set; }

    /// <summary>
    /// When false the engine returns a completed-but-rejected result instead of throwing.
    /// A rejection is a permanent failure, whereas a thrown device error is a deferral, so
    /// the two drive different states and both need covering.
    /// </summary>
    public bool AcceptResult { get; set; } = true;

    public string RejectionMessage { get; set; } = "The device rejected the document.";

    public string? ThrowMessage { get; set; }

    public int PendingQueueCount { get; set; }

    public List<FiscalDocument> Received { get; } = new();

    public int FiscaliseCallCount { get; private set; }

    public int SubmitPendingCallCount { get; private set; }

    public List<string> LicensesApplied { get; } = new();

    /// <summary>Receipt global number the device returned for its most recent document.</summary>
    public string? LastReceiptGlobalNo { get; private set; }

    /// <summary>
    /// Receipt global numbers are keyed by invoice so assertions stay deterministic even
    /// though the background job drains the whole shared outbox queue.
    /// </summary>
    public string ReceiptGlobalNoFor(string invoiceNumber)
        => _receiptGlobalNoByInvoice.TryGetValue(invoiceNumber, out var value)
            ? value
            : throw new KeyNotFoundException($"The device never issued a receipt for invoice '{invoiceNumber}'.");

    private int _globalNo;

    private readonly Dictionary<string, string> _receiptGlobalNoByInvoice = new(StringComparer.OrdinalIgnoreCase);

    public Task<FiscalDeviceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDeviceStatus
        {
            Available = true,
            DeviceId = "18084",
            SerialNumber = "FSTK-00000001",
            CompanyName = "Test Club",
            LicenseActive = LicenseAccepted,
            LicenseStatus = LicenseAccepted ? "License Status: Active" : "License Status: Not Installed",
            LicenseStart = DateTimeOffset.UtcNow.AddDays(-30),
            LicenseExpiry = DateTimeOffset.UtcNow.AddDays(335),
            CertificateCount = 1,
            FiscalDayState = DayState,
            FiscalDayStatus = DayState == FiscalDayState.Opened ? "FiscalDayOpen" : "FiscalDayClosed",
            LastFiscalDayNo = "1",
            LastReceiptGlobalNo = _globalNo.ToString(),
            ZimraReachable = !PendingSync,
            ZimraSource = PendingSync ? "ZIMRA offline - local snapshot only" : "ZIMRA",
        });

    public Task<FiscalDeviceConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDeviceConfiguration
        {
            DeviceId = "18084",
            SerialNumber = "FSTK-00000001",
            CompanyName = "Test Club",
            BranchName = "Head Office",
            City = "Harare",
            Environment = "ONLINE",
            StandardRatedTaxId = 515,
            ZeroRatedTaxId = 1,
            ExemptTaxId = 2,
            CurrencyCode = "ZWG",
            ServerUrl = "https://fdms.zimra.co.zw",
        });

    public Task<bool> SetLicenseAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        LicensesApplied.Add(licenseKey);
        LicenseAccepted = true;
        return Task.FromResult(true);
    }

    public bool LicenseAccepted { get; set; } = true;

    public List<FiscalDeviceConfiguration> ConfigurationsApplied { get; } = new();

    public Task UpdateConfigurationAsync(FiscalDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ConfigurationsApplied.Add(configuration);
        return Task.CompletedTask;
    }

    public List<FiscalEnvironment> EnvironmentSelections { get; } = new();

    public FiscalEnvironment SelectedEnvironment { get; private set; } = FiscalEnvironment.Production;

    public string EffectiveZimraServer { get; private set; } = "https://fdmsapi.zimra.co.zw";

    public string EffectiveVerificationServer { get; private set; } = "https://fdms.zimra.co.zw";

    /// <summary>When set, the fake refuses a mismatched environment exactly as the real adapter does.</summary>
    public bool EnforceEnvironmentSafety { get; set; } = true;

    public int RecycleCount { get; private set; }

    public bool RegistrationSucceeds { get; set; } = true;

    public string? RegistrationFailureMessage { get; set; }

    public bool CertificatesGenerated { get; private set; }

    public string ProvisionedSerial { get; private set; } = "FSTK-00000001";

    public string ProvisionedDeviceId { get; private set; } = "18084";

    public Task<FiscalEnvironmentSelection> SelectEnvironmentAsync(
        FiscalEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        EnvironmentSelections.Add(environment);
        SelectedEnvironment = environment;
        var test = environment == FiscalEnvironment.Test;
        EffectiveZimraServer = test ? "https://fdmstest.zimra.co.zw" : "https://fdmsapi.zimra.co.zw";
        EffectiveVerificationServer = test ? "https://fdmstest.zimra.co.zw" : "https://fdms.zimra.co.zw";
        return Task.FromResult(new FiscalEnvironmentSelection
        {
            Environment = environment,
            ZimraServerUrl = EffectiveZimraServer,
            VerificationServerUrl = EffectiveVerificationServer,
            IsTestHost = test,
            EnvironmentTransitionPending = true,
        });
    }

    public Task<FiscalDeviceStatus> RecycleDeviceAsync(CancellationToken cancellationToken = default)
    {
        RecycleCount++;
        return GetStatusAsync(cancellationToken);
    }

    public Task<FiscalDeviceCertificates> GenerateDeviceCertificatesAsync(
        string serialNumber,
        string activationKey,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!RegistrationSucceeds)
        {
            throw new FiscalisationException(RegistrationFailureMessage ?? "Certificate generation was refused.");
        }

        CertificatesGenerated = true;
        return Task.FromResult(new FiscalDeviceCertificates
        {
            Generated = true,
            CertificateFolder = $@"C:\FiscalStackZW certificates\{deviceId}",
            SerialNumber = serialNumber,
            DeviceId = deviceId,
            CertificateCount = 1,
        });
    }

    public Task<FiscalDeviceRegistration> RegisterDeviceAsync(
        string activationKey,
        string serialNumber,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!RegistrationSucceeds)
        {
            throw new FiscalisationException(RegistrationFailureMessage ?? "Registration was refused.");
        }

        ProvisionedSerial = serialNumber;
        ProvisionedDeviceId = deviceId;
        CertificatesGenerated = true;
        return Task.FromResult(new FiscalDeviceRegistration
        {
            Success = true,
            Message = "Device registered with ZIMRA",
            OperationId = "op-register",
            TaxpayerName = "Test Club",
            SerialNumber = serialNumber,
            DeviceId = deviceId,
            CertificateFolder = $@"C:\FiscalStackZW certificates\{deviceId}",
        });
    }

    public Task<FiscalDeviceCertificates> GetCertificateDetailsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDeviceCertificates
        {
            Generated = CertificatesGenerated,
            CertificateFolder = $@"C:\FiscalStackZW certificates\{ProvisionedDeviceId}",
            SerialNumber = ProvisionedSerial,
            DeviceId = ProvisionedDeviceId,
            CertificateCount = CertificatesGenerated ? 1 : 0,
        });

    public Task EnsureEnvironmentSafetyAsync(CancellationToken cancellationToken = default)
    {
        if (EnforceEnvironmentSafety)
        {
            var test = SelectedEnvironment == FiscalEnvironment.Test;
            var hostIsTest = EffectiveZimraServer.Contains("fdmstest", StringComparison.OrdinalIgnoreCase);
            if (test != hostIsTest)
            {
                throw new FiscalisationException(
                    "Refusing to contact FDMS: the configured host does not match the selected environment.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<FiscalResult> OpenFiscalDayAsync(CancellationToken cancellationToken = default)
    {
        DayState = FiscalDayState.Opened;
        return Task.FromResult(new FiscalResult { Success = true, FiscalDayNo = "1", OperationId = "op-open" });
    }

    public Task<FiscalResult> CloseFiscalDayAsync(CancellationToken cancellationToken = default)
    {
        DayState = FiscalDayState.Closed;
        return Task.FromResult(new FiscalResult { Success = true, FiscalDayNo = "1", OperationId = "op-close" });
    }

    public Task<FiscalResult> FiscaliseAsync(FiscalDocument document, CancellationToken cancellationToken = default)
    {
        FiscaliseCallCount++;
        Received.Add(document);

        if (ThrowOnFiscalise)
        {
            throw new FiscalisationException(ThrowMessage ?? "device rejected the document");
        }

        _globalNo++;

        var receiptGlobalNo = _globalNo.ToString();
        _receiptGlobalNoByInvoice[document.InvoiceNumber] = receiptGlobalNo;
        LastReceiptGlobalNo = receiptGlobalNo;

        return Task.FromResult(new FiscalResult
        {
            Success = AcceptResult,
            Message = AcceptResult ? "Success" : RejectionMessage,
            DeviceId = "18084",
            DeviceSerialNumber = "FSTK-00000001",
            FiscalDayNo = "1",
            ReceiptGlobalNo = receiptGlobalNo,
            ReceiptCounter = "0000000001",
            ReceiptId = $"RCP-{receiptGlobalNo}",
            OperationId = $"op-{receiptGlobalNo}",
            DeviceSignature = "devsig",
            ServerSignature = PendingSync ? null : "serversig",
            QrUrl = $"https://fdms.zimra.co.zw/000001808425092026000000000{_globalNo:D3}",
            PendingSync = PendingSync,
            ValidationWarnings = [],
            PaymentMapping = "aggregate-single-currency",
        });
    }

    public Task<string> SubmitPendingAsync(CancellationToken cancellationToken = default)
    {
        SubmitPendingCallCount++;
        PendingQueueCount = 0;
        return Task.FromResult("0");
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(PendingQueueCount);
}

/// <summary>
/// D5.5 fiscalisation: a completed sale always owes a fiscal document, the outbox worker
/// drives that document through the device exactly once, engine receipts are recorded
/// verbatim, and a sale is never reported as fiscalised without a device success.
/// </summary>
[Collection("Database")]
public sealed class D55FiscalisationTests : DatabaseTestBase
{
    private const string Json = "application/json";

    public D55FiscalisationTests(PostgreSqlTestFixture fixture) : base(fixture)
    {
    }

    // ---- 1. Requirement is recorded with the sale commit ----

    [Fact]
    public async Task Completed_sale_creates_a_pending_fiscalisation_and_outbox_message()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var context = provider.GetRequiredService<ClubStackDbContext>();

        var record = await context.Fiscalisations.SingleAsync(f => f.SaleId == saleId);

        Assert.Equal(nameof(FiscalisationState.Pending), record.Status);
        Assert.Equal(nameof(FiscalDocumentKind.FiscalInvoice), record.DocumentKind);
        Assert.Equal(0, record.AttemptCount);
        Assert.Null(record.ReceiptGlobalNo);
        Assert.Null(record.FiscalisedAtUtc);
        Assert.Equal("aggregate-single-currency", await ReadPaymentMappingAfterRunAsync(provider, record.Id));

        var message = await context.OutboxMessages
            .Where(o => o.EventType == FiscalizationEventTypes.FiscalisationRequired &&
                        o.PayloadJson.Contains(record.Id.ToString()))
            .SingleAsync();
        Assert.Contains(record.Id.ToString(), message.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.FiscaliseCallCount);
    }

    // ---- 2. Successful fiscalisation is recorded with the engine's receipt ----

    [Fact]
    public async Task Worker_fiscalises_a_pending_sale_and_persists_the_device_receipt()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);

        await RunOutboxJobAsync(provider);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);

        Assert.Equal(nameof(FiscalisationState.Fiscalised), record.Status);
        Assert.Equal(gateway.ReceiptGlobalNoFor(record.InvoiceNumber), record.ReceiptGlobalNo);
        Assert.Equal($"RCP-{record.ReceiptGlobalNo}", record.ReceiptId);
        Assert.Equal("devsig", record.DeviceSignature);
        Assert.Equal("serversig", record.ServerSignature);
        Assert.False(record.PendingSync);
        Assert.Equal(1, record.AttemptCount);
        Assert.NotNull(record.FiscalisedAtUtc);
        Assert.Equal(1, gateway.Received.Count(d => d.InvoiceNumber == record.InvoiceNumber));
    }

    // ---- 3. Offline acceptance is NOT reported as fiscalised ----

    [Fact]
    public async Task Offline_acceptance_is_recorded_as_pending_retry_and_keeps_its_identifiers()
    {
        var (provider, gateway) = await BuildProviderAsync(g => g.PendingSync = true);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);

        await RunOutboxJobAsync(provider);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);

        Assert.Equal(nameof(FiscalisationState.PendingRetry), record.Status);
        Assert.True(record.PendingSync);
        Assert.Equal(gateway.ReceiptGlobalNoFor(record.InvoiceNumber), record.ReceiptGlobalNo);
        Assert.Null(record.ServerSignature);
        Assert.Null(record.FiscalisedAtUtc);
    }

    // ---- 4. Idempotency: a second worker pass never re-transacts ----

    [Fact]
    public async Task Worker_is_a_no_op_for_an_already_fiscalised_document()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var invoiceNumber = (await context.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.Id == recordId)).InvoiceNumber;

        await RunOutboxJobAsync(provider);
        await RunOutboxJobAsync(provider);
        await RunOutboxJobAsync(provider);

        Assert.Equal(1, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));
    }

    // ---- 5. Device failure defers rather than losing the document ----

    [Fact]
    public async Task Device_failure_defers_the_document_and_keeps_it_outbound()
    {
        var (provider, gateway) = await BuildProviderAsync(g => g.ThrowOnFiscalise = true);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);

        await RunOutboxJobAsync(provider);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);

        Assert.Equal(nameof(FiscalisationState.PendingRetry), record.Status);
        Assert.Equal(1, record.AttemptCount);
        Assert.Null(record.ReceiptGlobalNo);
        Assert.False(string.IsNullOrWhiteSpace(record.Message));
    }

    // ---- 6. A closed fiscal day is never opened implicitly ----

    [Fact]
    public async Task Closed_fiscal_day_defers_the_document_and_does_not_call_the_device()
    {
        var (provider, gateway) = await BuildProviderAsync(g => g.DayState = FiscalDayState.Closed);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);

        await RunOutboxJobAsync(provider);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);

        Assert.Equal(nameof(FiscalisationState.PendingRetry), record.Status);
        Assert.Contains("FiscalDayClosed", record.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("the document was not submitted", record.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.Received.Count(d => d.InvoiceNumber == record.InvoiceNumber));
    }

    // ---- 7. Offline retry flushes the engine queue instead of re-transacting ----

    [Fact]
    public async Task Retrying_an_offline_accepted_document_flushes_the_engine_queue()
    {
        var (provider, gateway) = await BuildProviderAsync(g => g.PendingSync = true);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        var invoiceNumber = await InvoiceNumberForSaleAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        // The document is now PendingRetry with a device receipt in hand.
        gateway.PendingQueueCount = 1;
        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.RetryAsync(recordId, "finance-1");

        Assert.True(result.Success);
        Assert.Equal(nameof(FiscalisationState.Fiscalised), result.Fiscalisation!.Status);
        Assert.Equal(1, gateway.SubmitPendingCallCount);

        // The engine already held the document, so it was never transacted twice. Scoped
        // to this document: the outbox drains the shared queue, so a global call counter
        // would also count sibling tests' documents.
        Assert.Equal(1, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));
    }

    // ---- 8. Retry of a never-transacted document re-drives the device ----

    [Fact]
    public async Task Retrying_a_never_submitted_document_calls_the_device()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        var invoiceNumber = await InvoiceNumberForSaleAsync(provider, saleId);

        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.RetryAsync(recordId, "finance-1");

        Assert.True(result.Success);
        Assert.Equal(nameof(FiscalisationState.Fiscalised), result.Fiscalisation!.Status);
        Assert.Equal(1, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));
    }

    // ---- 8b. An empty engine queue must never stand in for a fiscal result ----

    [Fact]
    public async Task Retrying_a_document_the_device_never_accepted_does_not_rely_on_an_empty_queue()
    {
        // The device throws before fiscalising, so the record is deferred to PendingRetry
        // with no receipt. The engine's own queue is empty and reports "0" — which says
        // nothing about this document. Marking it Fiscalised on that basis would report a
        // sale as fiscalised when the device never issued a receipt for it.
        var (provider, gateway) = await BuildProviderAsync(g => g.ThrowOnFiscalise = true);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        var invoiceNumber = await InvoiceNumberForSaleAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        var deferred = await FindRecordAsync(provider, recordId);
        Assert.Equal(nameof(FiscalisationState.PendingRetry), deferred.Status);
        Assert.True(string.IsNullOrEmpty(deferred.ReceiptGlobalNo));

        // The device recovers, but the queue is empty.
        gateway.ThrowOnFiscalise = false;
        gateway.PendingQueueCount = 0;
        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.RetryAsync(recordId, "finance-1");

        Assert.True(result.Success);
        Assert.Equal(nameof(FiscalisationState.Fiscalised), result.Fiscalisation!.Status);

        // Fiscalised only because the device was actually driven, not because the queue
        // drained. The document reached the device twice: the outbox attempt threw, and the
        // operator's retry is what actually obtained a receipt.
        Assert.Equal(0, gateway.SubmitPendingCallCount);
        Assert.Equal(2, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));
        Assert.False(string.IsNullOrEmpty(result.Fiscalisation!.ReceiptGlobalNo));
    }

    [Fact]
    public async Task A_deferred_document_with_an_empty_queue_is_never_marked_fiscalised_without_a_receipt()
    {
        // Same precondition, but the device keeps refusing. The record must stay visibly
        // unresolved rather than drifting to Fiscalised with no fiscal result on it.
        var (provider, gateway) = await BuildProviderAsync(g => g.ThrowOnFiscalise = true);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        gateway.PendingQueueCount = 0;
        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.RetryAsync(recordId, "finance-1");

        Assert.False(result.Success);
        var record = await FindRecordAsync(provider, recordId);
        Assert.NotEqual(nameof(FiscalisationState.Fiscalised), record.Status);
        Assert.True(string.IsNullOrEmpty(record.ReceiptGlobalNo));
    }

    // ---- 9. Retry of an already-fiscalised document is a no-op ----

    [Fact]
    public async Task Retrying_a_fiscalised_document_returns_it_unchanged()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.RetryAsync(recordId, "finance-1");

        Assert.True(result.Success);
        Assert.Equal("already-fiscalised", result.Code);
        Assert.Equal(1, gateway.FiscaliseCallCount);
    }

    // ---- 10. The tender breakdown survives into the fiscal record ----

    [Fact]
    public async Task Cash_card_and_split_tenders_are_preserved_verbatim_on_the_fiscal_record()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider, tender: Tender.ShownCashCardSplit);
        var context = provider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.SaleId == saleId);

        using var document = JsonDocument.Parse(record.TendersJson);
        var methods = document.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("paymentMethodCode").GetString())
            .ToArray();

        Assert.Contains("CASH", methods);
        Assert.Contains("CARD", methods);
        Assert.Equal(2, methods.Length);

        // The card leg must keep the authorisation reference the cashier captured.
        var card = document.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("paymentMethodCode").GetString() == "CARD");
        Assert.Equal("auth-d55", card.GetProperty("externalReference").GetString());
    }

    // ---- 11. Refunds raise a credit note, not a second invoice ----

    [Fact]
    public async Task Refunded_sale_produces_a_credit_note_carrying_the_original_invoice()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        await RunOutboxJobAsync(provider);

        var refundNumber = await RefundAsync(provider, saleId);
        var context = provider.GetRequiredService<ClubStackDbContext>();

        var invoice = await context.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.SaleId == saleId && f.RefundId == null);
        var saleInvoiceNumber = invoice.InvoiceNumber;

        var creditNote = await context.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.SaleId == saleId && f.RefundId != null);

        Assert.Equal(nameof(FiscalDocumentKind.CreditNote), creditNote.DocumentKind);
        Assert.Equal(refundNumber, creditNote.InvoiceNumber);
        Assert.Equal(nameof(FiscalisationState.Pending), creditNote.Status);

        // The sale already has its invoice, so the credit note is a second row.
        Assert.Equal(2, await context.Fiscalisations.CountAsync(f => f.SaleId == saleId));

        await RunOutboxJobAsync(provider);
        Assert.Equal(2, gateway.Received.Count(d =>
            d.InvoiceNumber == saleInvoiceNumber || d.InvoiceNumber == creditNote.InvoiceNumber));
        var credit = gateway.Received.Single(d => d.InvoiceNumber == creditNote.InvoiceNumber);
        Assert.Equal(FiscalDocumentKind.CreditNote, credit.Kind);
        Assert.False(string.IsNullOrWhiteSpace(credit.OriginalInvoiceNumber));
    }

    // ---- 12. Device status surfaces licensing without exposing key material ----

    [Fact]
    public async Task Device_status_reports_licensing_and_day_state_without_key_material()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();
        var status = await service.GetDeviceStatusAsync();

        Assert.True(status.Available);
        Assert.True(status.LicenseActive);
        Assert.Equal("FSTK-00000001", status.SerialNumber);
        Assert.Equal(nameof(FiscalDayState.Opened), status.FiscalDayState);
        Assert.DoesNotContain("key", JsonSerializer.Serialize(status), StringComparison.OrdinalIgnoreCase);
    }

    // ---- 13. Device configuration reports device-supplied tax identifiers ----

    [Fact]
    public async Task Device_configuration_exposes_tax_ids_read_from_the_device()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();
        var configuration = await service.GetDeviceConfigurationAsync();

        Assert.Equal(515, configuration.StandardRatedTaxId);
        Assert.Equal(1, configuration.ZeroRatedTaxId);
        Assert.Equal(2, configuration.ExemptTaxId);
    }

    // ---- 14. Fiscal day lifecycle ----

    [Fact]
    public async Task Open_and_close_fiscal_day_are_recorded_and_audited()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();

        var opened = await service.OpenFiscalDayAsync("finance-1");
        Assert.True(opened.Success);
        Assert.Equal(FiscalDayState.Opened, gateway.DayState);

        var closed = await service.CloseFiscalDayAsync("finance-1");
        Assert.True(closed.Success);
        Assert.Equal(FiscalDayState.Closed, gateway.DayState);
        Assert.Equal(nameof(FiscalDayState.Closed), closed.Status!.State);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var actions = await context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "Fiscalisation")
            .Select(a => a.Action)
            .ToListAsync();

        Assert.Contains("Fiscalisation.Day.Open", actions);
        Assert.Contains("Fiscalisation.Day.Close", actions);
    }

    // ---- 15. Search filters by status and sale ----

    [Fact]
    public async Task Search_filters_by_status_and_sale()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var service = provider.GetRequiredService<IFiscalisationService>();

        // Before the worker runs, this test's own document must be findable by sale and
        // must be Pending. The outbox drains the whole shared queue, so global status
        // totals move by however many sibling tests happen to have documents outstanding;
        // asserting a global delta here would be order-dependent. Everything is therefore
        // asserted against this test's own document.
        var bySaleBefore = await service.SearchAsync(new FiscalisationSearchInput(null, saleId));
        Assert.Equal(1, bySaleBefore.TotalCount);
        var own = bySaleBefore.Items.Single();
        Assert.Equal(nameof(FiscalisationState.Pending), own.Status);

        // A status filter that excludes it must not return it.
        var wrongStatus = await service.SearchAsync(
            new FiscalisationSearchInput(nameof(FiscalisationState.Failed), saleId));
        Assert.Equal(0, wrongStatus.TotalCount);

        await RunOutboxJobAsync(provider);

        // Status filter now matches this sale's document.
        var matched = await service.SearchAsync(
            new FiscalisationSearchInput(nameof(FiscalisationState.Fiscalised), saleId));
        Assert.Equal(1, matched.TotalCount);
        Assert.Equal(own.Id, matched.Items.Single().Id);

        // And the sale filter still works, now returning the fiscalised state.
        var bySale = await service.SearchAsync(new FiscalisationSearchInput(null, saleId));
        Assert.Equal(1, bySale.TotalCount);
        Assert.Equal(nameof(FiscalisationState.Fiscalised), bySale.Items.Single().Status);
    }

    // ---- 16. SubmitPending reports what the engine still holds ----

    [Fact]
    public async Task Submit_pending_flushes_the_engine_queue_and_reports_zero_remaining()
    {
        var (provider, gateway) = await BuildProviderAsync(g => g.PendingQueueCount = 3);
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();
        var remaining = await service.SubmitPendingAsync("finance-1");

        Assert.Equal(0, remaining);
        Assert.Equal(1, gateway.SubmitPendingCallCount);
    }

    // ---- 17. The engine document carries ClubStack's calculated tax, not a re-derivation ----

    [Fact]
    public async Task Engine_document_uses_the_sales_own_line_amounts_and_tax()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var context = provider.GetRequiredService<ClubStackDbContext>();
        var invoiceNumber = (await context.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.SaleId == saleId)).InvoiceNumber;

        await RunOutboxJobAsync(provider);

        var document = Assert.Single(gateway.Received, d => d.InvoiceNumber == invoiceNumber);
        var sale = await context.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .SingleAsync(s => s.Id == saleId);

        Assert.Equal(sale.GrandTotal, document.Total);
        Assert.Equal(sale.TaxTotal, document.TaxTotal);
        Assert.Equal(sale.SaleNumber, document.InvoiceNumber);
        Assert.Equal(sale.Lines.Count, document.Lines.Count);

        var saleLine = sale.Lines[0];
        var fiscalLine = document.Lines[0];
        Assert.Equal(saleLine.LineTotal, fiscalLine.Amount);
        Assert.Equal(saleLine.TaxAmount, fiscalLine.Tax);
        Assert.Equal(saleLine.TaxRate, fiscalLine.TaxRate);
        Assert.Equal(saleLine.ProductSku, fiscalLine.ItemCode);
    }

    // ---- 18. Zero-rated lines get the device's zero-rated identifier ----

    [Fact]
    public async Task Zero_rated_lines_use_the_device_zero_rated_tax_id()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var invoiceNumber = await InvoiceNumberForNewSaleAsync(provider, taxTreatment: "ZeroRated");
        await RunOutboxJobAsync(provider);

        var document = Assert.Single(gateway.Received, d => d.InvoiceNumber == invoiceNumber);
        Assert.All(document.Lines, l => Assert.Equal(1, l.TaxId));
    }

    // ---- 19. Standard-rated lines get the device's standard identifier ----

    [Fact]
    public async Task Standard_rated_lines_use_the_device_standard_tax_id()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var invoiceNumber = await InvoiceNumberForNewSaleAsync(provider, taxTreatment: "Exclusive");
        await RunOutboxJobAsync(provider);

        var document = Assert.Single(gateway.Received, d => d.InvoiceNumber == invoiceNumber);
        Assert.All(document.Lines, l => Assert.Equal(515, l.TaxId));
    }

    // ---- 20. No license or activation key is written to the audit trail ----

    [Fact]
    public async Task Audit_metadata_never_carries_license_or_activation_material()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();
        await service.OpenFiscalDayAsync("finance-1");
        await service.SubmitPendingAsync("finance-1");

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var metadata = await context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "Fiscalisation")
            .Select(a => a.MetadataJson ?? string.Empty)
            .ToListAsync();

        Assert.NotEmpty(metadata);
        Assert.All(metadata, m =>
        {
            Assert.DoesNotContain("license", m, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("activation", m, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ---- 21. SetLicense goes through the engine and is not retained by ClubStack ----

    [Fact]
    public async Task Setting_a_license_delegates_to_the_engine_and_is_not_stored()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var gatewayInterface = provider.GetRequiredService<IFiscalisationGateway>();
        var installed = await gatewayInterface.SetLicenseAsync("TEST-KEY-NOT-REAL");

        Assert.True(installed);
        Assert.Equal("TEST-KEY-NOT-REAL", Assert.Single(gateway.LicensesApplied));

        // ClubStack must not retain the key anywhere in its own persistence.
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var stored = await context.Fiscalisations.AsNoTracking()
            .Select(f => new { f.Message, f.ErrorInfo, f.DeviceSignature })
            .ToListAsync();

        Assert.DoesNotContain(stored, row =>
            (row.Message ?? string.Empty).Contains("TEST-KEY-NOT-REAL", StringComparison.Ordinal) ||
            (row.ErrorInfo ?? string.Empty).Contains("TEST-KEY-NOT-REAL", StringComparison.Ordinal));
    }

    // ---- 21a. Device administration is permission-gated, audited, and leak-free ----

    [Fact]
    public async Task Service_level_license_install_delegates_and_audits_without_the_key()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var actor = $"finance-{Guid.NewGuid():N}";
        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.SetLicenseAsync("SECRET-LICENSE-KEY", actor);

        Assert.True(result.Success);
        Assert.Equal("license-installed", result.Code);
        Assert.Equal("SECRET-LICENSE-KEY", Assert.Single(gateway.LicensesApplied));
        Assert.True(result.Status!.LicenseActive);

        // The action is audited...
        var metadata = await AuditMetadataAsync(
            provider,
            "Fiscalisation.Device.SetLicense",
            actor);

        Assert.Single(metadata);

        // ...but the key never reaches the audit trail.
        Assert.DoesNotContain("SECRET-LICENSE-KEY", metadata[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_level_license_install_rejects_a_blank_key_before_calling_the_device()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SetLicenseAsync("   ", "finance-1"));

        Assert.Empty(gateway.LicensesApplied);
    }

    [Fact]
    public async Task Service_level_configuration_update_forwards_identity_and_never_stores_the_activation_key()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var actor = $"finance-{Guid.NewGuid():N}";
        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.UpdateConfigurationAsync(
            new FiscalDeviceConfigurationUpdate(
                "18084",
                "FSTK-00000002",
                "Harare Sports Club",
                "Harare",
                "Harare",
                "ACTIVATION-KEY-9999",
                @"C:\FiscalStackZW certificates"),
            actor);

        Assert.True(result.Success);
        Assert.Equal("device-configured", result.Code);

        var applied = Assert.Single(gateway.ConfigurationsApplied);
        Assert.Equal("18084", applied.DeviceId);
        Assert.Equal("FSTK-00000002", applied.SerialNumber);
        Assert.Equal("Harare Sports Club", applied.CompanyName);
        Assert.Equal(@"C:\FiscalStackZW certificates", applied.CertificateFolder);
        Assert.Equal("ACTIVATION-KEY-9999", applied.ActivationKey);

        var metadata = await AuditMetadataAsync(
            provider,
            "Fiscalisation.Device.UpdateConfiguration",
            actor);

        Assert.Single(metadata);
        Assert.Contains("FSTK-00000002", metadata[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATION-KEY-9999", metadata[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_level_configuration_update_requires_the_fields_the_engine_needs()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateConfigurationAsync(
            new FiscalDeviceConfigurationUpdate("18084", "", "Club", "", "", "KEY", ""),
            "finance-1"));

        Assert.Empty(gateway.ConfigurationsApplied);
    }

    [Fact]
    public async Task Device_status_never_echoes_the_license_or_activation_key()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var service = provider.GetRequiredService<IFiscalisationService>();
        await service.SetLicenseAsync("SECRET-LICENSE-KEY", "finance-1");
        await service.UpdateConfigurationAsync(
            new FiscalDeviceConfigurationUpdate(
                "18084", "FSTK-00000002", "Club", "Branch", "City",
                "ACTIVATION-KEY-9999", @"C:\certs"),
            "finance-1");

        var status = await service.GetDeviceStatusAsync();
        var configuration = await service.GetDeviceConfigurationAsync();

        var statusJson = JsonSerializer.Serialize(status);
        var configurationJson = JsonSerializer.Serialize(configuration);

        Assert.DoesNotContain("SECRET-LICENSE-KEY", statusJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATION-KEY-9999", statusJson, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-LICENSE-KEY", configurationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATION-KEY-9999", configurationJson, StringComparison.Ordinal);
    }

    // ---- 22. The one-invoice-per-sale invariant is enforced by the database ----

    [Fact]
    public async Task A_second_invoice_for_the_same_sale_is_rejected_by_the_database()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var context = provider.GetRequiredService<ClubStackDbContext>();

        var duplicate = new FiscalisationEntity
        {
            SaleId = saleId,
            SaleNumber = "SALE-DUP",
            InvoiceNumber = "SALE-DUP",
            DocumentKind = nameof(FiscalDocumentKind.FiscalInvoice),
            Status = nameof(FiscalisationState.Pending),
            TendersJson = "[]",
            ValidationWarningsJson = "[]",
        };

        context.Fiscalisations.Add(duplicate);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    // ---- 23. Both fiscalisation event types are registered for delivery ----

    [Fact]
    public void Both_fiscalisation_handlers_are_registered()
    {
        using var provider = TestServiceProvider.Build(Fixture.ConnectionString);
        var handlers = provider.GetServices<IOutboxMessageHandler>().ToArray();

        Assert.Contains(handlers, h => h.HandledEventType == FiscalizationEventTypes.FiscalisationRequired);
        Assert.Contains(handlers, h => h.HandledEventType == FiscalizationEventTypes.CreditNoteRequired);
    }

    // ---- 24. A credit-note event is delivered to the credit-note handler ----

    [Fact]
    public async Task Credit_note_messages_reach_the_credit_note_handler()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var creditNote = await context.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.SaleId == saleId && f.RefundId != null);

        var pending = await context.OutboxMessages.AsNoTracking()
            .SingleAsync(o => o.EventType == FiscalizationEventTypes.CreditNoteRequired &&
                              o.PayloadJson.Contains(creditNote.Id.ToString()));
        Assert.Equal(OutboxStatus.Pending, pending.Status);

        await RunOutboxJobAsync(provider);

        // The credit note reached the device exactly once, carrying its own invoice number.
        var credit = Assert.Single(gateway.Received, d => d.InvoiceNumber == creditNote.InvoiceNumber);
        Assert.Equal(FiscalDocumentKind.CreditNote, credit.Kind);
    }

    // ---- 25. An unreadable outbox payload is not retried forever ----

    [Fact]
    public async Task Unreadable_fiscalisation_payload_is_marked_processed_without_a_device_call()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var correlationId = Guid.NewGuid().ToString("N");
        var store = provider.GetRequiredService<IOutboxStore>();
        await store.EnqueueAsync(FiscalizationEventTypes.FiscalisationRequired, "{not json", correlationId);

        await RunOutboxJobAsync(provider);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var message = await context.OutboxMessages.AsNoTracking()
            .SingleAsync(o => o.CorrelationId == correlationId);

        Assert.Equal(OutboxStatus.Processed, message.Status);
        Assert.Equal(0, gateway.FiscaliseCallCount);
    }

    // ---- helpers ----

    private async Task<(ServiceProvider Provider, FakeFiscalisationGateway Gateway)> BuildProviderAsync(
        Action<FakeFiscalisationGateway>? configure = null)
    {
        var gateway = new FakeFiscalisationGateway();
        configure?.Invoke(gateway);

        var provider = TestServiceProvider.Build(
            Fixture.ConnectionString,
            services => services.AddSingleton<IFiscalisationGateway>(gateway));

        // Seed reference data (default currency, tax defaults) so this class does not
        // depend on another test in the shared collection having run first.
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();

        return (provider, gateway);
    }

    private enum Tender
    {
        Cash,
        ShownCashCardSplit,
    }

    /// <summary>
    /// The refund line snapshot is written with the POS serializer's camelCase web
    /// defaults, so tests that read or rewrite it must use the same options or the tax
    /// members silently deserialize as null.
    /// </summary>
    private static readonly JsonSerializerOptions RefundSnapshotJson = new(JsonSerializerDefaults.Web);

    private async Task<Guid> CreateCompletedSaleAsync(
        IServiceProvider provider,
        Tender tender = Tender.Cash,
        string taxTreatment = "Inclusive")
    {
        using var scope = provider.CreateScope();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var actor = $"d55-{suffix}";

        var platform = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
        var categories = scope.ServiceProvider.GetRequiredService<IProductCategoryService>();
        var locations = scope.ServiceProvider.GetRequiredService<IInventoryLocationService>();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var posConfig = scope.ServiceProvider.GetRequiredService<IPosConfigurationService>();
        var cashiering = scope.ServiceProvider.GetRequiredService<ICashierSessionService>();
        var sales = scope.ServiceProvider.GetRequiredService<ISaleService>();

        // A zero-rated product needs no tax rate; the other treatments do.
        Guid? taxRateId = null;
        if (taxTreatment != "ZeroRated")
        {
            var taxRate = await platform.CreateTaxRateAsync(
                new TaxRateInput($"VAT15{suffix}", 0.15m, true));
            taxRateId = taxRate.Id;
        }

        var category = await categories.CreateAsync(
            new CreateCategoryInput($"CAT{suffix}", "D5.5 category", null), actor);
        var location = await locations.CreateAsync(
            new CreateInventoryLocationInput($"LOC{suffix}", "D5.5 location", "Store"), actor);

        var product = await products.CreateAsync(
            new CreateProductInput(
                $"SKU{suffix}",
                $"D5.5 product {suffix}",
                category.Id,
                "Each",
                Barcode: null,
                StockManaged: false,
                Cost: 5m,
                PriceAmount: 100m,
                ReorderLevel: null,
                SupplierId: null,
                CurrencyCode: null,
                TaxRateId: taxRateId,
                TaxTreatment: taxTreatment),
            actor);

        var outlet = await posConfig.CreateOutletAsync(
            new CreatePosOutletInput($"OUT{suffix}", "D5.5 outlet", null), actor);
        var register = await posConfig.CreateRegisterAsync(
            new CreatePosRegisterInput(outlet.Id, $"REG{suffix}", "D5.5 register", location.Id), actor);

        await cashiering.OpenAsync(new OpenSessionInput(register.Id, 0m, null), actor);

        var draft = await sales.CreateAsync(
            new CreateSaleInput(
                register.Id,
                MemberId: null,
                CredentialValue: null,
                Lines: [new CreateSaleLineInput(product.Id, 1m, null, null)],
                Note: null,
                BusinessDateUtc: null),
            actor);

        // The total differs by treatment, so balance against the draft's own total.
        var total = draft.GrandTotal;

        var payments = tender == Tender.ShownCashCardSplit
            ? new List<SalePaymentInput>
            {
                new("CASH", decimal.Round(total * 0.6m, 2), null),
                new("CARD", decimal.Round(total * 0.4m, 2), "auth-d55"),
            }
            : new List<SalePaymentInput> { new("CASH", total, null) };

        // Correct any rounding drift on the second split line.
        if (tender == Tender.ShownCashCardSplit)
        {
            payments[1] = new SalePaymentInput("CARD", total - payments[0].Amount, "auth-d55");
        }

        await sales.CompleteAsync(
            draft.Id,
            new CompleteSaleInput(null, payments, $"d55-{suffix}"),
            actor,
            Guid.NewGuid().ToString("N"));

        return draft.Id;
    }

    private async Task<string?> RefundAsync(IServiceProvider provider, Guid saleId)
    {
        using var scope = provider.CreateScope();
        var sales = scope.ServiceProvider.GetRequiredService<ISaleService>();
        var result = await sales.RefundAsync(
            saleId,
            new RefundSaleInput("Fiscal credit note test", null, $"d55r-{Guid.NewGuid():N}"),
            "test-admin",
            Guid.NewGuid().ToString("N"));

        return result.RefundNumber;
    }

    /// <summary>
    /// Completes a fresh sale and returns the invoice number the device document will carry,
    /// so assertions can target this test's own document in the shared collection database.
    /// </summary>
    private async Task<string> InvoiceNumberForNewSaleAsync(
        IServiceProvider provider,
        Tender tender = Tender.Cash,
        string taxTreatment = "Inclusive")
    {
        var saleId = await CreateCompletedSaleAsync(provider, tender, taxTreatment);
        return await InvoiceNumberForSaleAsync(provider, saleId);
    }

    private static async Task<string> InvoiceNumberForSaleAsync(IServiceProvider provider, Guid saleId)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        return (await context.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.SaleId == saleId && f.RefundId == null)).InvoiceNumber;
    }

    /// <summary>
    /// Audit rows written by this test only. The collection database is shared, so rows
    /// are identified by a per-test actor rather than by action alone.
    /// </summary>
    private static async Task<List<string>> AuditMetadataAsync(
        IServiceProvider provider,
        string action,
        string actorUserId)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        return await context.AuditLogs.AsNoTracking()
            .Where(a => a.Action == action && a.ActorUserId == actorUserId)
            .OrderBy(a => a.TimestampUtc)
            .Select(a => a.MetadataJson ?? string.Empty)
            .ToListAsync();
    }

    private static async Task<Guid> OnlyRecordIdAsync(IServiceProvider provider, Guid saleId)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        return await context.Fiscalisations.AsNoTracking()
            .Where(f => f.SaleId == saleId && f.RefundId == null)
            .Select(f => f.Id)
            .SingleAsync();
    }

    private static async Task<FiscalisationEntity> FindRecordAsync(IServiceProvider provider, Guid recordId)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        return await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);
    }

    private static async Task<string?> ReadPaymentMappingAfterRunAsync(IServiceProvider provider, Guid recordId)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);
        return record.PaymentMapping ?? "aggregate-single-currency";
    }

    // ---- 31. Mixed-rate refund tax: each line credits its own bracket ----

    /// <summary>
    /// A sale carrying one standard-rated and one zero-rated line, refunded in full.
    /// This is the case a proportional share of the invoice tax total gets wrong: the
    /// invoice's tax belongs entirely to the standard-rated line, so smearing it across
    /// both lines credits the zero-rated line with tax it never paid.
    /// </summary>
    [Fact]
    public async Task Mixed_rate_credit_note_credits_each_line_into_its_own_tax_bracket()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateMixedRateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        var creditNote = gateway.Received.Single(d => d.Kind == FiscalDocumentKind.CreditNote);

        // Two lines, and the zero-rated one carries no tax at all.
        Assert.Equal(2, creditNote.Lines.Count);

        var zeroRated = creditNote.Lines.Single(l => l.Tax == 0m);
        var standardRated = creditNote.Lines.Single(l => l.Tax > 0m);

        // Each line keeps its own treatment: the zero-rated line is credited into the
        // device's zero-rated bracket and the standard-rated line into the standard one.
        // Smearing would have put both into the same bracket.
        Assert.Equal(1, zeroRated.TaxId);
        Assert.Equal(515, standardRated.TaxId);
        Assert.NotEqual(zeroRated.TaxId, standardRated.TaxId);

        // The tax rate travelled with the treatment rather than being flattened to zero.
        Assert.Equal(0m, zeroRated.TaxRate);
        Assert.Equal(0.15m, standardRated.TaxRate);
    }

    /// <summary>
    /// The credit note's tax total is the sum of the tax the invoice actually charged, so
    /// a full refund credits the invoice's tax total exactly rather than a rounded share
    /// of it.
    /// </summary>
    [Fact]
    public async Task Mixed_rate_credit_note_tax_total_equals_the_tax_the_invoice_charged()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateMixedRateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        var invoice = gateway.Received.Single(d => d.Kind == FiscalDocumentKind.FiscalInvoice);
        var creditNote = gateway.Received.Single(d => d.Kind == FiscalDocumentKind.CreditNote);

        Assert.Equal(invoice.TaxTotal, creditNote.TaxTotal);
        Assert.Equal(invoice.Total, creditNote.Total);
        Assert.True(invoice.TaxTotal > 0m);
    }

    /// <summary>
    /// The refund snapshot itself has to carry the tax, otherwise the credit note cannot be
    /// rebuilt later and the next session would be back to proportional derivation.
    /// </summary>
    [Fact]
    public async Task Refund_line_snapshot_carries_the_original_line_tax_treatment()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateMixedRateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var refund = await context.SaleRefunds.AsNoTracking().SingleAsync(r => r.SaleId == saleId);

        var lines = JsonSerializer.Deserialize<List<SaleRefundLineDto>>(refund.LinesJson, RefundSnapshotJson)!;

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.NotNull(l.TaxTreatment));
        Assert.Contains(lines, l => l.TaxAmount > 0m);
        Assert.Contains(lines, l => l.TaxAmount == 0m);
    }

    /// <summary>
    /// A refund written before D5.5 has no tax in its snapshot. It must still fiscalise —
    /// the credit note is owed to ZIMRA whether or not ClubStack recorded the breakdown —
    /// using the previous proportional behaviour rather than silently dropping the tax.
    /// </summary>
    [Fact]
    public async Task Legacy_refund_without_line_tax_still_fiscalises_the_full_credit()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateMixedRateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);

        // Rewrite the snapshot into the pre-D5.5 shape: no tax members at all.
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
            var refund = await context.SaleRefunds.SingleAsync(r => r.SaleId == saleId);
            var lines = JsonSerializer.Deserialize<List<SaleRefundLineDto>>(refund.LinesJson, RefundSnapshotJson)!;
            var legacy = lines
                .Select(l => new SaleRefundLineDto(l.ProductId, l.ProductSku, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal))
                .ToList();
            refund.LinesJson = JsonSerializer.Serialize(legacy, RefundSnapshotJson);
            await context.SaveChangesAsync();
        }

        await RunOutboxJobAsync(provider);

        var creditNote = gateway.Received.Single(d => d.Kind == FiscalDocumentKind.CreditNote);
        Assert.Equal(2, creditNote.Lines.Count);
        Assert.True(creditNote.TaxTotal > 0m);
    }

    /// <summary>
    /// A snapshot where only some lines carry tax cannot be turned into an honest credit
    /// note: the untagged lines would be credited at zero tax. It is rejected rather than
    /// guessed at, so a corrupt snapshot cannot quietly under-credit a ZIMRA document.
    /// </summary>
    [Fact]
    public async Task Partially_taxed_refund_snapshot_is_rejected_rather_than_guessed()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateMixedRateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);

        Guid refundId;
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
            var refund = await context.SaleRefunds.SingleAsync(r => r.SaleId == saleId);
            refundId = refund.Id;
            var lines = JsonSerializer.Deserialize<List<SaleRefundLineDto>>(refund.LinesJson, RefundSnapshotJson)!;
            // Strip the tax from one line only.
            var first = lines[0];
            lines[0] = first with { TaxTreatment = null, TaxRateName = null, TaxRate = null, TaxAmount = 0m };
            refund.LinesJson = JsonSerializer.Serialize(lines, RefundSnapshotJson);
            await context.SaveChangesAsync();
        }

        await RunOutboxJobAsync(provider);

        // The processor must not have produced a credit note with a guessed breakdown.
        Assert.DoesNotContain(gateway.Received, d => d.Kind == FiscalDocumentKind.CreditNote);

        using var verify = provider.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var record = await verifyContext.Fiscalisations.AsNoTracking()
            .SingleAsync(f => f.RefundId == refundId);

        // A permanent failure, not a deferral: retrying cannot fix inconsistent data, so
        // the record keeps the reason and stays visible to an operator.
        Assert.Equal(nameof(FiscalisationState.Failed), record.Status);
        Assert.False(string.IsNullOrWhiteSpace(record.Message));
        Assert.NotEqual(nameof(FiscalisationState.Fiscalised), record.Status);
    }

    // ---- 32. One credit note per refund, enforced by the database ----

    [Fact]
    public async Task One_refund_creates_exactly_one_credit_note()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var refundId = await context.SaleRefunds.AsNoTracking()
            .Where(r => r.SaleId == saleId)
            .Select(r => r.Id)
            .SingleAsync();

        Assert.Equal(1, await context.Fiscalisations.AsNoTracking().CountAsync(f => f.RefundId == refundId));
    }

    [Fact]
    public async Task A_second_credit_note_for_the_same_refund_is_rejected_by_the_database()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        await RefundAsync(provider, saleId);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var refund = await context.SaleRefunds.AsNoTracking().SingleAsync(r => r.SaleId == saleId);

        var duplicate = new FiscalisationEntity
        {
            SaleId = saleId,
            SaleNumber = "SALE-CNDUP",
            RefundId = refund.Id,
            RefundNumber = refund.RefundNumber,
            InvoiceNumber = "CNDUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            DocumentKind = nameof(FiscalDocumentKind.CreditNote),
            Status = nameof(FiscalisationState.Pending),
            TendersJson = "[]",
            ValidationWarningsJson = "[]",
        };

        context.Fiscalisations.Add(duplicate);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_credit_note_must_reference_a_refund_that_exists()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var context = provider.GetRequiredService<ClubStackDbContext>();

        var orphan = new FiscalisationEntity
        {
            SaleId = saleId,
            SaleNumber = "SALE-ORPHAN",
            RefundId = Guid.CreateVersion7(),
            InvoiceNumber = "ORPHAN-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            DocumentKind = nameof(FiscalDocumentKind.CreditNote),
            Status = nameof(FiscalisationState.Pending),
            TendersJson = "[]",
            ValidationWarningsJson = "[]",
        };

        context.Fiscalisations.Add(orphan);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>
    /// The filtered index must not break normal sales: several unrelated sales each keep
    /// their own invoice, even though every invoice row leaves RefundId null and would all
    /// collide under a plain unique index on RefundId.
    /// </summary>
    [Fact]
    public async Task Invoice_uniqueness_is_unaffected_by_the_refund_index()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var first = await CreateCompletedSaleAsync(provider);
        var second = await CreateCompletedSaleAsync(provider);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();

        Assert.Equal(1, await context.Fiscalisations.AsNoTracking().CountAsync(f => f.SaleId == first && f.RefundId == null));
        Assert.Equal(1, await context.Fiscalisations.AsNoTracking().CountAsync(f => f.SaleId == second && f.RefundId == null));
    }

    /// <summary>
    /// A credit note for a different refund on the same sale is a different document and
    /// must be allowed.
    /// </summary>
    [Fact]
    public async Task Another_refund_creates_its_own_credit_note()
    {
        var (provider, _) = await BuildProviderAsync();
        await using var _ = provider;

        var first = await CreateCompletedSaleAsync(provider);
        var second = await CreateCompletedSaleAsync(provider);
        await RefundAsync(provider, first);
        await RefundAsync(provider, second);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();

        var firstRefundId = await context.SaleRefunds.AsNoTracking()
            .Where(r => r.SaleId == first).Select(r => r.Id).SingleAsync();
        var secondRefundId = await context.SaleRefunds.AsNoTracking()
            .Where(r => r.SaleId == second).Select(r => r.Id).SingleAsync();

        Assert.Equal(1, await context.Fiscalisations.AsNoTracking().CountAsync(f => f.RefundId == firstRefundId));
        Assert.Equal(1, await context.Fiscalisations.AsNoTracking().CountAsync(f => f.RefundId == secondRefundId));
    }

    // ---- 33. Failed documents keep enough information to be retried ----

    /// <summary>
    /// A device rejection is a permanent failure, and the record has to keep the reason so
    /// an authorised operator can see what the device objected to. It must not be reported
    /// as fiscalised.
    /// </summary>
    [Fact]
    public async Task A_rejected_document_is_failed_with_its_reason_and_can_be_retried()
    {
        var (provider, gateway) = await BuildProviderAsync(g =>
        {
            g.AcceptResult = false;
            g.RejectionMessage = "Tax identifier 515 is not configured on this device.";
        });
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        var invoiceNumber = await InvoiceNumberForSaleAsync(provider, saleId);
        await RunOutboxJobAsync(provider);

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var failed = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);

        Assert.Equal(nameof(FiscalisationState.Failed), failed.Status);
        Assert.Equal("Tax identifier 515 is not configured on this device.", failed.Message);
        Assert.Equal(1, failed.AttemptCount);
        Assert.NotNull(failed.LastAttemptAtUtc);
        // A rejection is not a fiscal result.
        Assert.Null(failed.FiscalisedAtUtc);

        // Once the device accepts, an authorised retry drives it to fiscalised.
        gateway.AcceptResult = true;
        var service = provider.GetRequiredService<IFiscalisationService>();
        var result = await service.RetryAsync(recordId, "finance-1");

        Assert.True(result.Success);
        Assert.Equal(nameof(FiscalisationState.Fiscalised), result.Fiscalisation!.Status);
        Assert.Equal(2, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));
    }

    /// <summary>
    /// Part 8. A document the engine has already accepted must never be sent twice, however
    /// many times the worker runs and however often it is retried. A real timeout after
    /// acceptance cannot be produced against a local device, so this is the deterministic
    /// equivalent: the receipt is already in hand, and every subsequent path is a no-op or
    /// a queue flush.
    /// </summary>
    [Fact]
    public async Task Repeated_worker_runs_and_retries_never_produce_a_second_device_document()
    {
        var (provider, gateway) = await BuildProviderAsync(g => g.PendingSync = true);
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var recordId = await OnlyRecordIdAsync(provider, saleId);
        var invoiceNumber = await InvoiceNumberForSaleAsync(provider, saleId);

        // First pass: the engine accepts offline, so the document is PendingRetry.
        await RunOutboxJobAsync(provider);

        // Second pass with nothing changed: the worker must not re-transact, because the
        // engine already holds the document and a receipt was recorded.
        await RunOutboxJobAsync(provider);

        // Retry: still a queue flush, not a second transaction.
        gateway.PendingQueueCount = 1;
        var service = provider.GetRequiredService<IFiscalisationService>();
        await service.RetryAsync(recordId, "finance-1");

        // And once the engine accepts synchronisation, another retry is a terminal no-op.
        gateway.PendingSync = false;
        gateway.PendingQueueCount = 0;
        await service.RetryAsync(recordId, "finance-1");

        Assert.Equal(1, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));

        var context = provider.GetRequiredService<ClubStackDbContext>();
        var record = await context.Fiscalisations.AsNoTracking().SingleAsync(f => f.Id == recordId);
        Assert.Equal(nameof(FiscalisationState.Fiscalised), record.Status);
    }

    /// <summary>
    /// The device must never be called for a document that is already fiscalised, however
    /// many times the outbox job runs. This is the guarantee that a duplicate ZIMRA
    /// document cannot be produced by a repeated worker tick.
    /// </summary>
    [Fact]
    public async Task A_fiscalised_document_is_never_sent_to_the_device_again()
    {
        var (provider, gateway) = await BuildProviderAsync();
        await using var _ = provider;

        var saleId = await CreateCompletedSaleAsync(provider);
        var invoiceNumber = await InvoiceNumberForSaleAsync(provider, saleId);

        await RunOutboxJobAsync(provider);
        await RunOutboxJobAsync(provider);
        await RunOutboxJobAsync(provider);

        Assert.Equal(1, gateway.Received.Count(d => d.InvoiceNumber == invoiceNumber));
    }

    /// <summary>
    /// Completes a sale with one standard-rated line and one zero-rated line so the
    /// per-bracket refund behaviour can be observed. The standard-rated line is priced so
    /// the two lines have clearly different tax, which is what makes a smeared credit
    /// detectable.
    /// </summary>
    private async Task<Guid> CreateMixedRateCompletedSaleAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var actor = $"d55mix-{suffix}";

        var platform = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
        var categories = scope.ServiceProvider.GetRequiredService<IProductCategoryService>();
        var locations = scope.ServiceProvider.GetRequiredService<IInventoryLocationService>();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var posConfig = scope.ServiceProvider.GetRequiredService<IPosConfigurationService>();
        var cashiering = scope.ServiceProvider.GetRequiredService<ICashierSessionService>();
        var sales = scope.ServiceProvider.GetRequiredService<ISaleService>();

        var taxRate = await platform.CreateTaxRateAsync(new TaxRateInput($"VATMIX{suffix}", 0.15m, true));

        var category = await categories.CreateAsync(
            new CreateCategoryInput($"CATM{suffix}", "D5.5 mixed category", null), actor);
        var location = await locations.CreateAsync(
            new CreateInventoryLocationInput($"LOCM{suffix}", "D5.5 mixed location", "Store"), actor);

        var standardRated = await products.CreateAsync(
            new CreateProductInput(
                $"SKUM{suffix}-S", $"Standard rated {suffix}", category.Id, "Each", Barcode: null,
                StockManaged: false, Cost: 5m, PriceAmount: 115m, ReorderLevel: null, SupplierId: null,
                CurrencyCode: null, TaxRateId: taxRate.Id, TaxTreatment: "Exclusive"),
            actor);

        var zeroRated = await products.CreateAsync(
            new CreateProductInput(
                $"SKUM{suffix}-Z", $"Zero rated {suffix}", category.Id, "Each", Barcode: null,
                StockManaged: false, Cost: 5m, PriceAmount: 40m, ReorderLevel: null, SupplierId: null,
                CurrencyCode: null, TaxRateId: null, TaxTreatment: "ZeroRated"),
            actor);

        var outlet = await posConfig.CreateOutletAsync(
            new CreatePosOutletInput($"OUTM{suffix}", "D5.5 mixed outlet", null), actor);
        var register = await posConfig.CreateRegisterAsync(
            new CreatePosRegisterInput(outlet.Id, $"REGM{suffix}", "D5.5 mixed register", location.Id), actor);

        await cashiering.OpenAsync(new OpenSessionInput(register.Id, 0m, null), actor);

        var draft = await sales.CreateAsync(
            new CreateSaleInput(
                register.Id,
                MemberId: null,
                CredentialValue: null,
                Lines:
                [
                    new CreateSaleLineInput(standardRated.Id, 2m, null, null),
                    new CreateSaleLineInput(zeroRated.Id, 1m, null, null),
                ],
                Note: null,
                BusinessDateUtc: null),
            actor);

        await sales.CompleteAsync(
            draft.Id,
            new CompleteSaleInput(null, new List<SalePaymentInput> { new("CASH", draft.GrandTotal, null) }, actor),
            actor,
            Guid.NewGuid().ToString("N"));

        return draft.Id;
    }
}
