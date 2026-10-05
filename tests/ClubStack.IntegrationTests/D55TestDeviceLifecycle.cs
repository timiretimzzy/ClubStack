using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Drives the D5.5 test device through ClubStack's fiscalisation adapter and the real
/// FiscalStackZW engine, then exercises a full fiscal day on the TEST FDMS host.
///
/// The chain is the mandated one:
/// <c>ClubStack -> IFiscalisationGateway -> FiscalStackZWDllGateway -> FiscalStackZW.dll -> TEST FDMS</c>.
/// This harness never contacts a ZIMRA or FDMS endpoint itself, never constructs an
/// engine instance outside the adapter, and never issues an engine call the adapter does
/// not already make on ClubStack's behalf.
///
/// It deliberately does not use the database fixture: device provisioning and the fiscal
/// day are engine concerns, and the adapter carries no persistence dependency. Persistence
/// of the fiscal result is covered by the database-backed suite.
///
/// Secrets come from environment variables and are never written to output, files or
/// assertions. Nothing runs unless <c>D55_TEST_DEVICE_ID</c> is set.
/// </summary>
public sealed class D55TestDeviceLifecycle
{
    private readonly ITestOutputHelper _output;

    public D55TestDeviceLifecycle(ITestOutputHelper output) => _output = output;

    private static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static bool Configured => Env("D55_TEST_DEVICE_ID") is not null;

    private void Log(string label, object? value) => _output.WriteLine($"{label,-28}: {value}");

