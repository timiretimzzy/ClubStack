namespace ClubStack.Api.Contracts;

// ---- Fiscalisation (D5.5) ----

public sealed record FiscalDeviceStatusResponse
{
    public bool Available { get; init; }
    public string? DeviceId { get; init; }
    public string? SerialNumber { get; init; }
    public string? CompanyName { get; init; }
    public string? TaxpayerName { get; init; }
    public string? TaxpayerTaxNumber { get; init; }
    public string? VatNumber { get; init; }

    /// <summary>True when the engine reports an active license. Never carries key material.</summary>
    public bool LicenseActive { get; init; }

    public string? LicenseStatus { get; init; }
    public DateTimeOffset? LicenseStart { get; init; }
    public DateTimeOffset? LicenseExpiry { get; init; }
    public int CertificateCount { get; init; }
    public string FiscalDayState { get; init; } = string.Empty;
    public string? FiscalDayStatus { get; init; }
    public string? LastFiscalDayNo { get; init; }
    public string? LastReceiptGlobalNo { get; init; }
    public bool ZimraReachable { get; init; }
    public string? ZimraSource { get; init; }
    public string? LastError { get; init; }
}

public sealed record FiscalDeviceConfigurationResponse
{
    public string? DeviceId { get; init; }
    public string? SerialNumber { get; init; }
    public string? CompanyName { get; init; }
    public string? BranchName { get; init; }
    public string? City { get; init; }
    public string? Environment { get; init; }

    /// <summary>Tax identifiers read from the device, never hard-coded by ClubStack.</summary>
    public int StandardRatedTaxId { get; init; }

    public int ZeroRatedTaxId { get; init; }
    public int ExemptTaxId { get; init; }
    public string? CurrencyCode { get; init; }
    public string? ServerUrl { get; init; }

    /// <summary>
    /// Certificate folder reported by the device. FiscalStackZW does not report this on
    /// reads, so it is normally null. The activation key is never returned by any read
    /// route.
    /// </summary>
    public string? CertificateFolder { get; init; }
}

public sealed record FiscalDayStatusResponse
{
    public string State { get; init; } = string.Empty;
    public string? FiscalDayStatus { get; init; }
    public string? LastFiscalDayNo { get; init; }
    public string? LastReceiptGlobalNo { get; init; }
    public bool ZimraReachable { get; init; }
    public string? ZimraSource { get; init; }
}

public sealed record FiscalDayActionResponse
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? FiscalDayNo { get; init; }
    public string? OperationId { get; init; }
    public FiscalDayStatusResponse? Status { get; init; }
}

public sealed record FiscalisationTenderResponse
{
    public string PaymentMethodCode { get; init; } = string.Empty;
    public string PaymentMethodName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? ExternalReference { get; init; }
}

public sealed record FiscalisationResponse
{
    public Guid Id { get; init; }
    public Guid SaleId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public Guid? RefundId { get; init; }
    public string? RefundNumber { get; init; }
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

    /// <summary>How the ClubStack tender breakdown was represented to the device.</summary>
    public string? PaymentMapping { get; init; }

    public IReadOnlyList<FiscalisationTenderResponse> Tenders { get; init; } = Array.Empty<FiscalisationTenderResponse>();
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

public sealed record FiscalisationSearchResponse
{
    public IReadOnlyList<FiscalisationResponse> Items { get; init; } = Array.Empty<FiscalisationResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record FiscalisationRetryResponse
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public FiscalisationResponse? Fiscalisation { get; init; }
}

public sealed record FiscalisationSubmitPendingResponse
{
    /// <summary>Documents the engine still holds offline after the flush attempt.</summary>
    public int StillPending { get; init; }
}

/// <summary>
/// Installs a license key. The key is write-only: it is forwarded to FiscalStackZW
/// and is never echoed back in any response.
/// </summary>
public sealed record FiscalDeviceLicenseRequest
{
    public string LicenseKey { get; init; } = string.Empty;
}

public sealed record FiscalDeviceConfigurationRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;

    /// <summary>Write-only. Forwarded to the engine and never stored or returned.</summary>
    public string ActivationKey { get; init; } = string.Empty;

    public string CertificateFolder { get; init; } = string.Empty;
}

public sealed record FiscalDeviceActionResponse
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public FiscalDeviceStatusResponse? Status { get; init; }
}

/// <summary>
/// Takes a device from its current state to registered, licensed and bound to a named
/// FDMS environment. Both keys are write-only: they are forwarded to FiscalStackZW and
/// are never persisted, logged, or returned in any response.
/// </summary>
public sealed record FiscalDeviceProvisioningRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>One of <c>Production</c> or <c>Test</c>.</summary>
    public string Environment { get; init; } = string.Empty;

    /// <summary>Write-only. Used only for registration.</summary>
    public string ActivationKey { get; init; } = string.Empty;

    /// <summary>Write-only. Applied after the engine is recycled for the new identity.</summary>
    public string LicenseKey { get; init; } = string.Empty;

    /// <summary>
    /// Issues a local self-signed certificate set before registering. Off by default:
    /// registration generates its own keys and imports a server-signed certificate.
    /// </summary>
    public bool GenerateLocalCertificatesFirst { get; init; }
}

public sealed record FiscalDeviceProvisioningResponse
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string? ZimraServerUrl { get; init; }
    public bool IsTestHost { get; init; }
    public bool EnvironmentSelected { get; init; }
    public bool Registered { get; init; }
    public bool LicenseApplied { get; init; }
    public string? OperationId { get; init; }
    public string? TaxpayerName { get; init; }
    public string? TaxpayerTaxNumber { get; init; }
    public int CertificateCount { get; init; }
    public string? CertificateFolder { get; init; }
    public FiscalDeviceStatusResponse? Status { get; init; }

    /// <summary>Ordered steps performed, so a failure identifies the stage that stopped.</summary>
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
}
