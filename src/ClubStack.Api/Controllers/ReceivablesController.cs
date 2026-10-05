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
/// Accounts receivable (D6 B13). Member identity stays on Member/Member
/// Account; the receivable is the accounting document and is settled by
/// allocation, never by deletion.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/receivables")]
[Authorize]
public sealed class ReceivablesController : AuditedControllerBase
{
    private readonly IReceivableService _receivables;

    public ReceivablesController(
        IReceivableService receivables,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _receivables = receivables;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinanceArView)]
    [ProducesResponseType(typeof(ReceivableSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ReceivableSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _receivables.SearchAsync(
            new ReceivableSearchInput(request.MemberId, request.Status, request.AsOf, request.Page, request.PageSize),
            cancellationToken);

        return Ok(new ReceivableSearchResponse
        {
            Items = result.Items.Select(ToReceivableResponse).ToList(),
            TotalCount = result.TotalCount,
            TotalOutstanding = result.TotalOutstanding,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceArView)]
    [ProducesResponseType(typeof(ReceivableResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(ToReceivableResponse(await _receivables.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinanceArManage)]
    [ProducesResponseType(typeof(ReceivableResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateReceivableRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var receivable = await _receivables.CreateAsync(
            new CreateReceivableInput(
                request.MemberId,
                request.SourceType,
                request.SourceId,
                request.SourceReference,
                request.Amount,
                request.TransactionDate,
                request.DueDate,
                request.CurrencyCode,
                request.ExchangeRate,
                request.RateDateUtc,
                request.Description),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Receivable.Create",
            "Receivable",
            receivable.Id.ToString(),
            receivable.Reference,
            metadataJson: $"amount={receivable.OriginalAmount};{receivable.CurrencyCode}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = receivable.Id }, ToReceivableResponse(receivable));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceArManage)]
    [ProducesResponseType(typeof(ReceivableResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var receivable = await _receivables.CancelAsync(id, request.Reason, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Receivable.Cancel",
            "Receivable",
            receivable.Id.ToString(),
            receivable.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToReceivableResponse(receivable));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static ReceivableResponse ToReceivableResponse(ReceivableDto r) => new()
    {
        Id = r.Id,
        Reference = r.Reference,
        MemberId = r.MemberId,
        MemberNumber = r.MemberNumber,
        MemberName = r.MemberName,
        MemberAccountId = r.MemberAccountId,
        SourceType = r.SourceType,
        SourceId = r.SourceId,
        SourceReference = r.SourceReference,
        OriginalAmount = r.OriginalAmount,
        AllocatedAmount = r.AllocatedAmount,
        OutstandingAmount = r.OutstandingAmount,
        TransactionDate = r.TransactionDate,
        DueDate = r.DueDate,
        CurrencyCode = r.CurrencyCode,
        Status = r.Status,
        JournalId = r.JournalId,
        JournalReference = r.JournalReference,
        Description = r.Description,
        CancelledReason = r.CancelledReason,
        CreatedAtUtc = r.CreatedAtUtc,
        Allocations = r.Allocations.Select(a => new ReceivableAllocationResponse
        {
            Id = a.Id,
            PaymentId = a.PaymentId,
            PaymentReference = a.PaymentReference,
            Amount = a.Amount,
            AllocatedAtUtc = a.AllocatedAtUtc,
            AllocatedByUserId = a.AllocatedByUserId,
        }).ToList(),
    };
}
