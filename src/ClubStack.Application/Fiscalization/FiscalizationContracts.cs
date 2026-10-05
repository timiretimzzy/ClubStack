using ClubStack.Hardware.Fiscalization;

namespace ClubStack.Application.Fiscalization;

/// <summary>Persisted fiscalisation record for one sale or refund (D5.5).</summary>
public sealed record FiscalisationDto
{
    public Guid Id { get; init; }

    public Guid SaleId { get; init; }

    public string SaleNumber { get; init; } = string.Empty;

    public Guid? RefundId { get; init; }

    public string? RefundNumber { get; init; }

    /// <summary>Invoice number presented to the fiscal device; the ClubStack sale number.</summary>
    public string InvoiceNumber { get; init; } = string.Empty;

    public string DocumentKind { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? DeviceId { get; init; }

    public string? DeviceSerialNumber { get; init; }

    public string? FiscalDayNo { get; init; }

    public string? ReceiptCounter { get; init; }

    public string? ReceiptGlobalNo { get; init; }

    public string? ReceiptId { get; init; }

    public string? OperationId { get; init; }

    public string? DeviceSignature { get; init; }

    public string? ServerSignature { get; init; }

    public string? QrUrl { get; init; }

    public string? OriginalInvoiceNumber { get; init; }

    /// <summary>How the ClubStack tender breakdown was represented to the engine.</summary>
    public string? PaymentMapping { get; init; }

    /// <summary>Cluster of tenders ClubStack recorded, preserved for audit.</summary>
    public IReadOnlyList<FiscalTender> Tenders { get; init; } = Array.Empty<FiscalTender>();

    public string? Code { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<string> ValidationWarnings { get; init; } = Array.Empty<string>();

    public bool PendingSync { get; init; }

    public int AttemptCount { get; init; }

    public DateTimeOffset? SubmittedAtUtc { get; init; }

    public DateTimeOffset? FiscalisedAtUtc { get; init; }

    public DateTimeOffset? LastAttemptAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record FiscalisationSearchInput(
    string? Status,
    Guid? SaleId,
    int Page = 1,
    int PageSize = 50);

public sealed record FiscalisationSearchResult(
    IReadOnlyList<FiscalisationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record FiscalDayStatusDto(
    string State,
    string? FiscalDayStatus,
    string? LastFiscalDayNo,
    string? LastReceiptGlobalNo,
    bool ZimraReachable,
    string? ZimraSource);

public sealed record FiscalDeviceStatusDto(
    bool Available,
    string? DeviceId,
    string? SerialNumber,
    string? CompanyName,
    string? TaxpayerName,
    string? TaxpayerTaxNumber,
    string? VatNumber,
    bool LicenseActive,
    string? LicenseStatus,
    DateTimeOffset? LicenseStart,
    DateTimeOffset? LicenseExpiry,
    int CertificateCount,
    string FiscalDayState,
    string? FiscalDayStatus,
    string? LastFiscalDayNo,
    string? LastReceiptGlobalNo,
    bool ZimraReachable,
    string? ZimraSource,
    string? LastError);

public sealed record FiscalDeviceConfigurationDto(
    string? DeviceId,
    string? SerialNumber,
    string? CompanyName,
    string? BranchName,
    string? City,
    string? Environment,
    int StandardRatedTaxId,
    int ZeroRatedTaxId,
    int ExemptTaxId,
    string? CurrencyCode,
    string? ServerUrl,
    /// <summary>
    /// Certificate folder reported by the device, if any. FiscalStackZW does not report
    /// this on reads, so this is normally null and the operator supplies the path when
    /// applying settings. The activation key is write-only and never returned.
    /// </summary>
    string? CertificateFolder);

public sealed record FiscalDayActionResult(
    bool Success,
    string Code,
    string Message,
    string? FiscalDayNo,
    string? OperationId,
    FiscalDayStatusDto? Status);

public sealed record FiscalisationRetryResult(
    bool Success,
    string Code,
    string Message,
    FiscalisationDto? Fiscalisation);

/// <summary>
/// Result of a sensitive device administration call. Deliberately carries no key
/// material: only whether the engine accepted the operation.
/// </summary>
public sealed record FiscalDeviceActionResult(
    bool Success,
    string Code,
    string Message,
    FiscalDeviceStatusDto? Status);

/// <summary>
/// Device settings to apply through the engine. The activation key is write-only -
/// it is forwarded to FiscalStackZW and never persisted, logged, or returned.
/// </summary>
public sealed record FiscalDeviceConfigurationUpdate(
    string DeviceId,
    string SerialNumber,
    string CompanyName,
    string BranchName,
    string City,
    string ActivationKey,
    string CertificateFolder);

/// <summary>
/// A request to take a virtual fiscal device from whatever state it is in to a
/// registered, licensed device in a named environment.
///
/// Both secrets are write-only: they reach the engine and nothing else.
/// </summary>
public sealed record FiscalDeviceProvisioningRequest(
    string DeviceId,
    string SerialNumber,
    FiscalEnvironment Environment,
    string ActivationKey,
    string LicenseKey,
    /// <summary>
    /// Issues a local self-signed certificate set before registering. Off by default:
    /// registration performs its own key generation and imports a server-signed
    /// certificate, so a local set is neither required nor wanted for a device that is
    /// about to be registered against FDMS.
    /// </summary>
    bool GenerateLocalCertificatesFirst = false);

/// <summary>
/// Outcome of provisioning a device, including the ordered steps that were performed so
/// an operator can see where a failure occurred. Carries no key material.
/// </summary>
public sealed record FiscalDeviceProvisioningResult(
    bool Success,
    string Code,
    string Message,
    string Environment,
    string? ZimraServerUrl,
    bool IsTestHost,
    bool EnvironmentSelected,
    bool Registered,
    bool LicenseApplied,
    string? OperationId,
    string? TaxpayerName,
    string? TaxpayerTaxNumber,
    int CertificateCount,
    string? CertificateFolder,
    FiscalDeviceStatusDto? Status,
    IReadOnlyList<string> Steps);

/// <summary>
/// Business-level fiscalisation capability (D5.5). Exposes ClubStack operations,
/// not FiscalStackZW.dll methods.
/// </summary>
public interface IFiscalisationService
{
    Task<FiscalDeviceStatusDto> GetDeviceStatusAsync(CancellationToken cancellationToken = default);

    Task<FiscalDeviceConfigurationDto> GetDeviceConfigurationAsync(CancellationToken cancellationToken = default);

    Task<FiscalDayStatusDto> GetFiscalDayStatusAsync(CancellationToken cancellationToken = default);

    Task<FiscalDayActionResult> OpenFiscalDayAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task<FiscalDayActionResult> CloseFiscalDayAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task<FiscalisationDto?> GetForSaleAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<FiscalisationSearchResult> SearchAsync(FiscalisationSearchInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-attempts a fiscalisation that did not reach <c>Fiscalised</c>. Safe to call
    /// repeatedly: a document that already succeeded is returned unchanged.
    /// </summary>
    Task<FiscalisationRetryResult> RetryAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Asks the engine to flush its own offline queue.</summary>
    Task<int> SubmitPendingAsync(string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a license key through the engine's own license-enforcing entry point.
    /// The key is passed straight through and is never stored by ClubStack.
    /// </summary>
    Task<FiscalDeviceActionResult> SetLicenseAsync(
        string licenseKey,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies device settings (identity, activation key, certificate folder) through
    /// the engine. The activation key is write-only.
    /// </summary>
    Task<FiscalDeviceActionResult> UpdateConfigurationAsync(
        FiscalDeviceConfigurationUpdate update,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a device end to end through the engine: binds it to an environment,
    /// registers it with ZIMRA so real certificate material is installed, then installs
    /// the device-bound license.
    ///
    /// The order is deliberate. The environment is selected first so registration never
    /// contacts the wrong FDMS host. The license is applied last because the engine
    /// validates it against the serial captured when the engine was constructed, which
    /// registration changes; the engine instance is therefore recycled in between.
    ///
    /// Repeatable: given the same inputs it drives the same device to the same state,
    /// without an operator editing device configuration by hand.
    /// </summary>
    Task<FiscalDeviceProvisioningResult> ProvisionDeviceAsync(
        FiscalDeviceProvisioningRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
