using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClubStack.Application.Tests;

/// <summary>
/// The environment safety guard is the last thing standing between a misconfigured device
/// and the live fiscal environment, so it is tested directly against synthetic device
/// configuration files rather than only through the live harness.
///
/// Every case here is a configuration that must be refused. A guard that only passes is
/// untested; the property that matters is that it fails closed, including on inputs
/// nobody anticipated.
/// </summary>
public sealed class FiscalStackEnvironmentSafetyTests : IDisposable
{
    private readonly List<string> _paths = [];

    private const string TestApi = "https://fdmsapitest.zimra.co.zw";
    private const string TestVerification = "https://fdmstest.zimra.co.zw";
    private const string ProdApi = "https://fdmsapi.zimra.co.zw";
    private const string ProdVerification = "https://fdms.zimra.co.zw";

    public void Dispose()
    {
        foreach (var path in _paths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    /// <summary>Writes a device configuration and returns a gateway reading it.</summary>
    private FiscalStackZWDllGateway Gateway(
        (string Key, string Value)[] settings,
        FiscalEnvironment? declared = null,
        bool isTestDevice = false,
        string? serialNumber = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"d55-safety-{Guid.NewGuid():N}.ini");
        _paths.Add(path);
        File.WriteAllText(path, string.Join(Environment.NewLine, settings.Select(s => $"{s.Key}={s.Value}")));

        var options = new FiscalisationDeviceOptions
        {
            Environment = declared ?? FiscalEnvironment.Production,
            IsTestDevice = isTestDevice,
            SerialNumber = serialNumber,
        };

        return new FiscalStackZWDllGateway(
            NullLogger<FiscalStackZWDllGateway>.Instance,
            Options.Create(options),
            new FiscalStackDeviceConfigFile(path));
    }

    private static (string Key, string Value)[] Bound(FiscalEnvironment environment)
    {
        var (zimraServer, verificationServer) = FiscalEndpoints.For(environment);
        return
        [
            ("ZIMRASERVER", zimraServer),
            ("VERIFICATIONSERVER", verificationServer),
        ];
    }

    // ---- A correctly bound device is allowed ----

    [Theory]
    [InlineData(FiscalEnvironment.Test)]
    [InlineData(FiscalEnvironment.Production)]
    public async Task A_device_bound_consistently_to_its_environment_is_allowed(FiscalEnvironment environment)
    {
        var gateway = Gateway(Bound(environment), environment);

        await gateway.EnsureEnvironmentSafetyAsync();
    }

    // ---- A test device must never reach production ----

    [Fact]
    public async Task A_test_bound_device_pointed_at_the_production_api_is_refused()
    {
        var gateway = Gateway(
        [
            ("ZIMRASERVER", ProdApi),
            ("VERIFICATIONSERVER", TestVerification),
        ], FiscalEnvironment.Test);

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }

    [Fact]
    public async Task A_test_bound_device_pointed_at_the_production_verification_host_is_refused()
    {
        var gateway = Gateway(
        [
            ("ZIMRASERVER", TestApi),
            ("VERIFICATIONSERVER", ProdVerification),
        ], FiscalEnvironment.Test);

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }

    /// <summary>
    /// QRURL is the address a customer scans on the receipt. The engine recomputes it from
    /// the verification host, but a stale production value left on disk would still put a
    /// live link on a test receipt if anything ever preferred it.
    /// </summary>
    [Fact]
    public async Task A_test_bound_device_with_a_production_receipt_qr_host_is_refused()
    {
        var gateway = Gateway(
        [
            ("ZIMRASERVER", TestApi),
            ("VERIFICATIONSERVER", TestVerification),
            ("QRURL", ProdVerification),
        ], FiscalEnvironment.Test);

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }

    // ---- Test hosts must never be used as if they were production ----

    [Fact]
    public async Task A_production_declared_device_pointed_at_a_test_host_is_refused()
    {
        var gateway = Gateway(Bound(FiscalEnvironment.Test), FiscalEnvironment.Production);

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }

    // ---- Missing configuration is not a safe default ----

    /// <summary>
    /// The engine falls back to its own built-in value when a host key is absent, and that
    /// fallback is PRODUCTION. So an unset ZIMRASERVER on a test-bound device is the most
    /// dangerous state there is, and it must be refused rather than resolved optimistically.
    /// </summary>
    [Theory]
    [InlineData("ZIMRASERVER")]
    [InlineData("VERIFICATIONSERVER")]
    public async Task A_test_bound_device_with_a_missing_host_is_refused(string missingKey)
    {
        var settings = Bound(FiscalEnvironment.Test).Where(s => s.Key != missingKey).ToArray();
        var gateway = Gateway(settings, FiscalEnvironment.Test);

        var ex = await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());

        Assert.Contains(missingKey, ex.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task A_device_with_no_host_configuration_at_all_is_refused()
    {
        var gateway = Gateway([("SERIALNUMBER", "HOYOTEST")], FiscalEnvironment.Test);

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }

    // ---- Unknown hosts are not guessed ----

    [Theory]
    [InlineData("https://fdms.example.invalid")]
    [InlineData("https://127.0.0.1:8443")]
    [InlineData("not-a-url")]
    public async Task An_unrecognised_host_is_refused_rather_than_assumed(string host)
    {
        var gateway = Gateway(
        [
            ("ZIMRASERVER", host),
            ("VERIFICATIONSERVER", TestVerification),
        ], FiscalEnvironment.Test);

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }

    // ---- A declared test device is pinned even if the hosts agree with each other ----

    /// <summary>
    /// Both directions matter. A device declared as a test device must not transact against
    /// production even if its hosts were somehow left pointing there, which is the failure
    /// that would do irreversible damage.
    /// </summary>
    [Fact]
    public async Task A_device_declared_as_a_test_device_is_pinned_to_test_hosts()
    {
        var gateway = Gateway(
        [
            ("ZIMRASERVER", ProdApi),
            ("VERIFICATIONSERVER", ProdVerification),
            ("SERIALNUMBER", "HOYOTEST"),
        ], FiscalEnvironment.Production, isTestDevice: true, serialNumber: "HOYOTEST");

        await Assert.ThrowsAsync<FiscalisationException>(() => gateway.EnsureEnvironmentSafetyAsync());
    }
}
