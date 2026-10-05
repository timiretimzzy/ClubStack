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

/// <summary>Location administration (D1 Platform, Phase 8 §8.1 <c>locations</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/locations")]
[Authorize]
public sealed class LocationsController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public LocationsController(
        IPlatformConfigurationStore store,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PlatformLocationView)]
    [ProducesResponseType(typeof(IReadOnlyList<LocationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetLocationsAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformLocationView)]
    [ProducesResponseType(typeof(LocationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetLocationAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformLocationManage)]
    [ProducesResponseType(typeof(LocationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] LocationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var clubId = await ResolvePrimaryClubIdAsync(_store, cancellationToken);
        var row = await _store.CreateLocationAsync(clubId, new LocationInput(
            request.Name, request.Code, request.AddressLine1, request.AddressLine2, request.City, request.IsActive), cancellationToken);

        await RecordAuditAsync("Platform.Location.Create", "Location", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformLocationManage)]
    [ProducesResponseType(typeof(LocationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] LocationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdateLocationAsync(id, new LocationInput(
            request.Name, request.Code, request.AddressLine1, request.AddressLine2, request.City, request.IsActive), cancellationToken);

        await RecordAuditAsync("Platform.Location.Update", "Location", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static LocationResponse ToResponse(LocationDto d) => new()
    {
        Id = d.Id,
        ClubId = d.ClubId,
        Name = d.Name,
        Code = d.Code,
        AddressLine1 = d.AddressLine1,
        AddressLine2 = d.AddressLine2,
        City = d.City,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}