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
/// Membership applications through the Phase 8 §7.2 workflow
/// (Draft → Submitted → UnderReview → Approved → Activated, or Rejected).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/membership-applications")]
[Authorize]
public sealed class MembershipApplicationsController : AuditedControllerBase
{
    private readonly IMembershipLifecycleService _lifecycle;

    public MembershipApplicationsController(
        IMembershipLifecycleService lifecycle,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _lifecycle = lifecycle;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationView)]
    [ProducesResponseType(typeof(MembershipApplicationSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _lifecycle.SearchApplicationsAsync(new MembershipApplicationSearchInput(status, page, pageSize), cancellationToken);
        return Ok(new MembershipApplicationSearchResponse
        {
            Items = result.Items.Select(ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationView)]
    [ProducesResponseType(typeof(MembershipApplicationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var application = await _lifecycle.GetApplicationAsync(id, cancellationToken);
        return Ok(ToResponse(application));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationCreate)]
    [ProducesResponseType(typeof(MembershipApplicationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateMembershipApplicationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var application = await _lifecycle.CreateApplicationAsync(
            new CreateMembershipApplicationInput(
                request.PersonId,
                request.CategoryId,
                request.SponsorName,
                request.ReferrerName,
                request.Notes,
                request.RequiredDocuments),
            cancellationToken);

        await RecordAuditAsync(
            "Membership.Application.Create",
            "MembershipApplication",
            application.Id.ToString(),
            application.PersonName,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(application));
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationCreate)]
    [ProducesResponseType(typeof(MembershipApplicationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var application = await _lifecycle.SubmitApplicationAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Application.Submit",
            "MembershipApplication",
            application.Id.ToString(),
            cancellationToken: cancellationToken);

        return Ok(ToResponse(application));
    }

    [HttpPost("{id:guid}/start-review")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationApprove)]
    [ProducesResponseType(typeof(MembershipApplicationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken cancellationToken)
    {
        var application = await _lifecycle.StartReviewApplicationAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Application.StartReview",
            "MembershipApplication",
            application.Id.ToString(),
            cancellationToken: cancellationToken);

        return Ok(ToResponse(application));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationApprove)]
    [ProducesResponseType(typeof(MembershipApplicationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var application = await _lifecycle.ApproveApplicationAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Membership.Application.Approve",
            "MembershipApplication",
            application.Id.ToString(),
            application.MemberNumber ?? string.Empty,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(application));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipApplicationApprove)]
    [ProducesResponseType(typeof(MembershipApplicationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectMembershipApplicationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var application = await _lifecycle.RejectApplicationAsync(
            id,
            new RejectMembershipApplicationInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Membership.Application.Reject",
            "MembershipApplication",
            application.Id.ToString(),
            cancellationToken: cancellationToken);

        return Ok(ToResponse(application));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static MembershipApplicationResponse ToResponse(MembershipApplicationDto a) => new()
    {
        Id = a.Id,
        PersonId = a.PersonId,
        PersonName = a.PersonName,
        CategoryId = a.CategoryId,
        CategoryName = a.CategoryName,
        Status = a.Status,
        SponsorName = a.SponsorName,
        ReferrerName = a.ReferrerName,
        Notes = a.Notes,
        ApprovedByUserId = a.ApprovedByUserId,
        RequiredDocuments = a.RequiredDocuments,
        SubmittedAtUtc = a.SubmittedAtUtc,
        ReviewedAtUtc = a.ReviewedAtUtc,
        ApprovedAtUtc = a.ApprovedAtUtc,
        RejectionReason = a.RejectionReason,
        MemberId = a.MemberId,
        MemberNumber = a.MemberNumber,
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
    };
}