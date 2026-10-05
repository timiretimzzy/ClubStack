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
/// Manual reconciliation only (D6 B15). There is no bank feed and no bank
/// API: a user matches ledger journals against a statement closing balance.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/reconciliations")]
[Authorize]
public sealed class ReconciliationsController : AuditedControllerBase
{
    private readonly IFinancialReconciliationService _reconciliations;

    public ReconciliationsController(
        IFinancialReconciliationService reconciliations,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _reconciliations = reconciliations;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ReconciliationSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _reconciliations.SearchAsync(
            new FinancialReconciliationSearchInput(
                request.FinancialAccountId,
                request.Status,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new ReconciliationSearchResponse
        {
            Items = result.Items.Select(ToReconciliationResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(ToReconciliationResponse(await _reconciliations.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateReconciliationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var reconciliation = await _reconciliations.CreateAsync(
            new CreateReconciliationInput(
                request.FinancialAccountId,
                request.StatementDateUtc,
                request.StatementClosingBalance,
                request.Note),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Reconciliation.Create",
            "FinancialReconciliation",
            reconciliation.Id.ToString(),
            reconciliation.Reference,
            metadataJson: $"statementClosing={reconciliation.StatementClosingBalance}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = reconciliation.Id }, ToReconciliationResponse(reconciliation));
    }

    [HttpPost("{id:guid}/matches")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMatch(Guid id, [FromBody] MatchReconciliationLineRequest request, CancellationToken cancellationToken)
    {
        var reconciliation = await _reconciliations.AddMatchAsync(
            id,
            new MatchReconciliationLineInput(request.JournalId, request.LedgerAmount, request.Note),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Reconciliation.Match",
            "FinancialReconciliation",
            reconciliation.Id.ToString(),
            reconciliation.Reference,
            metadataJson: $"journal={request.JournalId};ledger={request.LedgerAmount}",
            cancellationToken: cancellationToken);

        return Ok(ToReconciliationResponse(reconciliation));
    }

    [HttpDelete("{id:guid}/matches/{matchId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveMatch(Guid id, Guid matchId, CancellationToken cancellationToken)
    {
        var reconciliation = await _reconciliations.RemoveMatchAsync(id, matchId, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Reconciliation.Unmatch",
            "FinancialReconciliation",
            reconciliation.Id.ToString(),
            reconciliation.Reference,
            metadataJson: $"match={matchId}",
            cancellationToken: cancellationToken);

        return Ok(ToReconciliationResponse(reconciliation));
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteReconciliationRequest? request, CancellationToken cancellationToken)
    {
        var reconciliation = await _reconciliations.CompleteAsync(
            id,
            new CompleteReconciliationInput(request?.Note),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Reconciliation.Complete",
            "FinancialReconciliation",
            reconciliation.Id.ToString(),
            reconciliation.Reference,
            metadataJson: $"statementClosing={reconciliation.StatementClosingBalance};variance={reconciliation.Variance}",
            cancellationToken: cancellationToken);

        return Ok(ToReconciliationResponse(reconciliation));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReconciliationPerform)]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var reconciliation = await _reconciliations.CancelAsync(id, request.Reason, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Reconciliation.Cancel",
            "FinancialReconciliation",
            reconciliation.Id.ToString(),
            reconciliation.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToReconciliationResponse(reconciliation));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static ReconciliationResponse ToReconciliationResponse(FinancialReconciliationDto r) => new()
    {
        Id = r.Id,
        Reference = r.Reference,
        FinancialAccountId = r.FinancialAccountId,
        FinancialAccountCode = r.FinancialAccountCode,
        FinancialAccountName = r.FinancialAccountName,
        StatementDateUtc = r.StatementDateUtc,
        StatementClosingBalance = r.StatementClosingBalance,
        LedgerClosingBalance = r.LedgerClosingBalance,
        Variance = r.Variance,
        Status = r.Status,
        Note = r.Note,
        CreatedByUserId = r.CreatedByUserId,
        CreatedAtUtc = r.CreatedAtUtc,
        CompletedAtUtc = r.CompletedAtUtc,
        CompletedByUserId = r.CompletedByUserId,
        CancelledReason = r.CancelledReason,
        MatchedLedgerTotal = r.MatchedLedgerTotal,
        Matches = r.Matches.Select(m => new ReconciliationMatchResponse
        {
            Id = m.Id,
            JournalId = m.JournalId,
            JournalReference = m.JournalReference,
            TransactionDate = m.TransactionDate,
            LedgerAmount = m.LedgerAmount,
            Note = m.Note,
            MatchedAtUtc = m.MatchedAtUtc,
            MatchedByUserId = m.MatchedByUserId,
        }).ToList(),
    };
}
