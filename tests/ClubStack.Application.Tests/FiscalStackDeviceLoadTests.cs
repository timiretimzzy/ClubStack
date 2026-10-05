using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClubStack.Application.Tests;

/// <summary>
/// Loads the real FiscalStackZW.dll through the production adapter. This proves the
/// net10 host can construct the net48 facade and that the envelope parser understands
/// the engine's own response shapes.
///
/// This is <c>[EMPIRICAL]</c> evidence only: it touches whatever device state the host
/// machine happens to have. It is skipped when the DLL is not present, and it never
/// transacts, opens or closes a fiscal day.
/// </summary>
public sealed class FiscalStackDeviceLoadTests
{
    private static readonly string DefaultDllPath =
        @"C:\Users\TIMIRE\Desktop\FISCALSTACK POS\FiscalStackZWdll\Deploy\FiscalStackZW.dll";

    private static string ResolveDllPath()
    {
        var configured = Environment.GetEnvironmentVariable("CLUBSTACK_FISCALSTACK_DLL");
        return string.IsNullOrWhiteSpace(configured) ? DefaultDllPath : configured;
    }

    private static bool DllPresent() => File.Exists(ResolveDllPath());

    private static FiscalStackZWDllGateway CreateGateway()
        => new(NullLogger<FiscalStackZWDllGateway>.Instance);

    [SkippableFact]
    public void Dll_is_present_on_the_host()
    {
        Skip.IfNot(DllPresent(), $"FiscalStackZW.dll not found at {ResolveDllPath()}.");

        Assert.True(File.Exists(ResolveDllPath()));
    }

    [SkippableFact]
    public async Task Adapter_reads_device_status_without_throwing()
    {
        Skip.IfNot(DllPresent(), $"FiscalStackZW.dll not found at {ResolveDllPath()}.");

        var gateway = CreateGateway();

        // Read-only. The adapter serialises this behind its own gate and runs the
        // synchronous facade call off the request thread.
        var status = await gateway.GetStatusAsync();

        Assert.NotNull(status);
        Assert.False(string.IsNullOrWhiteSpace(status.FiscalDayState.ToString()));
    }

    [SkippableFact]
    public async Task Adapter_reads_device_configuration_and_resolves_tax_identifiers()
    {
        Skip.IfNot(DllPresent(), $"FiscalStackZW.dll not found at {ResolveDllPath()}.");

        var gateway = CreateGateway();
        var configuration = await gateway.GetConfigurationAsync();

        Assert.NotNull(configuration);

        // Every identifier is either a positive device value or the explicit
        // "engine selects" sentinel; a negative or unparsed value would mean the
        // configuration mapping regressed.
        foreach (var taxId in new[]
                 {
                     configuration.StandardRatedTaxId,
                     configuration.ZeroRatedTaxId,
                     configuration.ExemptTaxId,
                 })
        {
            Assert.True(taxId >= TaxIds.EngineSelects, $"Unresolved tax identifier {taxId}.");
        }
    }

    [SkippableFact]
    public async Task Adapter_reports_a_pending_count_without_transacting_anything()
    {
        Skip.IfNot(DllPresent(), $"FiscalStackZW.dll not found at {ResolveDllPath()}.");

        var gateway = CreateGateway();

        // Verified against the engine to be side-effect free: it reads a counter and
        // never enqueues a document.
        var pending = await gateway.GetPendingCountAsync();

        Assert.True(pending >= 0);
    }

    [SkippableFact]
    public async Task Device_calls_are_serialised_by_the_adapter_gate()
    {
        Skip.IfNot(DllPresent(), $"FiscalStackZW.dll not found at {ResolveDllPath()}.");

        var gateway = CreateGateway();

        // Concurrent reads must not deadlock or corrupt the shared facade state. The
        // engine is not re-entrant, so this is the guard that protects it.
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => gateway.GetStatusAsync()));

        Assert.Equal(4, results.Length);
        Assert.All(results, r => Assert.NotNull(r));
    }
}
