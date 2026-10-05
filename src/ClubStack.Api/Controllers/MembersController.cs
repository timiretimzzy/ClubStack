using ClubStack.Api.Contracts;
using ClubStack.Application.Accounts;
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
/// Member directory: member search, the Member 360 profile (Phase 5 §27/§28) and
/// dependant relationships (Phase 2 MEM-012/MEM-013). Lifecycle transitions are
/// exposed on the memberships controller. The member account relationship
/// (ACC-001) resolves through this resource as part of the Member 360 view.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/members")]
[Authorize]
public sealed class MembersController : AuditedControllerBase
{
    private readonly IMembershipDirectoryService _directory;
    private readonly IMembershipLifecycleService _lifecycle;
    private readonly IMemberAccountService _accounts;

    public MembersController(
        IMembershipDirectoryService directory,
        IMembershipLifecycleService lifecycle,
        IMemberAccountService accounts,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _directory = directory;
        _lifecycle = lifecycle;
        _accounts = accounts;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(MemberSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _directory.SearchMembersAsync(new MemberSearchInput(query, status, page, pageSize), cancellationToken);
        return Ok(new MemberSearchResponse
        {
            Items = result.Items.Select(ToMemberResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(MemberDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var member = await _directory.GetMemberAsync(id, cancellationToken);
        return Ok(ToDetailResponse(member));
    }

    [HttpGet("{id:guid}/account")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsAccountView)]
    [ProducesResponseType(typeof(MemberAccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Account(Guid id, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAccountForMemberAsync(id, cancellationToken);
        return Ok(MemberAccountsController.ToAccountResponse(account));
    }

    [HttpGet("{id:guid}/memberships")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Memberships(Guid id, CancellationToken cancellationToken)
    {
        var memberships = await _lifecycle.GetMembershipsForMemberAsync(id, cancellationToken);
        return Ok(memberships.Select(ToMembershipResponse).ToList());
    }

    [HttpGet("{id:guid}/dependants")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipDependantResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDependants(Guid id, CancellationToken cancellationToken)
    {
        var dependants = await _directory.ListDependantsAsync(id, cancellationToken);
        return Ok(dependants.Select(ToDependantResponse).ToList());
    }

    [HttpPost("{id:guid}/dependants")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipDependantManage)]
    [ProducesResponseType(typeof(MembershipDependantResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDependant(Guid id, [FromBody] AddDependantRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var dependant = await _directory.AddDependantAsync(
            id,
            new AddDependantInput(request.DependantPersonId, request.RelationType, request.Notes),
            cancellationToken);

        await RecordAuditAsync(
            "Membership.Dependant.Add",
            "MembershipDependant",
            dependant.Id.ToString(),
            dependant.DependantName,
            cancellationToken: cancellationToken);

        return Ok(ToDependantResponse(dependant));
    }

    [HttpDelete("{id:guid}/dependants/{dependantPersonId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipDependantManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDependant(Guid id, Guid dependantPersonId, CancellationToken cancellationToken)
    {
        await _directory.RemoveDependantAsync(id, dependantPersonId, cancellationToken);

        await RecordAuditAsync(
            "Membership.Dependant.Remove",
            "MembershipDependant",
            dependantPersonId.ToString(),
            cancellationToken: cancellationToken);

        return NoContent();
    }

    internal static MemberResponse ToMemberResponse(MemberDto m) => new()
    {
        Id = m.Id,
        PersonId = m.PersonId,
        LegalName = m.LegalName,
        PreferredName = m.PreferredName,
        MemberNumber = m.MemberNumber,
        Status = m.Status,
        Phone = m.Phone,
        Email = m.Email,
    };

    private static MemberDetailResponse ToDetailResponse(MemberDetailDto m) => new()
    {
        Id = m.Id,
        PersonId = m.PersonId,
        LegalName = m.LegalName,
        PreferredName = m.PreferredName,
        MemberNumber = m.MemberNumber,
        Status = m.Status.ToString(),
        Person = PersonsController.ToResponse(m.Person),
        Memberships = m.Memberships.Select(MembershipsController.ToMembershipResponse).ToList(),
        Dependants = m.Dependants.Select(ToDependantResponse).ToList(),
    };

    internal static MembershipDependantResponse ToDependantResponse(MembershipDependantDto d) => new()
    {
        Id = d.Id,
        MemberId = d.MemberId,
        DependantPersonId = d.DependantPersonId,
        DependantName = d.DependantName,
        RelationType = d.RelationType,
        Notes = d.Notes,
    };

    internal static MembershipResponse ToMembershipResponse(MembershipDto m) => new()
    {
        Id = m.Id,
        MemberId = m.MemberId,
        CategoryId = m.CategoryId,
        CategoryName = m.CategoryName,
        Status = m.Status.ToString(),
        StartDateUtc = m.StartDateUtc,
        EndDateUtc = m.EndDateUtc,
        RenewalDateUtc = m.RenewalDateUtc,
        ApprovedByUserId = m.ApprovedByUserId,
        ApprovedAtUtc = m.ApprovedAtUtc,
        ActivatedAtUtc = m.ActivatedAtUtc,
        CreatedAtUtc = m.CreatedAtUtc,
    };
}