namespace ClubStack.Hardware.Fiscalization;

/// <summary>
/// ZIMRA fiscal document kinds, mapped onto the engine's <c>InvoiceFlag</c>
/// ("01" sale, "02" credit note, "03" debit note) by the adapter.
/// </summary>
public enum FiscalDocumentKind
{
    FiscalInvoice = 0,
    CreditNote = 1,
    DebitNote = 2,
}

/// <summary>
/// Lifecycle of a fiscalisation attempt. Mirrors the persisted
/// <c>fiscalisation_status</c> column; a sale is never treated as fiscalised
/// because the operational sale completed.
/// </summary>
public enum FiscalisationState
{
    NotRequired = 0,
    Pending = 1,
    Submitted = 2,
    Fiscalised = 3,
    PendingRetry = 4,
    Failed = 5,
}

/// <summary>Fiscal-day lifecycle as reported by the fiscal device.</summary>
public enum FiscalDayState
{
    Unknown = 0,
    Opened = 1,
    Closed = 2,
    Closing = 3,
}

/// <summary>
/// Which ZIMRA FDMS environment the device is bound to.
///
/// The engine stores this as a single value (<c>LAST_ENVIRONMENT</c> plus the
/// <c>ZIMRASERVER</c>/<c>VERIFICATIONSERVER</c> pair it implies) rather than as a URL
/// that each call site re-interprets. Every environment decision in ClubStack
/// resolves through <see cref="FiscalEndpoints"/> so there is exactly one mapping.
/// </summary>
public enum FiscalEnvironment
{
    /// <summary>Live FDMS. Never used for a device provisioned as a test device.</summary>
    Production = 0,

    /// <summary>FDMS test host. Required for D5.5 verification.</summary>
    Test = 1,
}

/// <summary>
/// One fiscal receipt line. Values are derived from an already-calculated
/// ClubStack sale line; the adapter never re-prices and never invents a rate.
/// </summary>
public sealed record FiscalLine
{
    /// <summary>HS code. Supplied by ClubStack; the engine normalises unknown codes.</summary>
    public string ItemCode { get; init; } = string.Empty;

    public string Name1 { get; init; } = string.Empty;

    public string? Name2 { get; init; }

    public decimal Quantity { get; init; }

    public decimal Price { get; init; }

    /// <summary>Line total as calculated by ClubStack.</summary>
    public decimal Amount { get; init; }

    /// <summary>Tax amount as calculated by ClubStack.</summary>
    public decimal Tax { get; init; }

    /// <summary>Tax rate as calculated by ClubStack.</summary>
    public decimal TaxRate { get; init; }

    /// <summary>
    /// FDMS tax identifier. 0 asks the engine to select from its own configured
    /// table; a positive value is the standard/zero/exempt id read from the
    /// device configuration rather than hard-coded here.
    /// </summary>
    public int TaxId { get; init; }
}

/// <summary>
/// The fiscal document to produce. Mirrors the engine's <c>TransactM</c>
/// contract; this is a ClubStack-owned shape, not a DLL passthrough.
/// </summary>
public sealed record FiscalDocument
{
    /// <summary>Unique invoice number in the taxpayer context. Reuses the ClubStack sale number.</summary>
    public string InvoiceNumber { get; init; } = string.Empty;

    public FiscalDocumentKind Kind { get; init; } = FiscalDocumentKind.FiscalInvoice;

    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Grand total including tax, as calculated by ClubStack.</summary>
    public decimal Total { get; init; }

    /// <summary>Tax component of <see cref="Total"/>, as calculated by ClubStack.</summary>
    public decimal TaxTotal { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerTaxNumber { get; init; }

    public string? CustomerEmail { get; init; }

    public string? CustomerPhone { get; init; }

    public string? CustomerAddress { get; init; }

    /// <summary>Customer tax identification number, when supplied by ClubStack.</summary>
    public string? CustomerTin { get; init; }

    /// <summary>Required for a credit/debit note; the fiscalised original invoice number.</summary>
    public string? OriginalInvoiceNumber { get; init; }

    public string? Comment { get; init; }

    public IReadOnlyList<FiscalLine> Lines { get; init; } = Array.Empty<FiscalLine>();

    /// <summary>
    /// ClubStack tender breakdown, preserved verbatim for audit. The engine's
    /// supported aggregate representation is derived from this; it is never
    /// replaced by it.
    /// </summary>
    public IReadOnlyList<FiscalTender> Tenders { get; init; } = Array.Empty<FiscalTender>();
}

/// <summary>One ClubStack tender line, retained for audit alongside the fiscal document.</summary>
public sealed record FiscalTender(string PaymentMethodCode, string PaymentMethodName, string Kind, decimal Amount, string? ExternalReference);

/// <summary>Fiscal identifiers and verdict returned by the engine.</summary>
public sealed record FiscalResult
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? DeviceId { get; init; }

