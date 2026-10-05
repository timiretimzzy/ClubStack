using System.Globalization;
using System.Xml.Linq;
using ClubStack.Hardware.Fiscalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Adapts the legacy net48 <c>FiscalStackZW</c> facade to <see cref="IFiscalisationGateway"/>.
///
/// Constraints this adapter exists to satisfy:
/// <list type="bullet">
///   <item>The facade is process-wide mutable state (SQLite handle, device config, Zimra
///   gateway, current culture), so exactly one instance is created and every call is
///   serialised behind a single gate.</item>
///   <item>All facade calls block, so they run through <see cref="Task.Run"/> to keep them off
///   ASP.NET request threads and the EF/PostgreSQL connection path.</item>
///   <item>The facade is net48, so <c>Newtonsoft.Json</c>, <c>System.Data.SQLite.Core</c> and
///   <c>BouncyCastle.Cryptography</c> carry explicit version overrides. Without them the
///   runtime throws <c>FileNotFoundException</c> for
///   <c>System.Security.Permissions, Version=0.0.0.0</c>.</item>
///   <item>Nothing here bypasses licensing. Transactional facade calls are license-gated
///   upstream and their rejections are surfaced verbatim.</item>
/// </list>
/// </summary>
public sealed class FiscalStackZWDllGateway : IFiscalisationGateway
{
    /// <summary>Engine invoice flag for a fiscal invoice (sale).</summary>
    private const string FlagFiscalInvoice = "01";

    private const string PaymentMappingAggregate = "aggregate-single-currency";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<FiscalStackZWDllGateway> _logger;
    private readonly object _initLock = new();
    private readonly FiscalisationDeviceOptions _options;
    private readonly FiscalStackDeviceConfigFile _deviceConfig;

    private FiscalStackZW.FiscalStackZW? _device;
    private FiscalEnvironment? _selectedEnvironment;

    public FiscalStackZWDllGateway(
        ILogger<FiscalStackZWDllGateway> logger,
        IOptions<FiscalisationDeviceOptions>? options = null)
        : this(logger, options, new FiscalStackDeviceConfigFile())
    {
    }

    /// <summary>
    /// Overload that takes the device configuration explicitly.
    ///
    /// The environment safety guard is the one piece of this class that must be provable
    /// without a real device: it decides whether a call is allowed to reach FDMS at all.
    /// Taking the configuration as a parameter lets that decision be tested against
    /// synthetic files instead of whatever happens to be installed on the machine.
    /// </summary>
    internal FiscalStackZWDllGateway(
        ILogger<FiscalStackZWDllGateway> logger,
        IOptions<FiscalisationDeviceOptions>? options,
        FiscalStackDeviceConfigFile deviceConfig)
    {
        _logger = logger;
        _options = options?.Value ?? new FiscalisationDeviceOptions();
        _deviceConfig = deviceConfig;
        _selectedEnvironment = ReadPersistedEnvironment();
    }

    private FiscalStackZW.FiscalStackZW Device
    {
        get
        {
            if (_device is not null)
            {
                return _device;
            }

            lock (_initLock)
            {
                _device ??= new FiscalStackZW.FiscalStackZW();
                return _device;
            }
        }
    }

