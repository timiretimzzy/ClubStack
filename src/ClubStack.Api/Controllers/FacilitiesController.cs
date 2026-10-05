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

/// <summary>
/// Facility base configuration (D1 Platform, Phase 8 §8.1 <c>facilities</c>).
/// Only the registrar is established here; the reservation engine ships later.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/facilities")]
[Authorize]
public sealed class FacilitiesController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public FacilitiesController(
        IPlatformConfigurationStore store,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PlatformFacilityView)]
    [ProducesResponseType(typeof(IReadOnlyList<FacilityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetFacilitiesAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformFacilityView)]
    [ProducesResponseType(typeof(FacilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetFacilityAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformFacilityManage)]
    [ProducesResponseType(typeof(FacilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] FacilityRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var clubId = await ResolvePrimaryClubIdAsync(_store, cancellationToken);
        var row = await _store.CreateFacilityAsync(clubId, new FacilityInput(
            request.Name, request.Code, request.Description, request.Capacity, request.IsActive), cancellationToken);

        await RecordAuditAsync("Platform.Facility.Create", "Facility", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformFacilityManage)]
    [ProducesResponseType(typeof(FacilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] FacilityRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdateFacilityAsync(id, new FacilityInput(
            request.Name, request.Code, request.Description, request.Capacity, request.IsActive), cancellationToken);

        await RecordAuditAsync("Platform.Facility.Update", "Facility", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static FacilityResponse ToResponse(FacilityDto d) => new()
    {
        Id = d.Id,
        ClubId = d.ClubId,
        Name = d.Name,
        Code = d.Code,
        Description = d.Description,
        Capacity = d.Capacity,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}