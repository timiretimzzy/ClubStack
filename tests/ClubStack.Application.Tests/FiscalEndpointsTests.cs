using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;

namespace ClubStack.Application.Tests;

/// <summary>
/// The FDMS environment split is the single most safety-critical mapping in D5.5.
///
/// Two separate hosts serve each environment: a device API host the engine calls for
/// registration and fiscalisation, and a receipt verification host used for the QR
/// deep-link. They are NOT the same host, and the test pair differ from each other.
///
/// Collapsing the test pair onto one host is not cosmetic: FDMS returns HTTP 404 for
/// device routes on the verification host, so a test device silently fails to register.
/// These tests pin the four authorities so that regression fails loudly here instead of
/// surfacing as an opaque 404 during provisioning.
/// </summary>
public sealed class FiscalEndpointsTests
{
    [Fact]
    public void Test_environment_uses_the_test_api_host_for_the_device()
        => Assert.Equal("https://fdmsapitest.zimra.co.zw", FiscalEndpoints.For(FiscalEnvironment.Test).ZimraServer);

    [Fact]
    public void Test_environment_uses_the_test_verification_host_for_receipts()
        => Assert.Equal("https://fdmstest.zimra.co.zw", FiscalEndpoints.For(FiscalEnvironment.Test).VerificationServer);

    [Fact]
    public void Test_environment_does_not_use_the_verification_host_for_device_calls()
    {
        var (zimraServer, verificationServer) = FiscalEndpoints.For(FiscalEnvironment.Test);

        Assert.NotEqual(zimraServer, verificationServer);
    }

    [Fact]
    public void Production_environment_uses_the_live_api_host_for_the_device()
        => Assert.Equal("https://fdmsapi.zimra.co.zw", FiscalEndpoints.For(FiscalEnvironment.Production).ZimraServer);

    [Fact]
    public void Production_environment_uses_the_live_verification_host_for_receipts()
        => Assert.Equal("https://fdms.zimra.co.zw", FiscalEndpoints.For(FiscalEnvironment.Production).VerificationServer);

    [Fact]
    public void Every_test_host_is_recognised_as_test()
    {
        var (zimraServer, verificationServer) = FiscalEndpoints.For(FiscalEnvironment.Test);

        Assert.True(FiscalEndpoints.IsTestHost(zimraServer));
        Assert.True(FiscalEndpoints.IsTestHost(verificationServer));
    }

    [Fact]
    public void Every_production_host_is_recognised_as_production()
    {
        var (zimraServer, verificationServer) = FiscalEndpoints.For(FiscalEnvironment.Production);

        Assert.True(FiscalEndpoints.IsProductionHost(zimraServer));
        Assert.True(FiscalEndpoints.IsProductionHost(verificationServer));
    }

    /// <summary>
    /// The safety guard refuses a host it cannot classify, so the two predicates must
    /// never both claim the same authority. "fdmsapitest" contains neither
    /// "fdmsapi.zimra.co.zw" nor "fdms.zimra.co.zw" as a substring, but a host such as
    /// "fdmstest" once did, which is how a test host could pass a production check.
    /// </summary>
    [Theory]
    [InlineData("https://fdmsapitest.zimra.co.zw")]
    [InlineData("https://fdmstest.zimra.co.zw")]
    [InlineData("https://fdmsapi.zimra.co.zw")]
    [InlineData("https://fdms.zimra.co.zw")]
    public void A_known_host_is_never_both_test_and_production(string url)
    {
        var isTest = FiscalEndpoints.IsTestHost(url);
        var isProduction = FiscalEndpoints.IsProductionHost(url);

        Assert.NotEqual(isTest, isProduction);
    }

    [Theory]
    [InlineData("https://fdmsapitest.zimra.co.zw", FiscalEnvironment.Test)]
    [InlineData("https://fdmstest.zimra.co.zw", FiscalEnvironment.Test)]
    [InlineData("https://fdmsapi.zimra.co.zw", FiscalEnvironment.Production)]
    [InlineData("https://fdms.zimra.co.zw", FiscalEnvironment.Production)]
    public void A_known_host_resolves_to_its_environment(string url, FiscalEnvironment expected)
        => Assert.Equal(expected, FiscalEndpoints.EnvironmentOf(url));

    [Theory]
    [InlineData("https://example.invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unrecognised_host_is_not_guessed(string? url)
    {
        Assert.False(FiscalEndpoints.IsTestHost(url));
        Assert.False(FiscalEndpoints.IsProductionHost(url));
        Assert.Null(FiscalEndpoints.EnvironmentOf(url));
    }

    [Fact]
    public void The_engine_environment_token_matches_what_the_engine_persists()
    {
        Assert.Equal("test", FiscalEndpoints.EngineToken(FiscalEnvironment.Test));
        Assert.Equal("production", FiscalEndpoints.EngineToken(FiscalEnvironment.Production));
    }

    /// <summary>
    /// The engine derives its own environment from whether the API host name contains
    /// "test", and selects its tax table from that decision. If any test host lost that
    /// marker the device would silently fiscalise against production tax IDs.
    /// </summary>
    [Theory]
    [InlineData("https://fdmsapitest.zimra.co.zw")]
    [InlineData("https://fdmstest.zimra.co.zw")]
    public void Every_test_api_host_keeps_the_marker_the_engine_tests_for(string url)
        => Assert.Contains(FiscalEndpoints.TestHostMarker, url, StringComparison.OrdinalIgnoreCase);
}