    public string? DeviceSerialNumber { get; init; }

    public string? FiscalDayNo { get; init; }

    public string? ReceiptGlobalNo { get; init; }

    public string? ReceiptCounter { get; init; }

    public string? ReceiptId { get; init; }

    public string? OperationId { get; init; }

    public string? QrUrl { get; init; }

    /// <summary>Device receipt signature/hash. Null when the engine did not return one.</summary>
    public string? DeviceSignature { get; init; }

    /// <summary>Server/FDMS signature once the document has been synchronised.</summary>
    public string? ServerSignature { get; init; }

    /// <summary>True when the engine accepted the document locally but has not synchronised it.</summary>
    public bool PendingSync { get; init; }

    /// <summary>Engine-side validation messages, preserved verbatim. Never treated as success.</summary>
    public IReadOnlyList<string> ValidationWarnings { get; init; } = Array.Empty<string>();

    /// <summary>How ClubStack tenders were represented to the engine, for the audit trail.</summary>
    public string? PaymentMapping { get; init; }
}

/// <summary>Device identity, licensing and connectivity as reported by the engine.</summary>
public sealed record FiscalDeviceStatus
{
    public bool Available { get; init; }

    public string? DeviceId { get; init; }

    public string? SerialNumber { get; init; }

    public string? CompanyName { get; init; }

    public string? TaxpayerName { get; init; }

    public string? TaxpayerTaxNumber { get; init; }

    public string? VatNumber { get; init; }

    public bool LicenseActive { get; init; }

    public string? LicenseStatus { get; init; }

    public DateTimeOffset? LicenseStart { get; init; }

    public DateTimeOffset? LicenseExpiry { get; init; }

    public int CertificateCount { get; init; }

    public FiscalDayState FiscalDayState { get; init; } = FiscalDayState.Unknown;

    public string? FiscalDayStatus { get; init; }

    public string? LastFiscalDayNo { get; init; }

    public string? LastReceiptGlobalNo { get; init; }

    public bool ZimraReachable { get; init; }

    public string? ZimraSource { get; init; }

    public string? LastError { get; init; }
}

/// <summary>Device configuration. Tax identifiers come from here so no rate is hard-coded.</summary>
public sealed record FiscalDeviceConfiguration
{
    public string? DeviceId { get; init; }

    public string? SerialNumber { get; init; }

    public string? CompanyName { get; init; }

    public string? BranchName { get; init; }

    public string? City { get; init; }

    /// <summary>Taxpayer identification number as reported by the device.</summary>
    public string? TaxpayerTaxNumber { get; init; }

    /// <summary>VAT registration number as reported by the device.</summary>
    public string? VatNumber { get; init; }

    public string? Environment { get; init; }

    public int StandardRatedTaxId { get; init; }

    public int ZeroRatedTaxId { get; init; }

    public int ExemptTaxId { get; init; }

    /// <summary>
    /// The tax brackets the device actually has configured, read back from it. A receipt
    /// must be priced with a rate and a tax id from this table: the device rejects any
    /// other combination before submission.
    /// </summary>
    public DeviceTaxBracket[] TaxBrackets { get; init; } = [];

    public string? CurrencyCode { get; init; }

    public string? ServerUrl { get; init; }

    /// <summary>Folder holding the generated certificate set. Required to apply device settings.</summary>
    public string? CertificateFolder { get; init; }

    /// <summary>
    /// ZIMRA activation key. Never persisted by ClubStack and never returned by a
    /// read operation; only accepted as write input.
    /// </summary>
    public string? ActivationKey { get; init; }
}

/// <summary>One tax bracket as reported by the device configuration.</summary>
public sealed record DeviceTaxBracket(string TaxId, string Name, decimal Rate);

