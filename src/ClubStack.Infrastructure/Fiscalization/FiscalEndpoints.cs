using ClubStack.Hardware.Fiscalization;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// The single mapping from a <see cref="FiscalEnvironment"/> to the FDMS hosts a device
/// may contact.
///
/// The engine composes every FDMS call as
/// <c>{ZIMRASERVER}/{deviceId}/{method}</c> for registration and
/// <c>{ZIMRASERVER}/Device/v1/{deviceId}/{method}</c> for transactional calls, and uses
/// <c>VERIFICATIONSERVER</c> for receipt QR deep-links. It also derives its own
/// environment from whether <c>ZIMRASERVER</c> names a test host, and derives the tax
/// identifiers for that environment from the same decision. Persisting these two values
/// is therefore what actually binds a device to an environment.
/// </summary>
public static class FiscalEndpoints
{
    /// <summary>Substring that identifies any FDMS test host.</summary>
    public const string TestHostMarker = "test";

    /// <summary>Device API host that serves FDMS test.</summary>
    public const string TestZimraServer = "fdmsapitest.zimra.co.zw";

    /// <summary>Receipt verification host that serves FDMS test.</summary>
    public const string TestVerificationServer = "fdmstest.zimra.co.zw";

    /// <summary>Device API host that serves live FDMS.</summary>
    public const string ProductionZimraServer = "fdmsapi.zimra.co.zw";

    /// <summary>Receipt verification host that serves live FDMS.</summary>
    public const string ProductionVerificationServer = "fdms.zimra.co.zw";

    /// <summary>Hosts that serve live FDMS. A test device must never be routed to one.</summary>
    private static readonly string[] ProductionHosts =
    [
        ProductionVerificationServer,
        ProductionZimraServer,
    ];

    /// <summary>Hosts that serve FDMS test.</summary>
    private static readonly string[] TestHosts =
    [
        TestVerificationServer,
        TestZimraServer,
    ];

    /// <summary>
    /// Resolves the device API host and the receipt verification host for an environment.
    /// </summary>
    public static (string ZimraServer, string VerificationServer) For(FiscalEnvironment environment)
        => environment switch
        {
            FiscalEnvironment.Test => ("https://" + TestZimraServer, "https://" + TestVerificationServer),
            _ => ("https://" + ProductionZimraServer, "https://" + ProductionVerificationServer),
        };

    /// <summary>Whether a host serves FDMS test.</summary>
    public static bool IsTestHost(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && TestHosts.Any(h => url.Contains(h, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a host serves live FDMS.</summary>
    public static bool IsProductionHost(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && ProductionHosts.Any(h => url.Contains(h, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The environment a host belongs to, or <c>null</c> when the host is neither a known
    /// test nor a known production host. An unrecognised host is deliberately not guessed:
    /// the safety guard refuses to proceed rather than assuming.
    /// </summary>
    public static FiscalEnvironment? EnvironmentOf(string? url)
    {
        if (IsTestHost(url))
        {
            return FiscalEnvironment.Test;
        }

        return IsProductionHost(url) ? FiscalEnvironment.Production : null;
    }

    /// <summary>
    /// The environment token the engine persists (<c>LAST_ENVIRONMENT</c>). Matches the
    /// values the engine itself writes so ClubStack and the engine never disagree.
    /// </summary>
    public static string EngineToken(FiscalEnvironment environment)
        => environment == FiscalEnvironment.Test ? "test" : "production";
}
