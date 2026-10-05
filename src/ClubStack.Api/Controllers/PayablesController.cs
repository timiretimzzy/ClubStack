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
/// Accounts payable (D6 B13). Supplier identity stays on the existing
/// Supplier; the payable is the accounting document and is settled by
/// allocation, never by deletion.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/payables")]
[Authorize]
public sealed class PayablesController : AuditedControllerBase
{
    private readonly IPayableService _payables;

    public PayablesController(
        IPayableService payables,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _payables = payables;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinanceApView)]
    [ProducesResponseType(typeof(PayableSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PayableSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _payables.SearchAsync(
            new PayableSearchInput(request.SupplierId, request.Status, request.AsOf, request.Page, request.PageSize),
            cancellationToken);

        return Ok(new PayableSearchResponse
        {
            Items = result.Items.Select(ToPayableResponse).ToList(),
            TotalCount = result.TotalCount,
            TotalOutstanding = result.TotalOutstanding,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceApView)]
    [ProducesResponseType(typeof(PayableResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(ToPayableResponse(await _payables.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinanceApManage)]
    [ProducesResponseType(typeof(PayableResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePayableRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var payable = await _payables.CreateAsync(
            new CreatePayableInput(
                request.SupplierId,
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
            "Finance.Payable.Create",
            "Payable",
            payable.Id.ToString(),
            payable.Reference,
            metadataJson: $"amount={payable.OriginalAmount};{payable.CurrencyCode}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = payable.Id }, ToPayableResponse(payable));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceApManage)]
    [ProducesResponseType(typeof(PayableResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var payable = await _payables.CancelAsync(id, request.Reason, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Payable.Cancel",
            "Payable",
            payable.Id.ToString(),
            payable.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToPayableResponse(payable));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static PayableResponse ToPayableResponse(PayableDto p) => new()
    {
        Id = p.Id,
        Reference = p.Reference,
        SupplierId = p.SupplierId,
        SupplierCode = p.SupplierCode,
        SupplierName = p.SupplierName,
        SourceType = p.SourceType,
        SourceId = p.SourceId,
        SourceReference = p.SourceReference,
        OriginalAmount = p.OriginalAmount,
        AllocatedAmount = p.AllocatedAmount,
        OutstandingAmount = p.OutstandingAmount,
        TransactionDate = p.TransactionDate,
        DueDate = p.DueDate,
        CurrencyCode = p.CurrencyCode,
        Status = p.Status,
        JournalId = p.JournalId,
        JournalReference = p.JournalReference,
        Description = p.Description,
        CancelledReason = p.CancelledReason,
        CreatedAtUtc = p.CreatedAtUtc,
        Allocations = p.Allocations.Select(a => new PayableAllocationResponse
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
