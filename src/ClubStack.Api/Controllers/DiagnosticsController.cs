using System.Diagnostics;
using System.Reflection;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Technical diagnostics for authorized operators. All values are non-sensitive;
/// credentials, connection strings and secrets are never exposed.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix)]
[Authorize(Policy = $"Permission:{Permissions.PlatformDiagnosticsView}")]
public sealed class DiagnosticsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ICorrelationContext _correlation;
    private readonly HealthCheckService _healthChecks;

    private static readonly DateTimeOffset ProcessStartTimeUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public DiagnosticsController(
        IWebHostEnvironment environment,
        ICorrelationContext correlation,
        HealthCheckService healthChecks)
    {
        _environment = environment;
        _correlation = correlation;
        _healthChecks = healthChecks;
    }

    [HttpGet("diagnostics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Diagnostics(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);

        return Ok(new
        {
            application = "ClubStack",
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            environment = _environment.EnvironmentName,
            machineName = Environment.MachineName,
            processStartUtc = ProcessStartTimeUtc,
            uptime = DateTimeOffset.UtcNow - ProcessStartTimeUtc,
            database = new
            {
                status = report.Entries.TryGetValue("database", out var db) ? db.Status.ToString() : "unknown",
            },
            correlationId = _correlation.CorrelationId,
        });
    }
}