/// <summary>
/// The endpoints a selected environment resolves to, as persisted on the device.
///
/// Recorded rather than recomputed so an operator can see exactly which hosts the
/// device will contact, and so the production-safety guard compares against what was
/// actually written rather than what was requested.
/// </summary>
public sealed record FiscalEnvironmentSelection
{
    public FiscalEnvironment Environment { get; init; }

    /// <summary>FDMS device/submit host the engine will call.</summary>
    public string ZimraServerUrl { get; init; } = string.Empty;

    /// <summary>Host used for receipt QR verification deep-links.</summary>
    public string VerificationServerUrl { get; init; } = string.Empty;

    /// <summary>Whether the persisted host is a known TEST host rather than production.</summary>
    public bool IsTestHost { get; init; }

    /// <summary>True when the engine has recorded an environment change needing re-registration.</summary>
    public bool EnvironmentTransitionPending { get; init; }
}

/// <summary>Certificate material for the provisioned device, as reported by the engine.</summary>
public sealed record FiscalDeviceCertificates
{
    public bool Generated { get; init; }

    public string? CertificateFolder { get; init; }

    public string? SerialNumber { get; init; }

    public string? DeviceId { get; init; }

    public string? Subject { get; init; }

    public string? Thumbprint { get; init; }

    public string? ValidTill { get; init; }

    public int CertificateCount { get; init; }
}

/// <summary>Outcome of a ZIMRA device registration performed through the engine.</summary>
public sealed record FiscalDeviceRegistration
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? OperationId { get; init; }

    public string? TaxpayerName { get; init; }

    public string? TaxpayerTaxNumber { get; init; }

    public string? VatNumber { get; init; }

    public string? SerialNumber { get; init; }

    public string? DeviceId { get; init; }

    public string? Subject { get; init; }

    public string? Thumbprint { get; init; }

    public string? ValidTill { get; init; }

    public string? CertificateFolder { get; init; }

    public string? OnlineStatus { get; init; }
}

/// <summary>
/// Resolves the device's numeric tax identifiers from its own configuration rather
/// than hard-coding them.
///
/// A registered device returns a coherent FDMS table. An unregistered device returns a
/// local snapshot whose brackets are not yet bound to FDMS (observed: two entries
/// named "Zero Rated" at different rates). Resolution therefore requires both a name
/// match and a rate that agrees with the treatment, and falls back to 0 - which tells
/// the engine to select the bracket itself - whenever the table is ambiguous.
/// </summary>
public static class TaxIds
{
    /// <summary>0 asks the engine to choose from its own configured table.</summary>
    public const int EngineSelects = 0;

/// <summary>
///     Standard-rated bracket: the highest positive rate in the table.
///
///     Brackets whose name declares a zero-rated or exempt treatment are excluded. Selecting
///     purely by rate is otherwise wrong on a real device: the development device's table
///     has exactly one positive rate, and that bracket is named "Zero Rated", so a
///     standard-rated line would be tagged with a zero-rated bracket. When no candidate
///     remains — or when the highest rate is claimed by two different identifiers — the
///     engine is asked to choose, which is this type's standing behaviour for a table that
///     does not describe itself unambiguously.
/// </summary>
public static int ResolveStandardRated(IReadOnlyList<DeviceTaxBracket> brackets)
{
    var candidates = brackets
        .Where(b => b.Rate > 0m && !IsZeroOrExemptName(b.Name))
        .ToArray();

    if (candidates.Length == 0)
    {
        return EngineSelects;
    }

    var highest = candidates.Max(b => b.Rate);
    var atHighest = candidates
        .Where(b => b.Rate == highest)
        .Select(b => Parse(b.TaxId))
        .Where(id => id > 0)
        .Distinct()
        .ToArray();

    return atHighest.Length == 1 ? atHighest[0] : EngineSelects;
}

/// <summary>Whether a bracket name declares a non-standard-rated treatment.</summary>
private static bool IsZeroOrExemptName(string name)
    => name.Contains("zero", StringComparison.OrdinalIgnoreCase)
       || name.Contains("exempt", StringComparison.OrdinalIgnoreCase);

/// <summary>Zero-rated bracket: rate of zero and a name indicating zero rating.</summary>
public static int ResolveZeroRated(IReadOnlyList<DeviceTaxBracket> brackets)
    {
        var matches = brackets
            .Where(b => b.Rate == 0m && b.Name.Contains("zero", StringComparison.OrdinalIgnoreCase))
            .Select(b => Parse(b.TaxId))
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        // More than one candidate means the table disagrees with itself; let the engine decide.
        return matches.Length == 1 ? matches[0] : EngineSelects;
    }