    [Fact]
    public async Task Provision_device_and_run_a_full_fiscal_day_on_the_test_host()
    {
        if (!Configured)
        {
            _output.WriteLine("SKIPPED: D55_TEST_DEVICE_ID is not set; no test device configured.");
            return;
        }

        var deviceId = Env("D55_TEST_DEVICE_ID")!;
        var serial = Env("D55_TEST_SERIAL")!;
        var activationKey = Env("D55_TEST_ACTIVATION_KEY")!;
        var license = Env("D55_TEST_LICENSE")!;
        var environment = string.Equals(Env("D55_TEST_ENVIRONMENT") ?? "TEST", "PRODUCTION", StringComparison.OrdinalIgnoreCase)
            ? FiscalEnvironment.Production
            : FiscalEnvironment.Test;

        IFiscalisationGateway gateway = new FiscalStackZWDllGateway(
            NullLogger<FiscalStackZWDllGateway>.Instance,
            Options.Create(new FiscalisationDeviceOptions
            {
                Environment = environment,
                IsTestDevice = environment == FiscalEnvironment.Test,
                SerialNumber = serial,
                DeviceId = deviceId,
            }));

        // ---- environment -----------------------------------------------------
        var selection = await gateway.SelectEnvironmentAsync(environment);
        Log("ENV Environment", selection.Environment);
        Log("ENV ZimraServer", selection.ZimraServerUrl);
        Log("ENV Verification", selection.VerificationServerUrl);
        Log("ENV IsTestHost", selection.IsTestHost);
        Log("ENV TransitionPending", selection.EnvironmentTransitionPending);
        Assert.True(selection.IsTestHost, "The device must be bound to the FDMS test host.");
        Assert.DoesNotContain("fdmsapi.zimra.co.zw", selection.ZimraServerUrl, StringComparison.OrdinalIgnoreCase);

        // ---- local certificate set (POS App wizard stage 2) -------------------
        // The reference POS application generates a local certificate set before it
        // registers, so the device has key material in place for the CSR the engine builds
        // during registration. Failure here is not fatal: registration generates its own
        // keys, so the stage is reported and the run continues.
        try
        {
            var generated = await gateway.GenerateDeviceCertificatesAsync(serial, activationKey, deviceId);
            Log("CERTGEN Count", generated.CertificateCount);
            Log("CERTGEN Folder", generated.CertificateFolder);
            Log("CERTGEN Thumbprint", generated.Thumbprint);
        }
        catch (Exception ex)
        {
            Log("CERTGEN", "skipped: " + ex.Message);
        }

        // ---- registration (the engine calls ZIMRA) ---------------------------
        // Registration is not idempotent, for two independent reasons:
        //   - it passes through the engine's license gate, which validates the stored
        //     license against the serial the engine was constructed with, so the
        //     register/license order depends on the identity the device already holds;
        //   - an activation key is single-use, so FDMS rejects a replay with 422 DEV02.
        // FiscalisationService applies exactly these rules; the harness mirrors them so the
        // two paths cannot drift apart.
        var currentConfiguration = await gateway.GetConfigurationAsync();
        var alreadyTargeted = string.Equals(currentConfiguration.SerialNumber?.Trim(), serial, StringComparison.OrdinalIgnoreCase);
        Log("ORDER AlreadyTargetSerial", alreadyTargeted);

        if (alreadyTargeted)
        {
            Log("LICENSE PreAccepted", await gateway.SetLicenseAsync(license));
        }

        // A target-serial device that already holds an issued certificate set is registered.
        var existingCertificates = await gateway.GetCertificateDetailsAsync();
        var alreadyRegistered =
            alreadyTargeted
            && existingCertificates.Generated
            && !string.IsNullOrWhiteSpace(existingCertificates.CertificateFolder)
            && string.Equals(existingCertificates.SerialNumber?.Trim(), serial, StringComparison.OrdinalIgnoreCase);
        Log("ORDER AlreadyRegistered", alreadyRegistered);

        if (alreadyRegistered)
        {
            Log("REGISTER Message", "already registered with ZIMRA; activation key replay avoided");
            Log("REGISTER TaxpayerName", currentConfiguration.CompanyName);
            Log("REGISTER Tin", currentConfiguration.TaxpayerTaxNumber);
            Log("REGISTER Serial", serial);
            Log("REGISTER DeviceId", deviceId);
            Log("REGISTER Subject", existingCertificates.Subject);
            Log("REGISTER ValidTill", existingCertificates.ValidTill);
            Log("REGISTER CertificateFolder", existingCertificates.CertificateFolder);
        }
        else
        {
            try
            {
                var registration = await gateway.RegisterDeviceAsync(activationKey, serial, deviceId);

                Log("REGISTER Message", registration.Message);
                Log("REGISTER OperationId", registration.OperationId);
                Log("REGISTER TaxpayerName", registration.TaxpayerName);
                Log("REGISTER Tin", registration.TaxpayerTaxNumber);
                Log("REGISTER Serial", registration.SerialNumber);
                Log("REGISTER DeviceId", registration.DeviceId);
                Log("REGISTER Subject", registration.Subject);
                Log("REGISTER ValidTill", registration.ValidTill);
                Log("REGISTER CertificateFolder", registration.CertificateFolder);
                Log("REGISTER OnlineStatus", registration.OnlineStatus);
                Assert.True(registration.Success);
            }
            catch (Exception ex)
            {
                Log("REGISTER FAILED", ex.Message);
                throw;
            }
        }

        // The engine validates a license against the serial captured at construction,
        // which registration has just replaced, so the instance is recycled first.
        var recycled = await gateway.RecycleDeviceAsync();
        Log("RECYCLED Serial", recycled.SerialNumber);

        // ---- license ---------------------------------------------------------
        Log("LICENSE PostRecycle", await gateway.SetLicenseAsync(license));

        // ---- certificates and identity ---------------------------------------
        var certificates = await gateway.GetCertificateDetailsAsync();
        var configuration = await gateway.GetConfigurationAsync();
        var status = await gateway.GetStatusAsync();

        Log("CERT Generated", certificates.Generated);
        Log("CERT Count", certificates.CertificateCount);
        Log("CERT Folder", certificates.CertificateFolder);
        Log("CERT FolderExists", Directory.Exists(certificates.CertificateFolder ?? string.Empty));
        Log("CERT Thumbprint", certificates.Thumbprint);
        Log("CERT ValidTill", certificates.ValidTill);
        Log("CFG Serial", configuration.SerialNumber);
        Log("CFG DeviceId", configuration.DeviceId);
        Log("CFG ServerUrl", configuration.ServerUrl);
        Log("CFG StdTaxId", configuration.StandardRatedTaxId);
        Log("CFG ZeroTaxId", configuration.ZeroRatedTaxId);
        Log("CFG ExemptTaxId", configuration.ExemptTaxId);
        Log("STATUS LicenseActive", status.LicenseActive);
        Log("STATUS LicenseExpiry", status.LicenseExpiry?.ToString("yyyy-MM-dd"));
        Log("STATUS ZimraReachable", status.ZimraReachable);
        Log("STATUS FiscalDay", status.FiscalDayStatus);

        Assert.Equal(serial, configuration.SerialNumber);
        Assert.Equal(deviceId, configuration.DeviceId);
        Assert.True(status.LicenseActive, "The engine must report an active license.");
        Assert.True(certificates.CertificateCount > 0, "The device must hold certificate material.");
        Assert.DoesNotContain("fdms.zimra.co.zw", configuration.ServerUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // ---- fiscal day ------------------------------------------------------
        var dayBefore = await gateway.GetStatusAsync();
        Log("DAY BeforeState", dayBefore.FiscalDayStatus);

        if (dayBefore.FiscalDayState != FiscalDayState.Opened)
        {
            var opened = await gateway.OpenFiscalDayAsync();
            Log("DAY Opened No", opened.FiscalDayNo);
            Log("DAY Opened OperationId", opened.OperationId);
            Log("DAY Opened Message", opened.Message);
            Assert.True(opened.Success);
        }
        else
        {
            Log("DAY", "already open; not opening another");
        }

        var dayOpen = await gateway.GetStatusAsync();
        Log("DAY AfterState", dayOpen.FiscalDayStatus);
        Log("DAY LastNo", dayOpen.LastFiscalDayNo);
        Assert.Equal(FiscalDayState.Opened, dayOpen.FiscalDayState);

        // ---- controlled sale -------------------------------------------------
        // Price the receipt with a bracket the device actually has configured. A rate the
        // device does not hold is refused before submission (RCPT025), and the engine's
        // fallback for a rate it cannot match is a hardcoded tax id this taxpayer does
        // not use. The device is VAT-inclusive, so a net line becomes gross plus VAT.
        var standardBracket = configuration.TaxBrackets
            .FirstOrDefault(b => b.TaxId == configuration.StandardRatedTaxId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.NotNull(standardBracket);

        var rate = (double)standardBracket!.Rate;
        var net = 10.00m;
        var gross = Math.Round(net * (1m + (decimal)(rate / 100.0)), 2, MidpointRounding.AwayFromZero);
        var vat = gross - net;

        Log("TAX BracketId", standardBracket.TaxId);
        Log("TAX BracketName", standardBracket.Name);
        Log("TAX Rate", rate);
        Log("TAX Net", net);
        Log("TAX Gross", gross);
        Log("TAX Vat", vat);

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var saleInvoice = $"D55-SALE-{stamp}";

        var sale = await gateway.FiscaliseAsync(Sale(saleInvoice, "D55 controlled test sale", 1m, gross, vat, rate));
        Log("SALE Invoice", saleInvoice);
        Log("SALE Message", sale.Message);
        Log("SALE FiscalDayNo", sale.FiscalDayNo);
        Log("SALE ReceiptGlobalNo", sale.ReceiptGlobalNo);
        Log("SALE ReceiptCounter", sale.ReceiptCounter);
        Log("SALE ReceiptId", sale.ReceiptId);
        Log("SALE OperationId", sale.OperationId);
        Log("SALE DeviceSignature", sale.DeviceSignature);
        Log("SALE ServerSignature", sale.ServerSignature);
        Log("SALE QrUrl", sale.QrUrl);
        Log("SALE PendingSync", sale.PendingSync);
        Log("SALE Warnings", string.Join(" | ", sale.ValidationWarnings));

        Assert.False(string.IsNullOrWhiteSpace(sale.ReceiptGlobalNo), "A fiscalised sale must carry a receipt global number.");

        // The device must have accepted the receipt itself, not merely queued it: a pending
        // receipt is stored locally and does not consume a global number on the server.
        Assert.False(sale.PendingSync, "The sale must be accepted by the test host, not left pending.");

        // The receipt QR deep-link is the verifier-facing proof that FDMS issued the
        // receipt, and it must point at the test verification host for this device.
        Assert.False(string.IsNullOrWhiteSpace(sale.QrUrl), "A fiscalised sale must expose its verification QR link.");
        Assert.Contains(FiscalEndpoints.TestVerificationServer, sale.QrUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(deviceId, sale.QrUrl!, StringComparison.Ordinal);

        // The device signature is read back from the record the device stored. The engine
        // only signs after it builds the receipt, so a signature is expected here; it is
        // logged rather than required because the engine's submit response does not
        // guarantee one for a credit note.
        Assert.False(string.IsNullOrWhiteSpace(sale.DeviceSignature), "The stored receipt must be device-signed.");

        // ---- a duplicate must not fiscalise twice ----------------------------
        // The device is the last line of defence against a double fiscalisation: it
        // refuses an invoice number it has already used and does not advance the receipt
        // chain. ClubStack's outbox is the first line of defence, so this proves the
        // device's own guarantee holds independently of it.
        FiscalResult? repeat = null;
        string? repeatError = null;
        try
        {
            repeat = await gateway.FiscaliseAsync(Sale(saleInvoice, "D55 controlled test sale", 1m, gross, vat, rate));
        }
        catch (FiscalisationException ex)
        {
            repeatError = ex.Message;
        }

        Log("REPEAT Refused", repeat is null);
        Log("REPEAT Error", repeatError);
        Assert.True(repeat is null, "The device must refuse a repeat of an invoice number it has already fiscalised.");
        Assert.Contains("already used", repeatError ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var afterRepeat = await gateway.GetStatusAsync();
        Log("REPEAT LastGlobalNo", afterRepeat.LastReceiptGlobalNo);
        Assert.Equal(sale.ReceiptGlobalNo, afterRepeat.LastReceiptGlobalNo);

        // ---- controlled refund / credit note --------------------------------
        var refundInvoice = $"D55-RFN-{stamp}";
        var refund = await gateway.FiscaliseAsync(CreditNote(refundInvoice, saleInvoice, 1m, gross, vat, rate));
        Log("REFUND Invoice", refundInvoice);
        Log("REFUND Against", saleInvoice);
        Log("REFUND Message", refund.Message);
        Log("REFUND FiscalDayNo", refund.FiscalDayNo);
        Log("REFUND ReceiptGlobalNo", refund.ReceiptGlobalNo);
        Log("REFUND OperationId", refund.OperationId);
        Log("REFUND DeviceSignature", refund.DeviceSignature);
        Log("REFUND PendingSync", refund.PendingSync);

        Assert.False(string.IsNullOrWhiteSpace(refund.ReceiptGlobalNo), "A fiscalised credit note must carry a receipt global number.");
        Assert.NotEqual(sale.ReceiptGlobalNo, refund.ReceiptGlobalNo);

        // ---- close the day ---------------------------------------------------
        var closed = await gateway.CloseFiscalDayAsync();
        Log("CLOSE Message", closed.Message);
        Log("CLOSE FiscalDayNo", closed.FiscalDayNo);
        Log("CLOSE OperationId", closed.OperationId);

        var dayEnd = await gateway.GetStatusAsync();
        Log("END FiscalDayState", dayEnd.FiscalDayState);
        Log("END FiscalDayStatus", dayEnd.FiscalDayStatus);
        Log("END LastReceiptGlobalNo", dayEnd.LastReceiptGlobalNo);
        Log("END ZimraReachable", dayEnd.ZimraReachable);
        Log("END Pending", await gateway.GetPendingCountAsync());

        Assert.Equal(FiscalDayState.Closed, dayEnd.FiscalDayState);
    }

    /// <param name="amount">The VAT-inclusive line total as printed on the receipt.</param>
    /// <param name="tax">The VAT contained in <paramref name="amount"/>.</param>
    /// <param name="rate">The device-configured rate the line is priced at.</param>
    private static FiscalDocument Sale(string invoice, string comment, decimal qty, decimal amount, decimal tax, double rate)
        => new()
        {
            InvoiceNumber = invoice,
            Kind = FiscalDocumentKind.FiscalInvoice,
            CurrencyCode = "ZWG",
            Total = amount,
            TaxTotal = tax,
            Comment = comment,
            Tenders = [new FiscalTender("Cash", "Cash", "Cash", amount, null)],
            Lines =
            [
                new FiscalLine
                {
                    ItemCode = "D55-TEST-1",
                    Name1 = "D5.5 controlled test item",
                    Quantity = qty,
                    Price = amount,
                    Amount = amount,
                    Tax = tax,
                    TaxRate = (decimal)rate,
                    TaxId = 0,
                },
            ],
        };

    private static FiscalDocument CreditNote(string invoice, string originalInvoice, decimal qty, decimal amount, decimal tax, double rate)
        => new()
        {
            InvoiceNumber = invoice,
            Kind = FiscalDocumentKind.CreditNote,
            OriginalInvoiceNumber = originalInvoice,
            CurrencyCode = "ZWG",
            Total = amount,
            TaxTotal = tax,
            Comment = "D5.5 controlled refund",
            Tenders = [new FiscalTender("Cash", "Cash", "Cash", amount, null)],
            Lines =
            [
                new FiscalLine
                {
                    ItemCode = "D55-TEST-1",
                    Name1 = "D5.5 controlled test item",
                    Quantity = qty,
                    Price = amount,
                    Amount = amount,
                    Tax = tax,
                    TaxRate = (decimal)rate,
                    TaxId = 0,
                },
            ],
        };
}
