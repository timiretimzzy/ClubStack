using ClubStack.Application.Fiscalization;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Provisions the supplied D5.5 test device through ClubStack and verifies the result.
///
/// The chain under test is exactly the production one:
/// <c>IFiscalisationService -> FiscalStackZWDllGateway -> FiscalStackZW.dll -> TEST FDMS</c>.
/// This harness never contacts a ZIMRA or FDMS endpoint itself and never invokes the
/// engine by any route ClubStack does not already use.
///
/// Credentials come from environment variables and are never written to output, files or
/// assertions. Only non-secret device values are read back out of the engine.
///
/// Nothing runs unless <c>D55_TEST_DEVICE_ID</c> is set, so an unprovisioned host is
/// unaffected.
/// </summary>
[Collection("Database")]
public sealed class D55TestDeviceProvisioning : DatabaseTestBase
{
    private readonly ITestOutputHelper _output;

    public D55TestDeviceProvisioning(PostgreSqlTestFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _output = output;
    }

    private static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static bool Configured => Env("D55_TEST_DEVICE_ID") is not null;

    /// <summary>
    /// Builds the application container with the real engine adapter in place of the test
    /// fake, so the service exercised is the production one.
    /// </summary>
    private ServiceProvider BuildRealProvider()
        => TestServiceProvider.Build(
            Fixture.ConnectionString,
            services => services.AddSingleton<IFiscalisationGateway>(
                _ => new FiscalStackZWDllGateway(NullLogger<FiscalStackZWDllGateway>.Instance)));

    private void Log(string label, object? value) => _output.WriteLine($"{label,-26}: {value}");

    [Fact]
    public async Task Provision_test_device_and_verify_identity_environment_certificates_and_license()
    {
        if (!Configured)
        {
            _output.WriteLine("SKIPPED: D55_TEST_DEVICE_ID is not set; no test device configured.");
            return;
        }

        var deviceId = Env("D55_TEST_DEVICE_ID")!;
        var serial = Env("D55_TEST_SERIAL")!;
        var activationKey = Env("D55_TEST_ACTIVATION_KEY");
        var license = Env("D55_TEST_LICENSE");
        var environment = Env("D55_TEST_ENVIRONMENT") ?? "TEST";

        Assert.False(string.IsNullOrWhiteSpace(activationKey), "D55_TEST_ACTIVATION_KEY must be supplied.");
        Assert.False(string.IsNullOrWhiteSpace(license), "D55_TEST_LICENSE must be supplied.");

        var requested = string.Equals(environment, "TEST", StringComparison.OrdinalIgnoreCase)
            ? FiscalEnvironment.Test
            : FiscalEnvironment.Production;

        await using var provider = BuildRealProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFiscalisationService>();
        var gateway = scope.ServiceProvider.GetRequiredService<IFiscalisationGateway>();

        var before = await gateway.GetConfigurationAsync();
        var beforeStatus = await gateway.GetStatusAsync();
        Log("BEFORE SerialNumber", before.SerialNumber);
        Log("BEFORE DeviceId", before.DeviceId);
        Log("BEFORE ServerUrl", before.ServerUrl);
        Log("BEFORE LicenseActive", beforeStatus.LicenseActive);
        Log("BEFORE CertCount", beforeStatus.CertificateCount);

        var request = new FiscalDeviceProvisioningRequest(
            DeviceId: deviceId,
            SerialNumber: serial,
            Environment: requested,
            ActivationKey: activationKey!,
            LicenseKey: license!);

        var result = await service.ProvisionDeviceAsync(request, "d55-verification");

        foreach (var step in result.Steps)
        {
            Log("STEP", step);
        }

        Log("Result Success", result.Success);
        Log("Result Code", result.Code);
        Log("Result Message", result.Message);
        Log("Result Environment", result.Environment);
        Log("Result ZimraServerUrl", result.ZimraServerUrl);
        Log("Result IsTestHost", result.IsTestHost);
        Log("Result Registered", result.Registered);
        Log("Result LicenseApplied", result.LicenseApplied);
        Log("Result OperationId", result.OperationId);
        Log("Result TaxpayerName", result.TaxpayerName);
        Log("Result TaxpayerTIN", result.TaxpayerTaxNumber);
        Log("Result CertificateCount", result.CertificateCount);
        Log("Result CertificateFolder", result.CertificateFolder);

        var after = await gateway.GetConfigurationAsync();
        var status = await gateway.GetStatusAsync();
        var certificates = await gateway.GetCertificateDetailsAsync();

        Log("AFTER SerialNumber", after.SerialNumber);
        Log("AFTER DeviceId", after.DeviceId);
        Log("AFTER ServerUrl", after.ServerUrl);
        Log("AFTER StdTaxId", after.StandardRatedTaxId);
        Log("AFTER ZeroTaxId", after.ZeroRatedTaxId);
        Log("AFTER ExemptTaxId", after.ExemptTaxId);
        Log("AFTER CertCount", status.CertificateCount);
        Log("AFTER CertFolder", certificates.CertificateFolder);
        Log("AFTER CertFolderExists", Directory.Exists(certificates.CertificateFolder ?? string.Empty));
        Log("AFTER CertValidTill", certificates.ValidTill);
        Log("AFTER LicenseActive", status.LicenseActive);
        Log("AFTER LicenseStatus", status.LicenseStatus);
        Log("AFTER LicenseExpiry", status.LicenseExpiry?.ToString("yyyy-MM-dd"));
        Log("AFTER ZimraReachable", status.ZimraReachable);
        Log("AFTER FiscalDayState", status.FiscalDayState);
        Log("AFTER FiscalDayStatus", status.FiscalDayStatus);
        Log("AFTER Pending", await gateway.GetPendingCountAsync());

        Assert.True(result.Success, $"Provisioning failed: {result.Code} {result.Message}");
        Assert.Equal(FiscalEnvironment.Test, requested);
        Assert.True(result.IsTestHost, "The device must be bound to a TEST host.");
        Assert.True(result.Registered, "The device must be registered with ZIMRA.");
        Assert.True(result.LicenseApplied, "The device must accept its license.");
        Assert.Equal(deviceId, after.DeviceId);
        Assert.Equal(serial, after.SerialNumber);
        Assert.True(status.LicenseActive, "The engine must report an active license.");
        Assert.True(certificates.CertificateCount > 0, "The device must hold certificate material.");
        Assert.DoesNotContain("fdmsapi.zimra.co.zw", after.ServerUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
