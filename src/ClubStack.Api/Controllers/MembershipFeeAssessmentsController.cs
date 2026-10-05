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
/// Traceable membership fee assessments (Phase 2 MEM-041/MEM-042/MEM-043).
/// Assessment commands are recorded; settlement happens in a later slice.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/membership-fee-assessments")]
[Authorize]
public sealed class MembershipFeeAssessmentsController : AuditedControllerBase
{
    private readonly IMembershipFeeService _fees;

    public MembershipFeeAssessmentsController(
        IMembershipFeeService fees,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _fees = fees;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.MembershipFeeView)]
    [ProducesResponseType(typeof(MembershipFeeAssessmentSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? membershipId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _fees.SearchAssessmentsAsync(
            new MembershipFeeAssessmentSearchInput(membershipId, status, page, pageSize),
            cancellationToken);

        return Ok(new MembershipFeeAssessmentSearchResponse
        {
            Items = result.Items.Select(ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipFeeView)]
    [ProducesResponseType(typeof(MembershipFeeAssessmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var assessment = await _fees.GetAssessmentAsync(id, cancellationToken);
        return Ok(ToResponse(assessment));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.MembershipFeeAssess)]
    [ProducesResponseType(typeof(MembershipFeeAssessmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assess([FromBody] AssessFeeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var assessment = await _fees.AssessAsync(
            new AssessMembershipFeeInput(request.MembershipId, request.FeeType, request.AssessmentDateUtc),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Membership.Fee.Assess",
            "MembershipFeeAssessment",
            assessment.Id.ToString(),
            assessment.MembershipNumber,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(assessment));
    }

    [HttpPost("{id:guid}/adjust")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipFeeAdjust)]
    [ProducesResponseType(typeof(MembershipFeeAssessmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] FeeAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var assessment = await _fees.AdjustAsync(
            id,
            new MembershipFeeAdjustmentInput(request.Amount, request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Membership.Fee.Adjust",
            "MembershipFeeAssessment",
            assessment.Id.ToString(),
            assessment.MembershipNumber,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(assessment));
    }

    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipFeeAdjust)]
    [ProducesResponseType(typeof(MembershipFeeAssessmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Void(Guid id, [FromBody] FeeVoidRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var assessment = await _fees.VoidAsync(
            id,
            new MembershipFeeVoidInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Membership.Fee.Void",
            "MembershipFeeAssessment",
            assessment.Id.ToString(),
            assessment.MembershipNumber,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(assessment));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static MembershipFeeAssessmentResponse ToResponse(MembershipFeeAssessmentDto a) => new()
    {
        Id = a.Id,
        MembershipId = a.MembershipId,
        MembershipNumber = a.MembershipNumber,
        MemberName = a.MemberName,
        FeeType = a.FeeType,
        Description = a.Description,
        Amount = a.Amount,
        CurrencyCode = a.CurrencyCode,
        IsProrated = a.IsProrated,
        AssessmentDateUtc = a.AssessmentDateUtc,
        DueDateUtc = a.DueDateUtc,
        Source = a.Source,
        Status = a.Status,
        CreatedAtUtc = a.CreatedAtUtc,
    };
}