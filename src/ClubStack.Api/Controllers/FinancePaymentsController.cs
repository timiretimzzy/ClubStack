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
/// Finance payments (D6 B14). One call records the payment, allocates it to
/// receivables/payables, moves the document statuses and posts the cash or
/// bank journal in a single transaction. Idempotent on the supplied key.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/payments")]
[Authorize]
public sealed class FinancePaymentsController : AuditedControllerBase
{
    private readonly IFinancePaymentService _payments;

    public FinancePaymentsController(
        IFinancePaymentService payments,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _payments = payments;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinancePaymentManage)]
    [ProducesResponseType(typeof(PaymentSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PaymentSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _payments.SearchAsync(
            new PaymentSearchInput(
                request.Direction,
                request.MemberId,
                request.SupplierId,
                request.FromDate,
                request.ToDate,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new PaymentSearchResponse
        {
            Items = result.Items.Select(ToPaymentResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinancePaymentManage)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(ToPaymentResponse(await _payments.GetAsync(id, cancellationToken)));

    [HttpGet("by-idempotency-key/{key}")]
    [Authorize(Policy = "Permission:" + Permissions.FinancePaymentManage)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdempotencyKey(string key, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdempotencyKeyAsync(key, cancellationToken);
        return payment is null ? NotFound() : Ok(ToPaymentResponse(payment));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinancePaymentManage)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Record([FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var payment = await _payments.RecordAsync(
            new RecordPaymentInput(
                request.Direction,
                request.MemberId,
                request.SupplierId,
                request.MemberAccountId,
                request.AccountTransactionId,
                request.SourceType,
                request.SourceId,
                request.SourceReference,
                request.Amount,
                request.CurrencyCode,
                request.TransactionDate,
                request.ValueDate,
                request.PaymentMethodCode,
                request.ExternalReference,
                request.Description,
                request.Allocations?
                    .Select(a => new PaymentAllocationInput(a.ReceivableId, a.PayableId, a.Amount))
                    .ToList(),
                IdempotencyKey: request.IdempotencyKey),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Payment.Record",
            "Payment",
            payment.Id.ToString(),
            payment.Reference,
            metadataJson:
                $"amount={payment.Amount};{payment.CurrencyCode};unapplied={payment.UnappliedAmount};journal={payment.JournalReference}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = payment.Id }, ToPaymentResponse(payment));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static PaymentResponse ToPaymentResponse(PaymentDto p) => new()
    {
        Id = p.Id,
        Reference = p.Reference,
        Direction = p.Direction,
        MemberId = p.MemberId,
        MemberNumber = p.MemberNumber,
        SupplierId = p.SupplierId,
        SupplierCode = p.SupplierCode,
        MemberAccountId = p.MemberAccountId,
        AccountTransactionId = p.AccountTransactionId,
        SourceType = p.SourceType,
        SourceId = p.SourceId,
        SourceReference = p.SourceReference,
        Amount = p.Amount,
        UnappliedAmount = p.UnappliedAmount,
        CurrencyCode = p.CurrencyCode,
        TransactionDate = p.TransactionDate,
        ValueDate = p.ValueDate,
        PaymentMethodCode = p.PaymentMethodCode,
        ExternalReference = p.ExternalReference,
        JournalId = p.JournalId,
        JournalReference = p.JournalReference,
        Description = p.Description,
        IdempotencyKey = p.IdempotencyKey,
        CreatedByUserId = p.CreatedByUserId,
        CreatedAtUtc = p.CreatedAtUtc,
        Allocations = p.Allocations.Select(a => new PaymentAllocationResponse
        {
            Id = a.Id,
            PaymentId = a.PaymentId,
            ReceivableId = a.ReceivableId,
            PayableId = a.PayableId,
            ReceivableReference = a.ReceivableReference,
            PayableReference = a.PayableReference,
            Amount = a.Amount,
            CreatedAtUtc = a.CreatedAtUtc,
        }).ToList(),
    };
}
