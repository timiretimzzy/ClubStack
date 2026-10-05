using ClubStack.Api.Contracts;
using ClubStack.Application.Authorization;
using ClubStack.Application.Fiscalization;
using ClubStack.Contracts.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubStack.Api.Controllers;

/// <summary>
/// ZIMRA fiscalisation (D5.5).
///
/// Every route is permission-gated. Device management, fiscal day lifecycle and retry
/// are sensitive operations and are audited; the raw license key and activation key are
/// never returned by any read route.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/fiscalisation")]
[Authorize]
public sealed class FiscalisationController : ControllerBase
{
    private readonly IFiscalisationService _fiscalisation;

    public FiscalisationController(IFiscalisationService fiscalisation) => _fiscalisation = fiscalisation;

    // ---- Device (Device.View / Device.Manage) ----

    [HttpGet("device/status")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDeviceView)]
    [ProducesResponseType(typeof(FiscalDeviceStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeviceStatus(CancellationToken cancellationToken)
        => Ok(Map(await _fiscalisation.GetDeviceStatusAsync(cancellationToken)));

    [HttpGet("device/configuration")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDeviceView)]
    [ProducesResponseType(typeof(FiscalDeviceConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeviceConfiguration(CancellationToken cancellationToken)
    {
        var configuration = await _fiscalisation.GetDeviceConfigurationAsync(cancellationToken);
        return Ok(new FiscalDeviceConfigurationResponse
        {
            DeviceId = configuration.DeviceId,
            SerialNumber = configuration.SerialNumber,
            CompanyName = configuration.CompanyName,
            BranchName = configuration.BranchName,
            City = configuration.City,
            Environment = configuration.Environment,
            StandardRatedTaxId = configuration.StandardRatedTaxId,
            ZeroRatedTaxId = configuration.ZeroRatedTaxId,
            ExemptTaxId = configuration.ExemptTaxId,
            CurrencyCode = configuration.CurrencyCode,
            ServerUrl = configuration.ServerUrl,
            CertificateFolder = configuration.CertificateFolder,
        });
    }

    // ---- Device administration (Device.Manage, sensitive, audited) ----

    [HttpPost("device/license")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDeviceManage)]
    [ProducesResponseType(typeof(FiscalDeviceActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetLicense(
        [FromBody] FiscalDeviceLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _fiscalisation.SetLicenseAsync(
            request.LicenseKey,
            CurrentUserId(),
            cancellationToken);

        return Ok(new FiscalDeviceActionResponse
        {
            Success = result.Success,
            Code = result.Code,
            Message = result.Message,
            Status = result.Status is null ? null : Map(result.Status),
        });
    }

    [HttpPost("device/configuration")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDeviceManage)]
    [ProducesResponseType(typeof(FiscalDeviceActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateConfiguration(
        [FromBody] FiscalDeviceConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _fiscalisation.UpdateConfigurationAsync(
            new FiscalDeviceConfigurationUpdate(
                request.DeviceId,
                request.SerialNumber,
                request.CompanyName,
                request.BranchName,
                request.City,
                request.ActivationKey,
                request.CertificateFolder),
            CurrentUserId(),
            cancellationToken);

        return Ok(new FiscalDeviceActionResponse
        {
            Success = result.Success,
            Code = result.Code,
            Message = result.Message,
            Status = result.Status is null ? null : Map(result.Status),
        });
    }

    [HttpPost("device/provision")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDeviceManage)]
    [ProducesResponseType(typeof(FiscalDeviceProvisioningResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProvisionDevice(
        [FromBody] Contracts.FiscalDeviceProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ClubStack.Hardware.Fiscalization.FiscalEnvironment>(request.Environment, ignoreCase: true, out var environment))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid fiscal environment.",
                Detail = $"'{request.Environment}' is not a known FDMS environment. Use 'Production' or 'Test'.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var result = await _fiscalisation.ProvisionDeviceAsync(
            new Application.Fiscalization.FiscalDeviceProvisioningRequest(
                request.DeviceId,
                request.SerialNumber,
                environment,
                request.ActivationKey,
                request.LicenseKey,
                request.GenerateLocalCertificatesFirst),
            CurrentUserId(),
            cancellationToken);

        return Ok(new FiscalDeviceProvisioningResponse
        {
            Success = result.Success,
            Code = result.Code,
            Message = result.Message,
            Environment = result.Environment,
            ZimraServerUrl = result.ZimraServerUrl,
            IsTestHost = result.IsTestHost,
            EnvironmentSelected = result.EnvironmentSelected,
            Registered = result.Registered,
            LicenseApplied = result.LicenseApplied,
            OperationId = result.OperationId,
            TaxpayerName = result.TaxpayerName,
            TaxpayerTaxNumber = result.TaxpayerTaxNumber,
            CertificateCount = result.CertificateCount,
            CertificateFolder = result.CertificateFolder,
            Status = result.Status is null ? null : Map(result.Status),
            Steps = result.Steps,
        });
    }

    // ---- Fiscal day (Day.Open / Day.Close) ----

    [HttpGet("day")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDeviceView)]
    [ProducesResponseType(typeof(FiscalDayStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDayStatus(CancellationToken cancellationToken)
    {
        var status = await _fiscalisation.GetFiscalDayStatusAsync(cancellationToken);
        return Ok(new FiscalDayStatusResponse
        {
            State = status.State,
            FiscalDayStatus = status.FiscalDayStatus,
            LastFiscalDayNo = status.LastFiscalDayNo,
            LastReceiptGlobalNo = status.LastReceiptGlobalNo,
            ZimraReachable = status.ZimraReachable,
            ZimraSource = status.ZimraSource,
        });
    }

    [HttpPost("day/open")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDayOpen)]
    [ProducesResponseType(typeof(FiscalDayActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OpenDay(CancellationToken cancellationToken)
    {
        var result = await _fiscalisation.OpenFiscalDayAsync(CurrentUserId(), cancellationToken);
        return Ok(Map(result));
    }

    [HttpPost("day/close")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationDayClose)]
    [ProducesResponseType(typeof(FiscalDayActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CloseDay(CancellationToken cancellationToken)
    {
        var result = await _fiscalisation.CloseFiscalDayAsync(CurrentUserId(), cancellationToken);
        return Ok(Map(result));
    }

    // ---- Documents (Transaction.View / Transaction.Retry) ----

    [HttpGet("documents")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationTransactionView)]
    [ProducesResponseType(typeof(FiscalisationSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? status,
        [FromQuery] Guid? saleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _fiscalisation.SearchAsync(
            new FiscalisationSearchInput(status, saleId, page, pageSize),
            cancellationToken);

        return Ok(new FiscalisationSearchResponse
        {
            Items = result.Items.Select(Map).ToArray(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("documents/by-sale/{saleId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationTransactionView)]
    [ProducesResponseType(typeof(FiscalisationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForSale(Guid saleId, CancellationToken cancellationToken)
    {
        var record = await _fiscalisation.GetForSaleAsync(saleId, cancellationToken);
        return record is null ? NotFound() : Ok(Map(record));
    }

    [HttpPost("documents/{id:guid}/retry")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationTransactionRetry)]
    [ProducesResponseType(typeof(FiscalisationRetryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var result = await _fiscalisation.RetryAsync(id, CurrentUserId(), cancellationToken);
        if (string.Equals(result.Code, "not-found", StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(new FiscalisationRetryResponse
        {
            Success = result.Success,
            Code = result.Code,
            Message = result.Message,
            Fiscalisation = result.Fiscalisation is null ? null : Map(result.Fiscalisation),
        });
    }

    [HttpPost("documents/submit-pending")]
    [Authorize(Policy = "Permission:" + Permissions.FiscalizationTransactionRetry)]
    [ProducesResponseType(typeof(FiscalisationSubmitPendingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitPending(CancellationToken cancellationToken)
    {
        var stillPending = await _fiscalisation.SubmitPendingAsync(CurrentUserId(), cancellationToken);
        return Ok(new FiscalisationSubmitPendingResponse { StillPending = stillPending });
    }

    private static FiscalDeviceStatusResponse Map(FiscalDeviceStatusDto status) => new()
    {
        Available = status.Available,
        DeviceId = status.DeviceId,
        SerialNumber = status.SerialNumber,
        CompanyName = status.CompanyName,
        TaxpayerName = status.TaxpayerName,
        TaxpayerTaxNumber = status.TaxpayerTaxNumber,
        VatNumber = status.VatNumber,
        LicenseActive = status.LicenseActive,
        LicenseStatus = status.LicenseStatus,
        LicenseStart = status.LicenseStart,
        LicenseExpiry = status.LicenseExpiry,
        CertificateCount = status.CertificateCount,
        FiscalDayState = status.FiscalDayState,
        FiscalDayStatus = status.FiscalDayStatus,
        LastFiscalDayNo = status.LastFiscalDayNo,
        LastReceiptGlobalNo = status.LastReceiptGlobalNo,
        ZimraReachable = status.ZimraReachable,
        ZimraSource = status.ZimraSource,
        LastError = status.LastError,
    };

    private static FiscalDayActionResponse Map(FiscalDayActionResult result) => new()
    {
        Success = result.Success,
        Code = result.Code,
        Message = result.Message,
        FiscalDayNo = result.FiscalDayNo,
        OperationId = result.OperationId,
        Status = result.Status is null
            ? null
            : new FiscalDayStatusResponse
            {
                State = result.Status.State,
                FiscalDayStatus = result.Status.FiscalDayStatus,
                LastFiscalDayNo = result.Status.LastFiscalDayNo,
                LastReceiptGlobalNo = result.Status.LastReceiptGlobalNo,
                ZimraReachable = result.Status.ZimraReachable,
                ZimraSource = result.Status.ZimraSource,
            },
    };

    private static FiscalisationResponse Map(FiscalisationDto dto) => new()
    {
        Id = dto.Id,
        SaleId = dto.SaleId,
        SaleNumber = dto.SaleNumber,
        RefundId = dto.RefundId,
        RefundNumber = dto.RefundNumber,
        InvoiceNumber = dto.InvoiceNumber,
        DocumentKind = dto.DocumentKind,
        Status = dto.Status,
        DeviceId = dto.DeviceId,
        DeviceSerialNumber = dto.DeviceSerialNumber,
        FiscalDayNo = dto.FiscalDayNo,
        ReceiptCounter = dto.ReceiptCounter,
        ReceiptGlobalNo = dto.ReceiptGlobalNo,
        ReceiptId = dto.ReceiptId,
        OperationId = dto.OperationId,
        DeviceSignature = dto.DeviceSignature,
        ServerSignature = dto.ServerSignature,
        QrUrl = dto.QrUrl,
        OriginalInvoiceNumber = dto.OriginalInvoiceNumber,
        PaymentMapping = dto.PaymentMapping,
        Tenders = dto.Tenders.Select(t => new FiscalisationTenderResponse
        {
            PaymentMethodCode = t.PaymentMethodCode,
            PaymentMethodName = t.PaymentMethodName,
            Kind = t.Kind,
            Amount = t.Amount,
            ExternalReference = t.ExternalReference,
        }).ToArray(),
        Code = dto.Code,
        Message = dto.Message,
        ValidationWarnings = dto.ValidationWarnings,
        PendingSync = dto.PendingSync,
        AttemptCount = dto.AttemptCount,
        SubmittedAtUtc = dto.SubmittedAtUtc,
        FiscalisedAtUtc = dto.FiscalisedAtUtc,
        LastAttemptAtUtc = dto.LastAttemptAtUtc,
        CreatedAtUtc = dto.CreatedAtUtc,
        UpdatedAtUtc = dto.UpdatedAtUtc,
    };

    private string CurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";
}
