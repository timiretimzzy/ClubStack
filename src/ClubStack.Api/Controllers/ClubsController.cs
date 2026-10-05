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

/// <summary>Club profile administration (D1 Platform, Phase 8 §8.1 <c>clubs</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/clubs")]
[Authorize]
public sealed class ClubsController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public ClubsController(
        IPlatformConfigurationStore store,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PlatformClubView)]
    [ProducesResponseType(typeof(IReadOnlyList<ClubResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var clubs = await _store.GetClubsAsync(cancellationToken);
        return Ok(clubs.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformClubView)]
    [ProducesResponseType(typeof(ClubResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var club = await _store.GetClubAsync(id, cancellationToken);
        return Ok(ToResponse(club));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformClubEdit)]
    [ProducesResponseType(typeof(ClubResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ClubRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var club = await _store.CreateClubAsync(ToInput(request), cancellationToken);

        await RecordAuditAsync(
            action: "Platform.Club.Create",
            entityType: "Club",
            entityId: club.Id.ToString(),
            reference: club.Code,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(club));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformClubEdit)]
    [ProducesResponseType(typeof(ClubResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ClubRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var club = await _store.UpdateClubAsync(id, ToInput(request), cancellationToken);

        await RecordAuditAsync(
            action: "Platform.Club.Update",
            entityType: "Club",
            entityId: club.Id.ToString(),
            reference: club.Code,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(club));
    }

    private static ClubInput ToInput(ClubRequest request) => new(
        request.Name,
        request.Code,
        request.AddressLine1,
        request.AddressLine2,
        request.City,
        request.Region,
        request.PostalCode,
        request.Country,
        request.Phone,
        request.Email,
        request.IsActive);

    private static ClubResponse ToResponse(ClubDto club) => new()
    {
        Id = club.Id,
        Name = club.Name,
        Code = club.Code,
        AddressLine1 = club.AddressLine1,
        AddressLine2 = club.AddressLine2,
        City = club.City,
        Region = club.Region,
        PostalCode = club.PostalCode,
        Country = club.Country,
        Phone = club.Phone,
        Email = club.Email,
        IsActive = club.IsActive,
        CreatedAtUtc = club.CreatedAtUtc,
        UpdatedAtUtc = club.UpdatedAtUtc,
    };
}