using System.Text.Json;
using ClubStack.Application.Auditing;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Fiscalization;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Business-level fiscalisation operations for the D5.5 API.
///
/// This is the only place that decides ClubStack's fiscalisation state. Device
/// identifiers returned by the engine are recorded; they are never inferred, and a
/// completed sale is never reported as fiscalised.
/// </summary>
public sealed class FiscalisationService : IFiscalisationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string AuditEntity = "Fiscalisation";
    private const string FiscalisedState = nameof(FiscalisationState.Fiscalised);

    private readonly ClubStackDbContext _context;
    private readonly IFiscalisationGateway _gateway;
    private readonly IAuditRecorder _audit;
    private readonly ILogger<FiscalisationService> _logger;

    public FiscalisationService(
        ClubStackDbContext context,
        IFiscalisationGateway gateway,
        IAuditRecorder audit,
        ILogger<FiscalisationService> logger)
    {
        _context = context;
        _gateway = gateway;
        _audit = audit;
        _logger = logger;
    }

    public async Task<FiscalDeviceStatusDto> GetDeviceStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _gateway.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            return ToDto(status);
        }
        catch (FiscalisationException ex)
        {
            _logger.LogWarning(ex, "Fiscal device status could not be read");
            return new FiscalDeviceStatusDto(
                Available: false,
                DeviceId: null,
                SerialNumber: null,
                CompanyName: null,
                TaxpayerName: null,
                TaxpayerTaxNumber: null,
                VatNumber: null,
                LicenseActive: false,
                LicenseStatus: null,
                LicenseStart: null,
                LicenseExpiry: null,
                CertificateCount: 0,
                FiscalDayState: nameof(FiscalDayState.Unknown),
                FiscalDayStatus: null,
                LastFiscalDayNo: null,
                LastReceiptGlobalNo: null,
                ZimraReachable: false,
                ZimraSource: null,
                LastError: ex.Message);
        }
    }

    public async Task<FiscalDeviceConfigurationDto> GetDeviceConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await _gateway.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        return new FiscalDeviceConfigurationDto(
            configuration.DeviceId,
            configuration.SerialNumber,
            configuration.CompanyName,
            configuration.BranchName,
            configuration.City,
            configuration.Environment,
            configuration.StandardRatedTaxId,
            configuration.ZeroRatedTaxId,
            configuration.ExemptTaxId,
            configuration.CurrencyCode,
            configuration.ServerUrl,
            configuration.CertificateFolder);
    }

    public async Task<FiscalDayStatusDto> GetFiscalDayStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await _gateway.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new FiscalDayStatusDto(
            status.FiscalDayState.ToString(),
            status.FiscalDayStatus,
            status.LastFiscalDayNo,
            status.LastReceiptGlobalNo,
            status.ZimraReachable,
            status.ZimraSource);
    }

    public async Task<FiscalDayActionResult> OpenFiscalDayAsync(string actorUserId, CancellationToken cancellationToken = default)
    {
        var result = await _gateway.OpenFiscalDayAsync(cancellationToken).ConfigureAwait(false);
        await RecordAuditAsync(actorUserId, "Fiscalisation.Day.Open", "day-open", result.Success, null, cancellationToken);
        var status = await ReadDayStatusSafeAsync(cancellationToken).ConfigureAwait(false);
        return new FiscalDayActionResult(result.Success, result.Code, result.Message, result.FiscalDayNo, result.OperationId, status);
    }

    public async Task<FiscalDayActionResult> CloseFiscalDayAsync(string actorUserId, CancellationToken cancellationToken = default)
    {
        var result = await _gateway.CloseFiscalDayAsync(cancellationToken).ConfigureAwait(false);
        await RecordAuditAsync(actorUserId, "Fiscalisation.Day.Close", "day-close", result.Success, null, cancellationToken);
        var status = await ReadDayStatusSafeAsync(cancellationToken).ConfigureAwait(false);
        return new FiscalDayActionResult(result.Success, result.Code, result.Message, result.FiscalDayNo, result.OperationId, status);
    }

    public async Task<FiscalisationDto?> GetForSaleAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var records = await _context.Fiscalisations
            .AsNoTracking()
            .Where(f => f.SaleId == saleId)
            .OrderBy(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Count == 0 ? null : ToDto(records[0]);
    }

    public async Task<FiscalisationSearchResult> SearchAsync(FiscalisationSearchInput input, CancellationToken cancellationToken = default)
    {
        var page = input.Page <= 0 ? 1 : input.Page;
        var pageSize = Math.Clamp(input.PageSize, 1, 200);

        var query = _context.Fiscalisations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            query = query.Where(f => f.Status == input.Status);
        }

        if (input.SaleId is { } saleId)
        {
            query = query.Where(f => f.SaleId == saleId);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new FiscalisationSearchResult(items.Select(ToDto).ToArray(), total, page, pageSize);
    }

    public async Task<FiscalisationRetryResult> RetryAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var record = await _context.Fiscalisations
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (record is null)
        {
            return new FiscalisationRetryResult(false, "not-found", "The fiscalisation record does not exist.", null);
        }

        if (string.Equals(record.Status, FiscalisedState, StringComparison.Ordinal))
        {
            // Idempotent: an already-fiscalised document is never sent again.
            return new FiscalisationRetryResult(true, "already-fiscalised", "The document is already fiscalised.", ToDto(record));
        }

        var previousStatus = record.Status;
        var previousMessage = record.Message;

        try
        {
            if (EngineHoldsDocument(record))
            {
                // The device already accepted this document, so the engine's own queue is
                // flushed rather than the same invoice number being transacted again.
                var stillPending = await _gateway.SubmitPendingAsync(cancellationToken).ConfigureAwait(false);
                record.PendingSync = !string.Equals(stillPending, "0", StringComparison.Ordinal);
                record.Status = record.PendingSync
                    ? nameof(FiscalisationState.PendingRetry)
                    : FiscalisedState;
                if (!record.PendingSync)
                {
                    record.FiscalisedAtUtc = DateTimeOffset.UtcNow;
                }

                record.Code = "engine-queue-flushed";
                record.Message = $"Engine offline queue flushed; documents still pending: {stillPending}.";
            }
            else
            {
                await RetryAsync(record, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (FiscalisationException ex)
        {
            record.Status = nameof(FiscalisationState.PendingRetry);
            record.Code = "retry-failed";
            record.Message = ex.Message;
            record.ErrorInfo = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await RecordAuditAsync(actorUserId, "Fiscalisation.Transaction.Retry", id.ToString(), false, new { error = ex.Message }, cancellationToken);
            return new FiscalisationRetryResult(false, "retry-failed", ex.Message, ToDto(record));
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await RecordAuditAsync(
            actorUserId,
            "Fiscalisation.Transaction.Retry",
            id.ToString(),
            record.Status == FiscalisedState,
            new { previousStatus, previousMessage, record.Status, record.ReceiptGlobalNo },
            cancellationToken);

        return new FiscalisationRetryResult(
            record.Status == FiscalisedState,
            record.Code ?? "retried",
            record.Message ?? "The retry completed.",
            ToDto(record));
    }

    public async Task<int> SubmitPendingAsync(string actorUserId, CancellationToken cancellationToken = default)
    {
        var pending = await _gateway.GetPendingCountAsync(cancellationToken).ConfigureAwait(false);
        await _gateway.SubmitPendingAsync(cancellationToken).ConfigureAwait(false);
        var remaining = await _gateway.GetPendingCountAsync(cancellationToken).ConfigureAwait(false);
        await RecordAuditAsync(
            actorUserId,
            "Fiscalisation.Transaction.SubmitPending",
            "engine-queue",
            remaining == 0,
            new { before = pending, after = remaining },
            cancellationToken);
        return remaining;
    }

    public async Task<FiscalDeviceActionResult> SetLicenseAsync(
        string licenseKey,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ValidationException(
                "The value is invalid.",
                new Dictionary<string, string[]>
                {
                    ["licenseKey"] = ["A license key is required."],
                });
        }

        // The key itself is never logged, audited, or persisted: only the outcome is.
        var installed = await _gateway.SetLicenseAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        await RecordAuditAsync(
            actorUserId,
            "Fiscalisation.Device.SetLicense",
            "device-license",
            installed,
            null,
            cancellationToken);

        if (!installed)
        {
            _logger.LogWarning("The fiscal device rejected the supplied license key.");
        }

        var status = await ReadDeviceStatusSafeAsync(cancellationToken).ConfigureAwait(false);
        return new FiscalDeviceActionResult(
            installed,
            installed ? "license-installed" : "license-rejected",
            installed
                ? "The fiscal device accepted the license key."
                : "The fiscal device rejected the license key.",
            status);
    }

    public async Task<FiscalDeviceActionResult> UpdateConfigurationAsync(
        FiscalDeviceConfigurationUpdate update,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.DeviceId) ||
            string.IsNullOrWhiteSpace(update.SerialNumber) ||
            string.IsNullOrWhiteSpace(update.CompanyName) ||
            string.IsNullOrWhiteSpace(update.ActivationKey) ||
            string.IsNullOrWhiteSpace(update.CertificateFolder))
        {
            throw new ValidationException(
                "The value is invalid.",
                new Dictionary<string, string[]>
                {
                    ["deviceId"] = ["Device id, serial number, company name, activation key and certificate folder are all required."],
                });
        }

        // The activation key is write-only: it reaches the engine and nothing else.
        await _gateway.UpdateConfigurationAsync(
            new FiscalDeviceConfiguration
            {
                DeviceId = update.DeviceId.Trim(),
                SerialNumber = update.SerialNumber.Trim(),
                CompanyName = update.CompanyName.Trim(),
                BranchName = update.BranchName.Trim(),
                City = update.City.Trim(),
                CertificateFolder = update.CertificateFolder.Trim(),
                ActivationKey = update.ActivationKey.Trim(),
            },
            cancellationToken).ConfigureAwait(false);

        await RecordAuditAsync(
            actorUserId,
            "Fiscalisation.Device.UpdateConfiguration",
            "device-configuration",
            true,
            // Identity fields only. The activation key is deliberately excluded.
            new
            {
                update.DeviceId,
                update.SerialNumber,
                update.CompanyName,
                update.BranchName,
                update.City,
                update.CertificateFolder,
            },
            cancellationToken);

        var status = await ReadDeviceStatusSafeAsync(cancellationToken).ConfigureAwait(false);
        return new FiscalDeviceActionResult(
            true,
            "device-configured",
            "The fiscal device accepted the new configuration.",
            status);
    }

    public async Task<FiscalDeviceProvisioningResult> ProvisionDeviceAsync(
        FiscalDeviceProvisioningRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            errors["deviceId"] = ["A device id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            errors["serialNumber"] = ["A device serial number is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ActivationKey))
        {
            errors["activationKey"] = ["An activation key is required."];
        }

        if (string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            errors["licenseKey"] = ["A license key is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("The value is invalid.", errors);
        }

        var deviceId = request.DeviceId.Trim();
        var serialNumber = request.SerialNumber.Trim();
        var steps = new List<string>();

        // 1. Bind the environment before anything can reach FDMS, so registration and
        //    every later fiscal call are pinned to the intended host.
        var selection = await _gateway.SelectEnvironmentAsync(request.Environment, cancellationToken).ConfigureAwait(false);
        steps.Add($"environment-selected:{selection.Environment}:{selection.ZimraServerUrl}");

        // 2. Optional local certificate set. Registration generates its own key and
        //    imports a server-signed certificate, so this is skipped by default.
        if (request.GenerateLocalCertificatesFirst)
        {
            var generated = await _gateway
                .GenerateDeviceCertificatesAsync(serialNumber, request.ActivationKey, deviceId, cancellationToken)
                .ConfigureAwait(false);
            steps.Add($"certificates-generated:{generated.CertificateCount}");
        }

        // 3. Register with ZIMRA. This performs taxpayer verification, CSR generation,
        //    device registration, certificate import, configuration retrieval and the
        //    server certificate, then rewrites the device identity.
        //
        //    Registration is NOT idempotent, for two independent reasons.
        //
        //    First, it passes through the engine's license gate, and that gate validates
        //    the *stored* license against the serial the engine captured when it was
        //    constructed. That produces a genuine ordering dependency:
        //
        //      - A legacy device whose stored license matches its current serial passes the
        //        gate, so registration must come first and the license second.
        //      - A device already carrying the target serial holds a license for some
        //        other serial, so the gate blocks registration. The license must be applied
        //        first, and registration then runs clean.
        //
        //    Second, an activation key is single-use. FDMS consumes it during taxpayer
        //    verification, so replaying it is rejected with 422 DEV02 "Activation key is
        //    incorrect". Re-registering a device that is already registered and certified
        //    therefore fails on a device that is in fact already provisioned.
        //
        //    Provisioning therefore inspects the device's own serial and certificate state
        //    and picks the outcome that reflects reality, instead of assuming one fixed
        //    sequence.
        var currentConfiguration = await _gateway.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var configuredSerial = currentConfiguration.SerialNumber;
        var alreadyTargeted = string.Equals(configuredSerial?.Trim(), serialNumber, StringComparison.OrdinalIgnoreCase);

        if (alreadyTargeted)
        {
            // The identity is already the target, so the new license is the one the gate
            // needs. Applying it first unblocks registration.
            var preLicensed = await _gateway
                .SetLicenseAsync(request.LicenseKey, cancellationToken)
                .ConfigureAwait(false);
            steps.Add(preLicensed ? "license-applied-before-registration" : "license-rejected-before-registration");

            if (!preLicensed)
            {
                var preLicenseStatus = await ReadDeviceStatusSafeAsync(cancellationToken).ConfigureAwait(false);
                return new FiscalDeviceProvisioningResult(
                    false,
                    "license-rejected",
                    "The device already carries the target serial but rejected the supplied license key, " +
                    "so registration cannot pass the engine's license gate.",
                    selection.Environment.ToString(),
                    selection.ZimraServerUrl,
                    selection.IsTestHost,
                    true,
                    false,
                    false,
                    null,
                    null,
                    null,
                    0,
                    null,
                    preLicenseStatus,
                    steps);
            }
        }

        // A target-serial device that already holds an issued certificate set is registered.
        // The evidence is the manifest FDMS signed during the first registration, read back
        // from the device, so this is an observation rather than an assumption.
        var existingCertificates = await _gateway
            .GetCertificateDetailsAsync(cancellationToken)
            .ConfigureAwait(false);
        var alreadyRegistered =
            alreadyTargeted
            && existingCertificates.Generated
            && !string.IsNullOrWhiteSpace(existingCertificates.CertificateFolder)
            && string.Equals(existingCertificates.SerialNumber?.Trim(), serialNumber, StringComparison.OrdinalIgnoreCase);

        FiscalDeviceRegistration registration;
        if (alreadyRegistered)
        {
            steps.Add("registration-already-complete");
            steps.Add("activation-key-replay-avoided");

            registration = new FiscalDeviceRegistration
            {
                Success = true,
                Message = "Device already registered with ZIMRA. Re-registration was not attempted " +
                          "because the activation key is single-use and replaying it is rejected by FDMS.",
                OperationId = null,
                TaxpayerName = currentConfiguration.CompanyName,
                TaxpayerTaxNumber = currentConfiguration.TaxpayerTaxNumber,
                SerialNumber = serialNumber,
                DeviceId = deviceId,
                Subject = existingCertificates.Subject,
                ValidTill = existingCertificates.ValidTill,
                CertificateFolder = existingCertificates.CertificateFolder,
            };
        }
        else
        {
            try
            {
                registration = await _gateway
                    .RegisterDeviceAsync(request.ActivationKey, serialNumber, deviceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (FiscalisationException) when (!alreadyTargeted)
            {
                // The gate can still block if the stored license is stale or absent. The
                // identity is now known, so install the license and retry once rather than
                // leaving the operator to work out the ordering.
                var recovered = await _gateway
                    .SetLicenseAsync(request.LicenseKey, cancellationToken)
                    .ConfigureAwait(false);
                steps.Add(recovered ? "license-applied-after-gate-block" : "license-rejected-after-gate-block");

                if (!recovered)
                {
                    throw;
                }

                await _gateway.RecycleDeviceAsync(cancellationToken).ConfigureAwait(false);
                steps.Add("engine-recycled-after-gate-block");

                registration = await _gateway
                    .RegisterDeviceAsync(request.ActivationKey, serialNumber, deviceId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        steps.Add($"registered:{registration.SerialNumber}:{registration.DeviceId}");

        // 4. Recycle the engine so the license is validated against the serial that
        //    registration just wrote, not the one it replaced. Skipped when the license was
        //    already applied above, because there the engine was built with this serial.
        if (!alreadyTargeted)
        {
            await _gateway.RecycleDeviceAsync(cancellationToken).ConfigureAwait(false);
            steps.Add("engine-recycled");
        }

        // 5. Install the device-bound license.
        if (alreadyTargeted)
        {
            steps.Add("license-applied");
        }
        else
        {
            var licensed = await _gateway.SetLicenseAsync(request.LicenseKey, cancellationToken).ConfigureAwait(false);
            steps.Add(licensed ? "license-applied" : "license-rejected");

            if (!licensed)
            {
                var rejectedStatus = await ReadDeviceStatusSafeAsync(cancellationToken).ConfigureAwait(false);
                return new FiscalDeviceProvisioningResult(
                    false,
                    "license-rejected",
                    "The device was registered but rejected the supplied license key.",
                    selection.Environment.ToString(),
                    selection.ZimraServerUrl,
                    selection.IsTestHost,
                    true,
                    true,
                    false,
                    registration.OperationId,
                    registration.TaxpayerName,
                    registration.TaxpayerTaxNumber,
                    0,
                    registration.CertificateFolder,
                    rejectedStatus,
                    steps);
            }
        }

        // 6. Report the identity, certificates and license the device actually holds.
        var certificates = await _gateway.GetCertificateDetailsAsync(cancellationToken).ConfigureAwait(false);
        steps.Add($"certificates:{certificates.CertificateCount}");

        await _gateway.EnsureEnvironmentSafetyAsync(cancellationToken).ConfigureAwait(false);
        steps.Add("environment-safety-verified");

        var status = await ReadDeviceStatusSafeAsync(cancellationToken).ConfigureAwait(false);
        steps.Add("status-read");

        await RecordAuditAsync(
            actorUserId,
            "Fiscalisation.Device.Provision",
            "device-provisioning",
            true,
            // Identity and outcome only. The activation key and license are excluded.
            new
            {
                request.Environment,
                request.DeviceId,
                request.SerialNumber,
                selection.ZimraServerUrl,
                registration.OperationId,
                registration.TaxpayerName,
                registration.TaxpayerTaxNumber,
                certificates.CertificateCount,
            },
            cancellationToken);

        return new FiscalDeviceProvisioningResult(
            true,
            "device-provisioned",
            "The device was registered and licensed.",
            selection.Environment.ToString(),
            selection.ZimraServerUrl,
            selection.IsTestHost,
            true,
            true,
            true,
            registration.OperationId,
            registration.TaxpayerName,
            registration.TaxpayerTaxNumber,
            certificates.CertificateCount,
            certificates.CertificateFolder ?? registration.CertificateFolder,
            status,
            steps);
    }

    private async Task<FiscalDeviceStatusDto?> ReadDeviceStatusSafeAsync(CancellationToken cancellationToken)    {
        try
        {
            return ToDto(await _gateway.GetStatusAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (FiscalisationException ex)
        {
            _logger.LogWarning(ex, "Fiscal device status could not be read after a device administration call");
            return null;
        }
    }

    /// <summary>Re-attempts a document the engine has not yet accepted.</summary>
    private async Task RetryAsync(FiscalisationEntity record, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == record.SaleId, cancellationToken);

        if (sale is null)
        {
            record.Status = nameof(FiscalisationState.Failed);
            record.Code = "sale-missing";
            record.Message = "The sale for this fiscalisation could not be loaded.";
            return;
        }

        var refund = record.RefundId is { } refundId
            ? await _context.SaleRefunds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken)
            : null;

        var status = await _gateway.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.FiscalDayState != FiscalDayState.Opened)
        {
            record.Status = nameof(FiscalisationState.PendingRetry);
            record.Code = "day-not-open";
            record.Message = $"The fiscal device day is {status.FiscalDayStatus ?? "not open"}; the document was not submitted.";
            return;
        }

        var configuration = await _gateway.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var document = FiscalDocumentBuilder.Build(sale, refund, configuration);

        record.AttemptCount += 1;
        record.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        record.SubmittedAtUtc ??= record.LastAttemptAtUtc;

        var result = await _gateway.FiscaliseAsync(document, cancellationToken).ConfigureAwait(false);
        ApplyResult(record, result);
    }

    /// <summary>
    /// Whether the engine is already holding this document, so a retry must reconcile
    /// through the engine's own queue instead of transacting the same invoice number again.
    ///
    /// A receipt on the record is the only positive evidence that the device actually
    /// fiscalised this document. Without one, the engine's queue says nothing about *this*
    /// document: an empty queue must never be read as "therefore fiscalised", or a document
    /// the device never accepted would be reported as fiscalised on the strength of a
    /// document belonging to a different sale. So the reconciliation shortcut below is
    /// gated on a receipt, and anything without one is re-driven through the device.
    /// </summary>
    private static bool EngineHoldsDocument(FiscalisationEntity record)
        => !string.IsNullOrWhiteSpace(record.ReceiptGlobalNo)
           && (record.PendingSync
               || string.Equals(record.Status, nameof(FiscalisationState.PendingRetry), StringComparison.Ordinal)
               || string.Equals(record.Status, nameof(FiscalisationState.Submitted), StringComparison.Ordinal));

    private static void ApplyResult(FiscalisationEntity record, FiscalResult result)
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
        record.Code = result.Code;
        record.Message = result.Message;
        record.PendingSync = result.PendingSync;
        record.ValidationWarningsJson = JsonSerializer.Serialize(result.ValidationWarnings, JsonOptions);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (!result.Success)
        {
            record.Status = nameof(FiscalisationState.Failed);
            return;
        }

        record.Status = result.PendingSync ? nameof(FiscalisationState.PendingRetry) : FiscalisedState;
        if (!result.PendingSync)
        {
            record.FiscalisedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task<FiscalDayStatusDto?> ReadDayStatusSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await GetFiscalDayStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FiscalisationException)
        {
            return null;
        }
    }

    private Task RecordAuditAsync(
        string actorUserId,
        string action,
        string entityId,
        bool success,
        object? metadata,
        CancellationToken cancellationToken)
        => _audit.RecordAsync(
            new AuditRecord
            {
                Id = Guid.CreateVersion7(),
                TimestampUtc = DateTimeOffset.UtcNow,
                ActorUserId = actorUserId,
                Action = action,
                EntityType = AuditEntity,
                EntityId = entityId,
                Result = success ? "Success" : "Failure",
                // Never log the activation key or license material.
                MetadataJson = metadata is null ? string.Empty : JsonSerializer.Serialize(metadata, JsonOptions),
            },
            cancellationToken);

    internal static FiscalDeviceStatusDto ToDto(FiscalDeviceStatus status) => new(
        status.Available,
        status.DeviceId,
        status.SerialNumber,
        status.CompanyName,
        status.TaxpayerName,
        status.TaxpayerTaxNumber,
        status.VatNumber,
        status.LicenseActive,
        status.LicenseStatus,
        status.LicenseStart,
        status.LicenseExpiry,
        status.CertificateCount,
        status.FiscalDayState.ToString(),
        status.FiscalDayStatus,
        status.LastFiscalDayNo,
        status.LastReceiptGlobalNo,
        status.ZimraReachable,
        status.ZimraSource,
        status.LastError);

    internal static FiscalisationDto ToDto(FiscalisationEntity entity)
    {
        IReadOnlyList<FiscalTender> tenders;
        try
        {
            tenders = JsonSerializer.Deserialize<List<TenderSnapshot>>(entity.TendersJson, JsonOptions) is { } rows
                ? rows.Select(t => new FiscalTender(
                    t.PaymentMethodCode,
                    t.PaymentMethodName,
                    t.Kind,
                    t.Amount,
                    t.ExternalReference)).ToArray()
                : Array.Empty<FiscalTender>();
        }
        catch (JsonException)
        {
            tenders = Array.Empty<FiscalTender>();
        }

        IReadOnlyList<string> warnings;
        try
        {
            warnings = JsonSerializer.Deserialize<List<string>>(entity.ValidationWarningsJson, JsonOptions)
                ?? new List<string>();
        }
        catch (JsonException)
        {
            warnings = Array.Empty<string>();
        }

        return new FiscalisationDto
        {
            Id = entity.Id,
            SaleId = entity.SaleId,
            SaleNumber = entity.SaleNumber,
            RefundId = entity.RefundId,
            RefundNumber = entity.RefundNumber,
            InvoiceNumber = entity.InvoiceNumber,
            DocumentKind = entity.DocumentKind,
            Status = entity.Status,
            DeviceId = entity.DeviceId,
            DeviceSerialNumber = entity.DeviceSerialNumber,
            FiscalDayNo = entity.FiscalDayNo,
            ReceiptCounter = entity.ReceiptCounter,
            ReceiptGlobalNo = entity.ReceiptGlobalNo,
            ReceiptId = entity.ReceiptId,
            OperationId = entity.OperationId,
            DeviceSignature = entity.DeviceSignature,
            ServerSignature = entity.ServerSignature,
            QrUrl = entity.QrUrl,
            OriginalInvoiceNumber = entity.OriginalInvoiceNumber,
            PaymentMapping = entity.PaymentMapping,
            Tenders = tenders,
            Code = entity.Code,
            Message = entity.Message,
            ValidationWarnings = warnings,
            PendingSync = entity.PendingSync,
            AttemptCount = entity.AttemptCount,
            SubmittedAtUtc = entity.SubmittedAtUtc,
            FiscalisedAtUtc = entity.FiscalisedAtUtc,
            LastAttemptAtUtc = entity.LastAttemptAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private sealed class TenderSnapshot
    {
        public string PaymentMethodCode { get; set; } = string.Empty;

        public string PaymentMethodName { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? ExternalReference { get; set; }
    }
}