    /// <summary>Exempt bracket: rate of zero and a name indicating exemption.</summary>
    public static int ResolveExempt(IReadOnlyList<DeviceTaxBracket> brackets)
    {
        var matches = brackets
            .Where(b => b.Rate == 0m && b.Name.Contains("exempt", StringComparison.OrdinalIgnoreCase))
            .Select(b => Parse(b.TaxId))
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        return matches.Length == 1 ? matches[0] : EngineSelects;
    }

    private static int Parse(string taxId)
        => int.TryParse(taxId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
}

/// <summary>
/// Abstraction over the ZIMRA fiscal device (D5.5). Controllers and the Domain
/// depend on this seam only; the FiscalStackZW.dll implementation lives in
/// Infrastructure. Every member is asynchronous and must be awaited off the
/// request thread because the engine is synchronous.
/// </summary>
public interface IFiscalisationGateway
{
    Task<FiscalDeviceStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<FiscalDeviceConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a license key through the engine's own public, license-enforcing
    /// entry point. The engine persists it; ClubStack never stores the key.
    /// </summary>
    Task<bool> SetLicenseAsync(string licenseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies device settings (device id, serial, activation key, certificate
    /// folder). Requires an existing certificate folder; the engine refuses
    /// otherwise.
    /// </summary>
    Task UpdateConfigurationAsync(FiscalDeviceConfiguration configuration, CancellationToken cancellationToken = default);

    Task<FiscalResult> OpenFiscalDayAsync(CancellationToken cancellationToken = default);

    Task<FiscalResult> CloseFiscalDayAsync(CancellationToken cancellationToken = default);

    Task<FiscalResult> FiscaliseAsync(FiscalDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the engine to submit its own queued offline documents. Returns the
    /// engine's raw report; the engine owns the durable queue.
    /// </summary>
    Task<string> SubmitPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Number of documents the engine still holds offline, when it can report it.</summary>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Binds the device to one FDMS environment and persists the resolved hosts.
    ///
    /// The engine derives its environment from a single stored value, so this is the
    /// only place that decides which hosts a device may use. Selecting an environment
    /// recycles the engine so the change takes effect, and reports an environment
    /// transition when the device was previously bound to the other one - certificates
    /// from one environment are not valid in the other.
    /// </summary>
    Task<FiscalEnvironmentSelection> SelectEnvironmentAsync(
        FiscalEnvironment environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the cached engine instance so the next call re-reads persisted device
    /// configuration.
    ///
    /// Required after any change to the device's identity or environment: the engine
    /// captures the serial and license state when it is constructed, so a
    /// registration that rewrote the serial would otherwise be validated against the
    /// previous one.
    /// </summary>
    Task<FiscalDeviceStatus> RecycleDeviceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a local certificate set for a device that has not yet been registered.
    ///
    /// Not a substitute for <see cref="RegisterDeviceAsync"/>: this path produces a
    /// self-signed set locally, whereas registration performs the real FDMS
    /// taxpayer verification and returns a server-signed certificate.
    /// </summary>
    Task<FiscalDeviceCertificates> GenerateDeviceCertificatesAsync(
        string serialNumber,
        string activationKey,
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the device with ZIMRA and installs the returned certificate material.
    ///
    /// The engine performs taxpayer verification, CSR generation, device registration,
    /// certificate import, configuration retrieval and server-certificate download in
    /// one operation, and writes the device's own certificate manifest as a result.
    /// </summary>
    Task<FiscalDeviceRegistration> RegisterDeviceAsync(
        string activationKey,
        string serialNumber,
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the certificate material currently installed for the device.</summary>
    Task<FiscalDeviceCertificates> GetCertificateDetailsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails before any fiscal network call when the device's effective endpoint does
    /// not match its selected environment.
    ///
    /// A test device must never reach production, and a device provisioned as a test
    /// device must not be operated as production. This is the single enforcement point
    /// for both directions.
    /// </summary>
    Task EnsureEnvironmentSafetyAsync(CancellationToken cancellationToken = default);
}
