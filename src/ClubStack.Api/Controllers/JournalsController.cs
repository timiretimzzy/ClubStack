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
/// Journal lifecycle (D6 B1, B4, B8, B9, B10, B12, B19). Drafts are
/// editable; posting is irreversible, so a posted journal is only ever
/// corrected by a compensating reversal journal.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/journals")]
[Authorize]
public sealed class JournalsController : AuditedControllerBase
{
    private readonly IJournalService _journals;

    public JournalsController(
        IJournalService journals,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _journals = journals;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalView)]
    [ProducesResponseType(typeof(JournalSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] JournalSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _journals.SearchJournalsAsync(
            new JournalSearchInput(
                request.Status,
                request.FinancialPeriodId,
                request.FinancialAccountId,
                request.SourceType,
                request.FromDate,
                request.ToDate,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new JournalSearchResponse
        {
            Items = result.Items.Select(ToJournalResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalView)]
    [ProducesResponseType(typeof(JournalResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(ToJournalResponse(await _journals.GetJournalAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalCreate)]
    [ProducesResponseType(typeof(JournalResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateJournalRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var journal = await _journals.CreateDraftAsync(
            new CreateJournalInput(
                request.Description,
                request.TransactionDate,
                request.BusinessDate,
                request.CurrencyCode,
                request.ExchangeRate,
                request.RateDateUtc,
                request.FinancialPeriodId,
                request.Lines
                    .Select(l => new CreateJournalLineInput(l.FinancialAccountId, l.Direction, l.Amount, l.Description))
                    .ToList(),
                request.SourceReference),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Journal.CreateDraft",
            "Journal",
            journal.Id.ToString(),
            journal.Reference,
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = journal.Id }, ToJournalResponse(journal));
    }

    [HttpGet("{id:guid}/validation")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalView)]
    [ProducesResponseType(typeof(JournalValidationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _journals.ValidateAsync(id, cancellationToken);

        return Ok(new JournalValidationResponse
        {
            IsValid = result.IsValid,
            Errors = result.Errors.ToList(),
            TotalDebit = result.TotalDebit,
            TotalCredit = result.TotalCredit,
            LineCount = result.LineCount,
        });
    }

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalPost)]
    [ProducesResponseType(typeof(JournalResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Post(Guid id, CancellationToken cancellationToken)
    {
        var journal = await _journals.PostAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Journal.Post",
            "Journal",
            journal.Id.ToString(),
            journal.Reference,
            metadataJson: $"debit={journal.TotalDebit};credit={journal.TotalCredit}",
            cancellationToken: cancellationToken);

        return Ok(ToJournalResponse(journal));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalPost)]
    [ProducesResponseType(typeof(JournalResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelJournalRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var journal = await _journals.CancelAsync(id, new CancelJournalInput(request.Reason), ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Journal.Cancel",
            "Journal",
            journal.Id.ToString(),
            journal.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToJournalResponse(journal));
    }

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceJournalReverse)]
    [ProducesResponseType(typeof(JournalResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseJournalRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var journal = await _journals.ReverseAsync(
            id,
            new ReverseJournalInput(request.Reason, request.TransactionDate),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Journal.Reverse",
            "Journal",
            journal.Id.ToString(),
            journal.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToJournalResponse(journal));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static JournalResponse ToJournalResponse(JournalDto j) => new()
    {
        Id = j.Id,
        Reference = j.Reference,
        Status = j.Status,
        Description = j.Description,
        FinancialPeriodId = j.FinancialPeriodId,
        FinancialPeriodReference = j.FinancialPeriodReference,
        TransactionDate = j.TransactionDate,
        BusinessDate = j.BusinessDate,
        TransactionCurrencyCode = j.TransactionCurrencyCode,
        BaseCurrencyCode = j.BaseCurrencyCode,
        ExchangeRate = j.ExchangeRate,
        RateDateUtc = j.RateDateUtc,
        SourceType = j.SourceType,
        SourceId = j.SourceId,
        SourceReference = j.SourceReference,
        ReversalOfJournalId = j.ReversalOfJournalId,
        ReversalOfJournalReference = j.ReversalOfJournalReference,
        ReversedByJournalId = j.ReversedByJournalId,
        ReceivableId = j.ReceivableId,
        PayableId = j.PayableId,
        TotalDebit = j.TotalDebit,
        TotalCredit = j.TotalCredit,
        PostedAtUtc = j.PostedAtUtc,
        PostedByUserId = j.PostedByUserId,
        CancelledReason = j.CancelledReason,
        CreatedByUserId = j.CreatedByUserId,
        CorrelationId = j.CorrelationId,
        CreatedAtUtc = j.CreatedAtUtc,
        Lines = j.Lines.Select(l => new JournalLineResponse
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            FinancialAccountId = l.FinancialAccountId,
            FinancialAccountCode = l.FinancialAccountCode,
            FinancialAccountName = l.FinancialAccountName,
            Direction = l.Direction,
            Amount = l.Amount,
            CurrencyCode = l.CurrencyCode,
            BaseAmount = l.BaseAmount,
            Description = l.Description,
            SourceLineId = l.SourceLineId,
        }).ToList(),
    };
}
