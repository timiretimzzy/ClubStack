using ClubStack.Api.Contracts;
using ClubStack.Application.Authorization;
using ClubStack.Application.Identity;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubStack.Api.Controllers;

/// <summary>Permission catalogue (D1 Identity, Phase 8 §8.2 <c>permissions</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/permissions")]
[Authorize]
public sealed class PermissionsController : ControllerBase
{
    private readonly IIdentityAdministrationService _service;

    public PermissionsController(IIdentityAdministrationService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.IdentityPermissionView)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var permissions = await _service.GetPermissionsAsync(cancellationToken);
        return Ok(permissions.Select(p => new PermissionResponse
        {
            Key = p.Key,
            Module = p.Module,
            Description = p.Description,
            IsSensitive = p.IsSensitive,
        }).ToList());
    }
}