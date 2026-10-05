using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Platform;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>System configuration key/value administration (D1 Platform, Phase 8 §8.1 <c>system_configurations</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/system-configuration")]
[Authorize]
public sealed class SystemConfigurationController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public SystemConfigurationController(
        IPlatformConfigurationStore store,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(IReadOnlyList<SystemConfigurationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetSystemConfigurationsAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{key}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(SystemConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
    {
        var row = await _store.GetSystemConfigurationAsync(key, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationEdit)]
    [ProducesResponseType(typeof(SystemConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Set([FromBody] SystemConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var row = await _store.SetSystemConfigurationAsync(
            new SystemConfigurationInput(request.Key, request.Value, request.ValueType, request.Description),
            actorUserId,
            cancellationToken);

        await RecordAuditAsync("Platform.Configuration.Set", "SystemConfiguration", row.Key, row.Key, metadataJson: row.Value, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static SystemConfigurationResponse ToResponse(SystemConfigurationDto d) => new()
    {
        Key = d.Key,
        Value = d.Value,
        ValueType = d.ValueType,
        Description = d.Description,
        IsSystem = d.IsSystem,
        UpdatedByUserId = d.UpdatedByUserId,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}