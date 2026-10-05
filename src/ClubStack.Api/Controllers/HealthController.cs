using System.Reflection;
using ClubStack.Application.Correlation;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Application health information. Anonymous by design (liveness); the database
/// probe is exposed through the readiness endpoint (<c>/health/ready</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix)]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly ICorrelationContext _correlation;

    public HealthController(ICorrelationContext correlation)
    {
        _correlation = correlation;
    }

    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new
    {
        status = "Healthy",
        application = "ClubStack",
        version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
        correlationId = _correlation.CorrelationId,
    });
}