    // The engine's SQLite handle, Zimra gateway and fiscal-day counters are static
    // shared state, and ZIMRA rejects overlapping requests for one fiscal day, so all
    // facade calls funnel through this one gate.
    private async Task<T> InvokeAsync<T>(string operation, Func<FiscalStackZW.FiscalStackZW, string> call, Func<FiscalStackEnvelope, T> map, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var envelope = await Task.Run(() => FiscalStackEnvelope.Parse(call(Device))).ConfigureAwait(false);
            if (!envelope.Ok)
            {
                throw new FiscalisationException($"FiscalStackZW {operation} failed: {envelope.Message}");
            }

            return map(envelope);
        }
        catch (FiscalisationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FiscalStackZW {Operation} failed with an unhandled error", operation);
            throw new FiscalisationException($"FiscalStackZW {operation} failed: {ex.Message}", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<FiscalDeviceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => InvokeAsync("GetStatus", static d => d.GetStatus(), envelope => MapStatus(envelope, ReadIdentityAndLicensing()));

    /// <summary>
    /// Reads the device configuration from <c>GetConfig</c>, which is the engine's
    /// consolidated view and the only one that reports the device id, the API host and the
    /// resolved tax identifiers.
    ///
    /// <c>GetDeviceConfig</c> is deliberately not used here: it returns only the
    /// taxpayer-side record that FDMS holds, which carries no device id and reports the
    /// receipt QR host as <c>qrUrl</c>. Reading the device id from it yields null and
    /// reading the host from it yields the verification host, which would make a
    /// test-bound device look like it was pointed at a different endpoint than it is.
    /// </summary>
    public Task<FiscalDeviceConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
        => InvokeAsync("GetConfig", static d => d.GetConfig(), MapConfiguration);

    public Task<bool> SetLicenseAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ArgumentException("A license key is required.", nameof(licenseKey));
        }

        return InvokeAsync("SetLicense", d => d.SetLicense(licenseKey), envelope =>
        {
            var status = FiscalStackPayload.Value(FiscalStackPayload.Xml(envelope), "Status") ?? envelope.Message;
            return status.Contains("Active", StringComparison.OrdinalIgnoreCase);
        });
    }

    public Task UpdateConfigurationAsync(FiscalDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var folder = configuration.CertificateFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new FiscalisationException("A certificate folder is required to update the device configuration.");
        }

        if (!Directory.Exists(folder))
        {
            // The engine rejects a missing folder outright, so surface that clearly
            // instead of letting it fail as an opaque validation error.
            throw new FiscalisationException($"Certificate folder not found: {folder}");
        }

