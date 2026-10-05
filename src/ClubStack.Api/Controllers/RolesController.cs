using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Identity;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>Role and role-permission administration (D1 Identity, Phase 8 §8.2 <c>roles</c>/<c>role_permissions</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/roles")]
[Authorize]
public sealed class RolesController : AuditedControllerBase
{
    private readonly IIdentityAdministrationService _service;

    public RolesController(
        IIdentityAdministrationService service,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.IdentityRoleView)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var roles = await _service.GetRolesAsync(cancellationToken);
        return Ok(roles.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityRoleView)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var role = await _service.GetRoleAsync(id, cancellationToken);
        return Ok(ToResponse(role));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.IdentityRoleManage)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var role = await _service.CreateRoleAsync(request.Name, request.Permissions, cancellationToken);
        await RecordAuditAsync("Identity.Role.Create", "Role", role.Id.ToString(), role.Name, cancellationToken: cancellationToken);
        return Ok(ToResponse(role));
    }

    [HttpPut("{id:guid}/permissions")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityRoleManage)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePermissions(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _service.UpdateRolePermissionsAsync(id, request.Permissions, cancellationToken);
        await RecordAuditAsync(
            "Identity.Role.Permissions.Update",
            "Role",
            role.Id.ToString(),
            role.Name,
            metadataJson: System.Text.Json.JsonSerializer.Serialize(new { Permissions = request.Permissions }),
            cancellationToken: cancellationToken);
        return Ok(ToResponse(role));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityRoleManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteRoleAsync(id, cancellationToken);
        await RecordAuditAsync("Identity.Role.Delete", "Role", id.ToString(), cancellationToken: cancellationToken);
        return NoContent();
    }

    private static RoleResponse ToResponse(RoleDto d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        IsSystem = d.IsSystem,
        Permissions = d.Permissions,
    };
}