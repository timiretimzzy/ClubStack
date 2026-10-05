using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Membership;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Household grouping of related persons (Phase 8 §8.3 <c>households</c>,
/// Phase 3 §8/§9). Roles are explicit relationship values, never inferred.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/households")]
[Authorize]
public sealed class HouseholdsController : AuditedControllerBase
{
    private readonly IMembershipDirectoryService _directory;

    public HouseholdsController(
        IMembershipDirectoryService directory,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _directory = directory;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(IReadOnlyList<HouseholdResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var households = await _directory.ListHouseholdsAsync(cancellationToken);
        return Ok(households.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(HouseholdResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var household = await _directory.GetHouseholdAsync(id, cancellationToken);
        return Ok(ToResponse(household));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.MembershipHouseholdManage)]
    [ProducesResponseType(typeof(HouseholdResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] HouseholdRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var household = await _directory.CreateHouseholdAsync(new HouseholdInput(request.Name, request.PrimaryMemberId), cancellationToken);

        await RecordAuditAsync(
            "Membership.Household.Create",
            "Household",
            household.Id.ToString(),
            household.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(household));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipHouseholdManage)]
    [ProducesResponseType(typeof(HouseholdResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] HouseholdRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var household = await _directory.UpdateHouseholdAsync(id, new HouseholdInput(request.Name, request.PrimaryMemberId), cancellationToken);

        await RecordAuditAsync(
            "Membership.Household.Update",
            "Household",
            household.Id.ToString(),
            household.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(household));
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipHouseholdManage)]
    [ProducesResponseType(typeof(HouseholdResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddHouseholdMemberRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var household = await _directory.AddHouseholdMemberAsync(id, new AddHouseholdMemberInput(request.PersonId, request.Role), cancellationToken);

        await RecordAuditAsync(
            "Membership.Household.AddMember",
            "Household",
            household.Id.ToString(),
            household.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(household));
    }

    [HttpDelete("{id:guid}/members/{personId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipHouseholdManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid personId, CancellationToken cancellationToken)
    {
        await _directory.RemoveHouseholdMemberAsync(id, personId, cancellationToken);

        await RecordAuditAsync(
            "Membership.Household.RemoveMember",
            "Household",
            id.ToString(),
            personId.ToString(),
            cancellationToken: cancellationToken);

        return NoContent();
    }

    internal static HouseholdResponse ToResponse(HouseholdDto h) => new()
    {
        Id = h.Id,
        Name = h.Name,
        Reference = h.Reference,
        PrimaryMemberId = h.PrimaryMemberId,
        PrimaryMemberName = h.PrimaryMemberName,
        PrimaryMemberNumber = h.PrimaryMemberNumber,
        IsActive = h.IsActive,
        Members = h.Members.Select(ToMemberResponse).ToList(),
    };

    private static HouseholdMemberResponse ToMemberResponse(HouseholdMemberDto m) => new()
    {
        HouseholdId = m.HouseholdId,
        PersonId = m.PersonId,
        PersonName = m.PersonName,
        MemberNumber = m.MemberNumber,
        Role = m.Role,
    };
}