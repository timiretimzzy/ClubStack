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

/// <summary>Outlet administration (D1 Platform, Phase 8 §8.1 <c>outlets</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/outlets")]
[Authorize]
public sealed class OutletsController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public OutletsController(
        IPlatformConfigurationStore store,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PlatformOutletView)]
    [ProducesResponseType(typeof(IReadOnlyList<OutletResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetOutletsAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformOutletView)]
    [ProducesResponseType(typeof(OutletResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetOutletAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformOutletManage)]
    [ProducesResponseType(typeof(OutletResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] OutletRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var clubId = await ResolvePrimaryClubIdAsync(_store, cancellationToken);
        var row = await _store.CreateOutletAsync(clubId, new OutletInput(request.Name, request.Code, request.IsActive), cancellationToken);

        await RecordAuditAsync("Platform.Outlet.Create", "Outlet", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformOutletManage)]
    [ProducesResponseType(typeof(OutletResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] OutletRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdateOutletAsync(id, new OutletInput(request.Name, request.Code, request.IsActive), cancellationToken);

        await RecordAuditAsync("Platform.Outlet.Update", "Outlet", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static OutletResponse ToResponse(OutletDto d) => new()
    {
        Id = d.Id,
        ClubId = d.ClubId,
        Name = d.Name,
        Code = d.Code,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}