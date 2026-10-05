using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Finance;
using ClubStack.Contracts.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Financial period lifecycle (D6 B5, B6, B19). A period is never deleted;
/// closing requires posted activity and reopening is limited to the most
/// recently closed period and always requires a reason.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/periods")]
[Authorize]
public sealed class FinancialPeriodsController : AuditedControllerBase
{
    private readonly IFinancialPeriodService _periods;

    public FinancialPeriodsController(
        IFinancialPeriodService periods,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _periods = periods;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinancePeriodView)]
    [ProducesResponseType(typeof(FinancialPeriodSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] FinancialPeriodSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _periods.SearchPeriodsAsync(
            new FinancialPeriodSearchInput(request.Status, request.Year, request.Page, request.PageSize),
            cancellationToken);

        return Ok(new FinancialPeriodSearchResponse
        {
            Items = result.Items.Select(ToPeriodResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinancePeriodView)]
    [ProducesResponseType(typeof(FinancialPeriodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(ToPeriodResponse(await _periods.GetPeriodAsync(id, cancellationToken)));

    [HttpGet("current")]
    [Authorize(Policy = "Permission:" + Permissions.FinancePeriodView)]
    [ProducesResponseType(typeof(FinancialPeriodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Current([FromQuery] DateTimeOffset? transactionDate, CancellationToken cancellationToken)
    {
        var period = await _periods.ResolveForTransactionDateAsync(
            transactionDate ?? DateTimeOffset.UtcNow,
            cancellationToken);

        return period is null ? NotFound() : Ok(ToPeriodResponse(period));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinancePeriodOpen)]
    [ProducesResponseType(typeof(FinancialPeriodResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Open([FromBody] OpenFinancialPeriodRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var period = await _periods.OpenAsync(
            new OpenFinancialPeriodInput(request.Year, request.Month, request.Name),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Period.Open",
            "FinancialPeriod",
            period.Id.ToString(),
            period.Reference,
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = period.Id }, ToPeriodResponse(period));
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "Permission:" + Permissions.FinancePeriodClose)]
    [ProducesResponseType(typeof(FinancialPeriodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var period = await _periods.CloseAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Period.Close",
            "FinancialPeriod",
            period.Id.ToString(),
            period.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToPeriodResponse(period));
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = "Permission:" + Permissions.FinancePeriodReopen)]
    [ProducesResponseType(typeof(FinancialPeriodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenFinancialPeriodRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var period = await _periods.ReopenAsync(id, new ReopenFinancialPeriodInput(request.Reason), ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Period.Reopen",
            "FinancialPeriod",
            period.Id.ToString(),
            period.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToPeriodResponse(period));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static FinancialPeriodResponse ToPeriodResponse(FinancialPeriodDto p) => new()
    {
        Id = p.Id,
        Reference = p.Reference,
        Year = p.Year,
        Month = p.Month,
        Name = p.Name,
        StartUtc = p.StartUtc,
        EndUtc = p.EndUtc,
        Status = p.Status,
        OpenedAtUtc = p.OpenedAtUtc,
        OpenedByUserId = p.OpenedByUserId,
        ClosedAtUtc = p.ClosedAtUtc,
        ClosedByUserId = p.ClosedByUserId,
        ReopenedAtUtc = p.ReopenedAtUtc,
        ReopenedByUserId = p.ReopenedByUserId,
        ReopenReason = p.ReopenReason,
        PostedJournalCount = p.PostedJournalCount,
        PostedDebitTotal = p.PostedDebitTotal,
        PostedCreditTotal = p.PostedCreditTotal,
    };
}