        return InvokeAsync("ApplyDeviceSettings", d => d.ApplyDeviceSettings(
                folder,
                configuration.DeviceId ?? string.Empty,
                configuration.SerialNumber ?? string.Empty,
                configuration.ActivationKey ?? string.Empty),
            static _ => true);
    }

    public Task<FiscalResult> OpenFiscalDayAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnvironmentSafety();
        return InvokeAsync("OpenDay", static d => d.OpenDay(), envelope => MapDay(envelope, true));
    }

    public Task<FiscalResult> CloseFiscalDayAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnvironmentSafety();
        return InvokeAsync("CloseDay", static d => d.CloseDay(), envelope => MapDay(envelope, false));
    }

    public Task<FiscalResult> FiscaliseAsync(FiscalDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.InvoiceNumber))
        {
            throw new ArgumentException("A fiscal document requires an invoice number.", nameof(document));
        }

        if (document.Lines.Count == 0)
        {
            throw new ArgumentException("A fiscal document requires at least one line.", nameof(document));
        }

        EnsureEnvironmentSafety();

        return FiscaliseCoreAsync(document, cancellationToken);
    }

    /// <summary>
    /// Submits the document and then reads the device's own stored record for it.
    ///
    /// The submit response carries only the day, the global number, the QR deep-link and
    /// the sync state. The device signature, the per-day receipt counter and the FDMS
    /// receipt id live in the record the device persisted, so they are read back from there
    /// rather than guessed. A receipt that is still pending is reported as such.
    /// </summary>
    private async Task<FiscalResult> FiscaliseCoreAsync(FiscalDocument document, CancellationToken cancellationToken)
    {
        var submitted = await InvokeAsync("TransactM", d => d.TransactM(
                document.CurrencyCode,
                document.CustomerEmail ?? string.Empty,
                document.InvoiceNumber,
                document.CustomerName ?? string.Empty,
                document.CustomerTaxNumber ?? string.Empty,
                document.CustomerAddress ?? string.Empty,
                document.CustomerPhone ?? string.Empty,
                document.CustomerTin ?? string.Empty,
                FiscalisationXmlBuilder.Money(document.Total),
                FiscalisationXmlBuilder.Money(document.TaxTotal),
                FlagFor(document.Kind),
                document.Comment ?? string.Empty,
                document.OriginalInvoiceNumber ?? string.Empty,
                FiscalisationXmlBuilder.BuildItems(document.Lines),
                FiscalisationXmlBuilder.BuildCurrencies(document.CurrencyCode, document.Total)),
            MapTransaction,
            cancellationToken).ConfigureAwait(false);

        if (!submitted.Success || string.IsNullOrWhiteSpace(document.InvoiceNumber))
        {
            return submitted;
        }

        try
        {
            var stored = await InvokeAsync("GetTransaction", d => d.GetTransaction(document.InvoiceNumber), MapStoredTransaction, cancellationToken)
                .ConfigureAwait(false);
            return Merge(submitted, stored);
        }
        catch (FiscalisationException)
        {
            // The receipt was accepted; only the read-back of its detail failed.
            return submitted;
        }
    }

    private static FiscalResult Merge(FiscalResult submitted, FiscalResult stored) => submitted with
    {
        ReceiptCounter = stored.ReceiptCounter ?? submitted.ReceiptCounter,
        ReceiptId = stored.ReceiptId ?? submitted.ReceiptId,
        OperationId = stored.OperationId ?? submitted.OperationId,
        DeviceSignature = stored.DeviceSignature ?? submitted.DeviceSignature,
        ServerSignature = stored.ServerSignature ?? submitted.ServerSignature,
        PendingSync = stored.PendingSync || submitted.PendingSync,
    };

    private static FiscalResult MapStoredTransaction(FiscalStackEnvelope envelope)
    {
        var json = FiscalStackPayload.Json(envelope);
        var receipt = JObject.Parse(json.Value<string>("XmlToTransact") ?? json.Value<string>("JsonData") ?? "{}");

        var signature = receipt.Value<JObject>("receiptDeviceSignature");
        var serverSignature = receipt.Value<string>("receiptServerSignature");
        var cloudStatus = json.Value<string>("FiscalCloudStatus");

        return new FiscalResult
        {
            Success = true,
            Message = envelope.Message,
            ReceiptCounter = receipt.Value<int?>("receiptCounter")?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ReceiptGlobalNo = receipt.Value<int?>("receiptGlobalNo")?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ReceiptId = NullIfBlank(json.Value<string>("ReceiptID")),
            DeviceSignature = signature?.Value<string>("signature"),
            ServerSignature = serverSignature,
            PendingSync = cloudStatus is not null
                && cloudStatus.Equals("pending", StringComparison.OrdinalIgnoreCase),
            PaymentMapping = PaymentMappingAggregate,
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public Task<string> SubmitPendingAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnvironmentSafety();
        return InvokeAsync("SubmitPendingQueue", d => d.SubmitPendingQueue(0), envelope =>
        {
            var json = FiscalStackPayload.Json(envelope);
            return json.Value<int>("stillPending").ToString(CultureInfo.InvariantCulture);
        });
    }

    /// <summary>
    /// Count-only query. Deliberately does not call <c>SubmitPendingQueue</c>, because
    /// that would flush the engine's queue as a side effect of a status read.
    /// </summary>
    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        => InvokeAsync("GetUnProcessedTransactions", d => d.GetUnProcessedTransactions(string.Empty, 1, 1), envelope =>
        {
            var json = FiscalStackPayload.Json(envelope);
            return json.Value<int>("TotalRecords");
        });

    public Task<FiscalEnvironmentSelection> SelectEnvironmentAsync(
        FiscalEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var previous = ReadPersistedEnvironment();
        var (zimraServer, verificationServer) = FiscalEndpoints.For(environment);

        // The engine derives its environment, its tax identifiers and every endpoint it
        // will contact from these persisted values, so writing them is what actually binds
        // the device.
        //
        // QRURL is also written even though the engine recomputes it from the verification
        // host on load (FiscalStackZW.cs:209). Leaving the previous environment's value on
        // disk makes the stored configuration disagree with the device's real behaviour,
        // which is exactly the kind of stale value an operator trusts by mistake.
        _deviceConfig.SetValues(new Dictionary<string, string?>
        {
            ["ZIMRASERVER"] = zimraServer,
            ["VERIFICATIONSERVER"] = verificationServer,
            ["QRURL"] = verificationServer,
        });

        _selectedEnvironment = environment;
        SwapDevice();

        var selection = new FiscalEnvironmentSelection
        {
            Environment = environment,
            ZimraServerUrl = zimraServer,
            VerificationServerUrl = verificationServer,
            IsTestHost = FiscalEndpoints.IsTestHost(zimraServer),
            EnvironmentTransitionPending = previous is not null
                && previous != environment,
        };

        _logger.LogInformation(
            "Fiscal device bound to the {Environment} FDMS environment ({ZimraServer}).",
            environment,
            zimraServer);

        return Task.FromResult(selection);
    }

    public async Task<FiscalDeviceStatus> RecycleDeviceAsync(CancellationToken cancellationToken = default)
    {
        SwapDevice();
        return await GetStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<FiscalDeviceCertificates> GenerateDeviceCertificatesAsync(
        string serialNumber,
        string activationKey,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(activationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        return InvokeAsync(
            "GenerateDeviceCertificates",
            d => d.GenerateDeviceCertificates(serialNumber.Trim(), activationKey.Trim(), deviceId.Trim()),
            MapCertificates);
    }

    public Task<FiscalDeviceRegistration> RegisterDeviceAsync(
        string activationKey,
        string serialNumber,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        return InvokeAsync(
            "RegisterDeviceWithZimra",
            d => d.RegisterDeviceWithZimra(activationKey.Trim(), serialNumber.Trim(), deviceId.Trim()),
            envelope =>
            {
                var json = FiscalStackPayload.Json(envelope);

                // Registration rewrites the device's identity. The engine captures the
                // serial when it is constructed, so the cached instance would keep
                // validating a license against the previous serial until it is replaced.
                SwapDevice();

                return new FiscalDeviceRegistration
                {
                    Success = true,
                    Message = envelope.Message,
                    OperationId = json.Value<string>("operationID"),
                    TaxpayerName = json.Value<string>("taxPayerName"),
                    TaxpayerTaxNumber = json.Value<string>("tin"),
                    VatNumber = json.Value<string>("vat"),
                    SerialNumber = json.Value<string>("serialNumber"),
                    DeviceId = json.Value<string>("deviceID"),
                    Subject = json.Value<string>("subject"),
                    Thumbprint = json.Value<string>("thumbprint"),
                    ValidTill = json.Value<string>("certificateValidTill"),
                    CertificateFolder = json.Value<string>("certificateFolder"),
                    OnlineStatus = json.Value<string>("onlineStatus"),
                };
            });
    }

    public Task<FiscalDeviceCertificates> GetCertificateDetailsAsync(CancellationToken cancellationToken = default)
        => InvokeAsync("GetCertificateDetails", static d => d.GetCertificateDetails(), MapCertificates);

    public Task EnsureEnvironmentSafetyAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnvironmentSafety();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Refuses to proceed when the device's configured endpoints are not all in the same
    /// environment, or when a test device would reach the live environment. Runs before any
    /// call that can reach FDMS.
    ///
    /// The host configuration is treated as the ground truth rather than
    /// <c>LAST_ENVIRONMENT</c> or the declared option, because that is what the engine
    /// actually uses: it reads <c>ZIMRASERVER</c> when it builds every request. The engine
    /// refreshes <c>LAST_ENVIRONMENT</c> only along its own save path, so after a
    /// provisioning step that wrote new hosts it can legitimately still name the previous
    /// environment. Trusting it would either block correct configuration or, worse, wave
    /// through a production device.
    /// </summary>
    private void EnsureEnvironmentSafety()
    {
        var zimraServer = _deviceConfig.Read("ZIMRASERVER");
        var verificationServer = _deviceConfig.Read("VERIFICATIONSERVER");
        var qrUrl = _deviceConfig.Read("QRURL");
        var serialNumber = _deviceConfig.Read("SERIALNUMBER");

        // Absent or unrecognised means the engine will use its own built-in default, which
        // is production. That is the most dangerous state there is, so it is refused rather
        // than resolved in the device's favour.
        var effective = FiscalEndpoints.EnvironmentOf(zimraServer);
        if (effective is null)
        {
            throw new FiscalisationException(
                $"Refusing to contact FDMS: ZIMRASERVER is missing or is not a recognised FDMS host " +
                $"('{zimraServer ?? "(unset)"}'). Without it the engine falls back to a production " +
                $"default. Select the environment explicitly before transacting " +
                $"(engine last recorded '{ReadPersistedEnvironment()?.ToString() ?? "none"}').");
        }

        // The environment the device is expected to be using: an explicit selection made
        // this session wins, otherwise the installation's declared binding. The engine's
        // LAST_ENVIRONMENT is deliberately not used here - it only refreshes on the
        // engine's own save path and can still name the previous environment.
        var expected = _selectedEnvironment ?? _options.Environment;
        if (effective != expected)
        {
            throw new FiscalisationException(
                $"Refusing to contact FDMS: this device's ZIMRASERVER ('{zimraServer}') is {effective}, " +
                $"but it is declared as a {expected} device. Test traffic is only permitted for a " +
                "device explicitly provisioned as a test device; re-provision the device or correct " +
                "the Fiscalisation:Device configuration.");
        }

        // Every other host the device can be pointed at must agree. QRURL is included because
        // it is the address a customer-facing receipt deep-link carries.
        foreach (var (key, host, required) in new[]
        {
            ("VERIFICATIONSERVER", verificationServer, true),
            ("QRURL", qrUrl, false),
        })
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                if (required)
                {
                    throw new FiscalisationException(
                        $"Refusing to contact FDMS: {key} is not set, so the device's environment is " +
                        "incomplete. Select the environment explicitly before transacting.");
                }

                continue;
            }

            if (FiscalEndpoints.EnvironmentOf(host) != effective)
            {
                throw new FiscalisationException(
                    $"Refusing to contact FDMS: {key} ('{host}') does not belong to the " +
                    $"{effective} environment this device would use ('{zimraServer}'). " +
                    "A device must not mix a test and a production host.");
            }
        }

        // An operator's declaration that this is a test device is honoured regardless of how
        // the hosts happen to be set. Reaching live FDMS is irreversible, so this is the one
        // check that is not allowed to be satisfied by configuration alone.
        if (_options.IsTestDevice && effective == FiscalEnvironment.Production)
        {
            throw new FiscalisationException(
                "Refusing to contact FDMS: this device is provisioned as a test device but its " +
                $"ZIMRASERVER ('{zimraServer}') is the live fiscal environment.");
        }

        if (_options.IsTestDevice
            && !string.IsNullOrWhiteSpace(serialNumber)
            && !string.IsNullOrWhiteSpace(_options.SerialNumber)
            && !string.Equals(serialNumber.Trim(), _options.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new FiscalisationException(
                $"Refusing to contact FDMS: this device is provisioned as test device " +
                $"'{_options.SerialNumber}' but its configured serial is '{serialNumber}'. " +
                "The device identity has drifted from its provisioning.");
        }
    }

    private void SwapDevice()
    {
        lock (_initLock)
        {
            _device = null;
        }
    }

    private FiscalEnvironment? ReadPersistedEnvironment()
    {
        var token = _deviceConfig?.Read("LAST_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim().Equals("test", StringComparison.OrdinalIgnoreCase)
            ? FiscalEnvironment.Test
            : FiscalEnvironment.Production;
    }

    private static FiscalDeviceCertificates MapCertificates(FiscalStackEnvelope envelope)
    {
        var json = FiscalStackPayload.Json(envelope);

        // The engine has two shapes here and they do not agree on casing or field names:
        //   - "no certificates" is an anonymous object, so camelCase: generated, certificateFolder.
        //   - "certificates found" serialises FiscalCertificateManifest, so PascalCase:
        //     CertificateFolder, DeviceCertificateSubject, CertificateValidTill, and so on.
        // Reading only one shape silently reports an issued device as having no
        // certificates, so both are accepted.
        var generated = json.Value<bool?>("generated") ?? json.Value<bool?>("Generated") == true;
        var folder = json.Value<string>("certificateFolder") ?? json.Value<string>("CertificateFolder");
        var deviceCertificate = json.Value<string>("deviceCertificateThumbprint") ?? json.Value<string>("DeviceCertificateThumbprint");
        var serverCertificate = json.Value<string>("serverCertificateThumbprint") ?? json.Value<string>("ServerCertificateThumbprint");
        var caCertificate = json.Value<string>("caCertificateThumbprint") ?? json.Value<string>("CACertificateThumbprint");

        // A manifest is proof that the full CA / device / server set was issued.
        var issued = new[] { caCertificate, deviceCertificate, serverCertificate }.Count(t => !string.IsNullOrWhiteSpace(t));

        return new FiscalDeviceCertificates
        {
            Generated = generated || !string.IsNullOrWhiteSpace(folder),
            CertificateFolder = folder,
            SerialNumber = json.Value<string>("serialNumber") ?? json.Value<string>("SerialNumber"),
            DeviceId = json.Value<string>("deviceID") ?? json.Value<string>("DeviceID"),
            Subject = json.Value<string>("deviceCertificateSubject") ?? json.Value<string>("DeviceCertificateSubject"),
            Thumbprint = deviceCertificate,
            ValidTill = json.Value<string>("certificateValidTill") ?? json.Value<string>("CertificateValidTill"),
            CertificateCount = issued > 0 ? issued : generated ? 1 : 0,
        };
    }

    private static string FlagFor(FiscalDocumentKind kind) => kind switch
    {
        FiscalDocumentKind.FiscalInvoice => FlagFiscalInvoice,
        FiscalDocumentKind.CreditNote => "02",
        FiscalDocumentKind.DebitNote => "03",
        _ => FlagFiscalInvoice,
    };

    private static FiscalResult MapDay(FiscalStackEnvelope envelope, bool opening)
    {
        var json = FiscalStackPayload.Json(envelope);
        return new FiscalResult
        {
            Success = true,
            Message = envelope.Message,
            FiscalDayNo = (json.Value<string>("fiscalDayNo") ?? json.Value<string>("FiscalDayNo") ?? string.Empty)
                .ToString(CultureInfo.InvariantCulture),
            OperationId = json.Value<string>("operationID") ?? json.Value<string>("OperationID"),
            PaymentMapping = opening ? "day-open" : "day-close",
        };
    }

    private static FiscalDeviceStatus MapStatus(FiscalStackEnvelope envelope, FiscalDeviceStatus? identity)
    {
        // GetStatus reports the fiscal-day and reachability state. It does not carry
        // device identity, so that is read from the device-status snapshot instead.
        var json = FiscalStackPayload.Json(envelope);
        return new FiscalDeviceStatus
        {
            Available = true,
            DeviceId = identity?.DeviceId,
            SerialNumber = identity?.SerialNumber,
            CompanyName = identity?.CompanyName,
            TaxpayerName = identity?.TaxpayerName,
            TaxpayerTaxNumber = identity?.TaxpayerTaxNumber,
            VatNumber = identity?.VatNumber,
            LicenseActive = identity?.LicenseActive ?? false,
            LicenseStatus = identity?.LicenseStatus,
            LicenseStart = identity?.LicenseStart,
            LicenseExpiry = identity?.LicenseExpiry,
            CertificateCount = identity?.CertificateCount ?? 0,
            FiscalDayStatus = json.Value<string>("FiscalDayStatus"),
            FiscalDayState = MapDayState(json.Value<string>("FiscalDayStatus")),
            LastFiscalDayNo = json.Value<string>("LastFiscalDayNo"),
            LastReceiptGlobalNo = json.Value<string>("LastReceiptGlobalNo"),
            ZimraReachable = json.Value<bool?>("zimraReachable") ?? false,
            ZimraSource = json.Value<string>("zimraSource"),
        };
    }

    private static FiscalDayState MapDayState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FiscalDayState.Unknown;
        }

        // The engine reports "FiscalDayOpen"/"FiscalDayClosed".
        if (raw.Contains("open", StringComparison.OrdinalIgnoreCase))
        {
            return FiscalDayState.Opened;
        }

        if (raw.Contains("clos", StringComparison.OrdinalIgnoreCase))
        {
            return FiscalDayState.Closed;
        }

        return FiscalDayState.Unknown;
    }

    // Snapshot helpers re-enter the engine while the outer InvokeAsync already holds
    // the gate, so they must not acquire it again. They never construct a second
    // facade instance: the engine's state is process-wide and a new instance would
    // re-open its SQLite handle independently.
    private string? RawSnapshot(string operation, Func<FiscalStackZW.FiscalStackZW, string> call)
    {
        try
        {
            var envelope = FiscalStackEnvelope.Parse(call(Device));
            return envelope.Ok ? envelope.Data : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FiscalStackZW {Operation} snapshot was unavailable", operation);
            return null;
        }
    }

    private FiscalDeviceStatus? ReadIdentityAndLicensing()
    {
        var statusXml = RawSnapshot("GetDeviceStatus", static d => d.GetDeviceStatus());
        var licenseXml = RawSnapshot("GetLicense", static d => d.GetLicense());
        var certificateJson = RawSnapshot("GetCertificateDetails", static d => d.GetCertificateDetails());

        FiscalDeviceStatus? result = null;
        if (!string.IsNullOrWhiteSpace(statusXml))
        {
            var doc = XDocument.Parse(statusXml);
            result = new FiscalDeviceStatus
            {
                Available = true,
                DeviceId = FiscalStackPayload.Value(doc, "DeviceID"),
                SerialNumber = FiscalStackPayload.Value(doc, "SerialNumber"),
                CompanyName = FiscalStackPayload.Value(doc, "CompanyName"),
            };
        }

        if (!string.IsNullOrWhiteSpace(licenseXml))
        {
            var doc = XDocument.Parse(licenseXml);
            var licenseStatus = FiscalStackPayload.Value(doc, "Status");
            result = (result ?? new FiscalDeviceStatus()) with
            {
                LicenseActive = licenseStatus?.Contains("Active", StringComparison.OrdinalIgnoreCase) ?? false,
                LicenseStatus = licenseStatus,
                LicenseStart = ParseDate(FiscalStackPayload.Value(doc, "StartDate")),
                LicenseExpiry = ParseDate(FiscalStackPayload.Value(doc, "ExpiryDate")),
            };
        }

        if (!string.IsNullOrWhiteSpace(certificateJson))
        {
            result = (result ?? new FiscalDeviceStatus()) with
            {
                CertificateCount = JObject.Parse(certificateJson).Value<bool?>("generated") == true ? 1 : 0,
            };
        }

        return result;
    }

    private static DateTimeOffset? ParseDate(string? raw)
        => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    private static FiscalDeviceConfiguration MapConfiguration(FiscalStackEnvelope envelope)
    {
        var json = FiscalStackPayload.Json(envelope);
        var brackets = (json.Value<JArray>("applicableTaxes") ?? [])
            .Select(t => new DeviceTaxBracket(
                t.Value<string>("taxID") ?? "0",
                t.Value<string>("taxName") ?? string.Empty,
                t.Value<decimal?>("taxPercent") ?? 0m))
            .Where(b => int.TryParse(b.TaxId, out var id) && id > 0)
            .ToArray();
        var taxes = brackets;

        var address = json.Value<JObject>("deviceBranchAddress");

        return new FiscalDeviceConfiguration
        {
            DeviceId = json.Value<string>("deviceID"),
            SerialNumber = json.Value<string>("deviceSerialNo"),
            CompanyName = json.Value<string>("taxPayerName"),
            BranchName = json.Value<string>("deviceBranchName"),
            City = address?.Value<string>("city"),
            TaxpayerTaxNumber = json.Value<string>("taxPayerTIN"),
            VatNumber = json.Value<string>("vatNumber"),
            Environment = json.Value<string>("deviceOperatingMode"),

            // The engine's own resolved tax ids are authoritative. Its pre-submission
            // compliance check rejects any receipt whose taxID is not one of them
            // (RCPT025), so they are what a receipt must be built from. The bracket table
            // is only a fallback: its tax names do not reliably identify the exempt
            // bracket, and a name heuristic that guesses here produces receipts the
            // device then refuses.
            StandardRatedTaxId = ResolveTaxId(json.Value<int?>("taxIDStandardRated"), taxes, TaxIds.ResolveStandardRated),
            ZeroRatedTaxId = ResolveTaxId(json.Value<int?>("taxIDZeroRated"), taxes, TaxIds.ResolveZeroRated),
            ExemptTaxId = ResolveTaxId(json.Value<int?>("taxIDExempted"), taxes, TaxIds.ResolveExempt),
            TaxBrackets = NormaliseBrackets(taxes),

            // The device API host, which is the endpoint a fiscal call actually uses.
            ServerUrl = json.Value<string>("zimraServer"),
        };
    }

    /// <summary>
    /// Collapses the duplicated rows the device returns for a single tax id.
    ///
    /// The table is not internally consistent: one taxpayer comes back with the same id
    /// twice, once as a fraction under a "Zero Rated" name and once as a percentage under
    /// the standard-rated name, and another id appears twice with different rates. The
    /// engine matches a receipt's rate against these rows with a tolerance tight enough to
    /// miss both forms, then falls back to a hardcoded tax id the device does not accept.
    /// Exposing one row per id, in the percentage form the engine can match, keeps callers
    /// from pricing a receipt with a rate the device will refuse.
    /// </summary>
    private static DeviceTaxBracket[] NormaliseBrackets(DeviceTaxBracket[] brackets)
        => brackets
            .GroupBy(b => b.TaxId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(b => b.Rate).ThenBy(b => b.Name, StringComparer.Ordinal).First())
            .OrderBy(b => int.Parse(b.TaxId, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

    /// <summary>
    /// Prefers the tax id the engine reports for the bracket, and falls back to a
    /// name-based reading of the returned bracket table when the engine does not report
    /// one.
    /// </summary>
    private static int ResolveTaxId(int? engineValue, DeviceTaxBracket[] taxes, Func<DeviceTaxBracket[], int> resolve)
    {
        if (engineValue is > 0)
        {
            return engineValue.Value;
        }

        return taxes.Length == 0 ? 0 : resolve(taxes);
    }

    private static FiscalResult MapTransaction(FiscalStackEnvelope envelope)
    {
        var doc = FiscalStackPayload.Xml(envelope);
        var syncState = FiscalStackPayload.Value(doc, "SyncState");
        var pending = string.Equals(syncState, "pending", StringComparison.OrdinalIgnoreCase);

        var warnings = (doc.Root?.Element("ValidationWarnings")?.Nodes() ?? Enumerable.Empty<XNode>())
            .OfType<XElement>()
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToArray();

        return new FiscalResult
        {
            Success = true,
            Message = envelope.Message,
            DeviceId = FiscalStackPayload.Value(doc, "DeviceID"),
            DeviceSerialNumber = FiscalStackPayload.Value(doc, "SerialNumber"),
            FiscalDayNo = FiscalStackPayload.Value(doc, "FiscalDayNo"),
            ReceiptGlobalNo = FiscalStackPayload.Value(doc, "ReceiptGlobalNo"),
            ReceiptCounter = FiscalStackPayload.Value(doc, "ReceiptCounter") ?? FiscalStackPayload.Value(doc, "ReceiptCounterValue"),
            ReceiptId = FiscalStackPayload.Value(doc, "ReceiptID") ?? FiscalStackPayload.Value(doc, "ReceiptId"),
            OperationId = FiscalStackPayload.Value(doc, "OperationID") ?? FiscalStackPayload.Value(doc, "OperationId"),
            QrUrl = FiscalStackPayload.Value(doc, "QRCode") ?? FiscalStackPayload.Value(doc, "QrUrl"),
            DeviceSignature = FiscalStackPayload.Value(doc, "DeviceSignature"),
            ServerSignature = FiscalStackPayload.Value(doc, "ServerSignature"),
            PendingSync = pending,
            ValidationWarnings = warnings,
            PaymentMapping = PaymentMappingAggregate,
        };
    }
}
