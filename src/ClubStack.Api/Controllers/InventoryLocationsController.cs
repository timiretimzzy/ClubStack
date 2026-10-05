using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Inventory;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Stock locations (Phase 2 INV-020, Phase 3 §30). Locations are the physical
/// places stock can be held; an inactive location still explains history but
/// refuses new stock-changing operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/inventory-locations")]
[Authorize]
public sealed class InventoryLocationsController : AuditedControllerBase
{
    private readonly IInventoryLocationService _locations;

    public InventoryLocationsController(
        IInventoryLocationService locations,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _locations = locations;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockView)]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryLocationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var locations = await _locations.ListAsync(includeInactive, cancellationToken);
        return Ok(locations.Select(ToLocationResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.InventoryLocationManage)]
    [ProducesResponseType(typeof(InventoryLocationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateInventoryLocationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var location = await _locations.CreateAsync(
            new CreateInventoryLocationInput(request.Code, request.Name, request.Type),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Inventory.Location.Create",
            "InventoryLocation",
            location.Id.ToString(),
            location.Code,
            cancellationToken: cancellationToken);

        return Ok(ToLocationResponse(location));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryLocationManage)]
    [ProducesResponseType(typeof(InventoryLocationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInventoryLocationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var location = await _locations.UpdateAsync(
            id,
            new UpdateInventoryLocationInput(request.Name, request.Type, request.IsActive),
            ActorUserId(),
            cancellationToken);

        return Ok(ToLocationResponse(location));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static InventoryLocationResponse ToLocationResponse(InventoryLocationDto l) => new()
    {
        Id = l.Id,
        Code = l.Code,
        Name = l.Name,
        Type = l.Type,
        Status = l.Status,
        CreatedAtUtc = l.CreatedAtUtc,
        UpdatedAtUtc = l.UpdatedAtUtc,
    };
}