using System.Security.Claims;
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
/// Membership period lifecycle (Phase 8 §7.3): activation, suspension,
/// reinstatement, renewal, cancellation and transfer. Every transition is a
/// validated command recorded in membership status history.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/memberships")]
[Authorize]
public sealed class MembershipsController : AuditedControllerBase
{
    private readonly IMembershipLifecycleService _lifecycle;

    public MembershipsController(
        IMembershipLifecycleService lifecycle,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _lifecycle = lifecycle;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var membership = await _lifecycle.GetMembershipAsync(id, cancellationToken);
        return Ok(ToMembershipResponse(membership));
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipStatusHistoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var history = await _lifecycle.GetMembershipHistoryAsync(id, cancellationToken);
        return Ok(history.Select(ToHistoryResponse).ToList());
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberActivate)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var membership = await _lifecycle.ActivateMembershipAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Membership.Activate",
            "Membership",
            membership.Id.ToString(),
            membership.CategoryName,
            cancellationToken: cancellationToken);

        return Ok(ToMembershipResponse(membership));
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberSuspend)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] MembershipTransitionRequest request, CancellationToken cancellationToken)
    {
        var membership = await _lifecycle.SuspendMembershipAsync(id, request.Reason, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Membership.Suspend",
            "Membership",
            membership.Id.ToString(),
            membership.CategoryName,
            cancellationToken: cancellationToken);

        return Ok(ToMembershipResponse(membership));
    }

    [HttpPost("{id:guid}/reinstate")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberReinstate)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reinstate(Guid id, [FromBody] MembershipTransitionRequest request, CancellationToken cancellationToken)
    {
        var membership = await _lifecycle.ReinstateMembershipAsync(id, request.Reason, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Membership.Reinstate",
            "Membership",
            membership.Id.ToString(),
            membership.CategoryName,
            cancellationToken: cancellationToken);

        return Ok(ToMembershipResponse(membership));
    }

    [HttpPost("{id:guid}/renew")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberRenew)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Renew(Guid id, CancellationToken cancellationToken)
    {
        var membership = await _lifecycle.RenewMembershipAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Membership.Renew",
            "Membership",
            membership.Id.ToString(),
            membership.CategoryName,
            cancellationToken: cancellationToken);

        return Ok(ToMembershipResponse(membership));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberCancel)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] MembershipTransitionRequest request, CancellationToken cancellationToken)
    {
        var membership = await _lifecycle.CancelMembershipAsync(id, request.Reason, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Membership.Cancel",
            "Membership",
            membership.Id.ToString(),
            membership.CategoryName,
            cancellationToken: cancellationToken);

        return Ok(ToMembershipResponse(membership));
    }

    [HttpPost("{id:guid}/transfer")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberTransfer)]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferMembershipRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var membership = await _lifecycle.TransferMembershipAsync(id, request.TargetCategoryId, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Membership.Transfer",
            "Membership",
            membership.Id.ToString(),
            membership.CategoryName,
            cancellationToken: cancellationToken);

        return Ok(ToMembershipResponse(membership));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

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

    private static MembershipStatusHistoryResponse ToHistoryResponse(MembershipStatusHistoryDto h) => new()
    {
        Id = h.Id,
        MembershipId = h.MembershipId,
        PreviousState = h.PreviousState,
        NewState = h.NewState,
        ChangedByUserId = h.ChangedByUserId,
        Reason = h.Reason,
        ChangedAtUtc = h.ChangedAtUtc,
    };
}