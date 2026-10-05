using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Contracts.Common;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Pos;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Point-of-sale sales (D5 POS). A sale is created as a draft (Open), completed
/// once it is balanced by payments (inventory movements + receipt + costing
/// safety net), voided before completion, or refunded in full after completion.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/sales")]
[Authorize]
public sealed class SalesController : AuditedControllerBase
{
    private readonly ISaleService _sales;

    public SalesController(
        ISaleService sales,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _sales = sales;
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleCreate)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var sale = await _sales.CreateAsync(
            new CreateSaleInput(
                request.RegisterId,
                request.MemberId,
                request.CredentialValue,
                request.Lines.Select(l => new CreateSaleLineInput(
                    l.ProductId,
                    l.Quantity,
                    l.UnitPriceOverride,
                    l.Discount is null
                        ? null
                        : new SaleDiscountLineInput(l.Discount.DiscountType, l.Discount.Value, l.Discount.Reason))).ToList(),
                request.Note,
                request.BusinessDateUtc),
            ActorUserId(),
            cancellationToken);

        return Ok(ToSaleResponse(sale));
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleReprint)]
    [ProducesResponseType(typeof(SaleSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? status,
        [FromQuery] Guid? registerId,
        [FromQuery] Guid? memberId,
        [FromQuery] DateTimeOffset? fromDateUtc,
        [FromQuery] DateTimeOffset? toDateUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _sales.SearchAsync(
            new SaleSearchInput(status, registerId, memberId, fromDateUtc, toDateUtc, page, pageSize),
            cancellationToken);

        return Ok(new SaleSearchResponse
        {
            Items = result.Items.Select(ToSaleResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleReprint)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var sale = await _sales.GetAsync(id, cancellationToken);
        return Ok(ToSaleResponse(sale));
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleComplete)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteSaleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var sale = await _sales.CompleteAsync(
            id,
            new CompleteSaleInput(
                request.MemberId,
                request.Payments.Select(p => new SalePaymentInput(p.PaymentMethodCode, p.Amount, p.ExternalReference)).ToList(),
                request.IdempotencyKey),
            ActorUserId(),
            CorrelationId,
            cancellationToken);

        await RecordAuditAsync(
            "POS.Sale.Complete",
            "Sale",
            sale.Id.ToString(),
            sale.SaleNumber,
            cancellationToken: cancellationToken);

        return Ok(ToSaleResponse(sale));
    }

    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleVoid)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidSaleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var sale = await _sales.VoidAsync(
            id,
            new VoidSaleInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "POS.Sale.Void",
            "Sale",
            sale.Id.ToString(),
            sale.SaleNumber,
            cancellationToken: cancellationToken);

        return Ok(ToSaleResponse(sale));
    }

    [HttpPost("{id:guid}/refund")]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleRefund)]
    [ProducesResponseType(typeof(SaleRefundResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundSaleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var refund = await _sales.RefundAsync(
            id,
            new RefundSaleInput(
                request.Reason,
                request.Payments?.Select(p => new SalePaymentInput(p.PaymentMethodCode, p.Amount, p.ExternalReference)).ToList(),
                request.IdempotencyKey),
            ActorUserId(),
            CorrelationId,
            cancellationToken);

        await RecordAuditAsync(
            "POS.Sale.Refund",
            "SaleRefund",
            refund.Id.ToString(),
            refund.RefundNumber,
            cancellationToken: cancellationToken);

        return Ok(new SaleRefundResponse
        {
            Id = refund.Id,
            RefundNumber = refund.RefundNumber,
            SaleId = refund.SaleId,
            SaleNumber = refund.SaleNumber,
            Status = refund.Status,
            CurrencyCode = refund.CurrencyCode,
            Amount = refund.Amount,
            Reason = refund.Reason,
            RefundedByUserId = refund.RefundedByUserId,
            RefundedAtUtc = refund.RefundedAtUtc,
            CreatedAtUtc = refund.CreatedAtUtc,
            Lines = refund.Lines.Select(l => new SaleRefundLineResponse
            {
                ProductId = l.ProductId,
                ProductSku = l.ProductSku,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
            }).ToList(),
            Payments = refund.Payments.Select(p => new SaleRefundPaymentResponse
            {
                PaymentMethodCode = p.PaymentMethodCode,
                PaymentMethodName = p.PaymentMethodName,
                Kind = p.Kind,
                Amount = p.Amount,
                ExternalReference = p.ExternalReference,
            }).ToList(),
        });
    }

    [HttpGet("{id:guid}/receipt")]
    [Authorize(Policy = "Permission:" + Permissions.PosSaleReprint)]
    [ProducesResponseType(typeof(ReceiptResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await _sales.GetReceiptAsync(id, cancellationToken);
        return Ok(ToReceiptResponse(receipt));
    }

    private static SaleResponse ToSaleResponse(SaleDto s) => new()
    {
        Id = s.Id,
        SaleNumber = s.SaleNumber,
        Status = s.Status,
        RegisterId = s.RegisterId,
        RegisterCode = s.RegisterCode,
        PosOutletId = s.PosOutletId,
        PosOutletCode = s.PosOutletCode,
        LocationId = s.LocationId,
        LocationCode = s.LocationCode,
        CashierUserId = s.CashierUserId,
        MemberId = s.MemberId,
        MemberNumber = s.MemberNumber,
        MemberName = s.MemberName,
        CurrencyCode = s.CurrencyCode,
        Subtotal = s.Subtotal,
        DiscountTotal = s.DiscountTotal,
        TaxTotal = s.TaxTotal,
        GrandTotal = s.GrandTotal,
        Note = s.Note,
        BusinessDateUtc = s.BusinessDateUtc,
        CompletedAtUtc = s.CompletedAtUtc,
        VoidedAtUtc = s.VoidedAtUtc,
        VoidReason = s.VoidReason,
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
        Lines = s.Lines.Select(ToLineResponse).ToList(),
        Payments = s.Payments.Select(ToPaymentResponse).ToList(),
        Discounts = s.Discounts.Select(d => new SaleDiscountResponse
        {
            Id = d.Id,
            Level = d.Level,
            LineId = d.LineId,
            DiscountType = d.DiscountType,
            Value = d.Value,
            Reason = d.Reason,
            AuthorizedByUserId = d.AuthorizedByUserId,
            AppliedAtUtc = d.AppliedAtUtc,
        }).ToList(),
        Receipt = s.Receipt is null ? null : ToReceiptResponse(s.Receipt),
    };

    private static SaleLineResponse ToLineResponse(SaleLineDto l) => new()
    {
        Id = l.Id,
        ProductId = l.ProductId,
        ProductSku = l.ProductSku,
        ProductName = l.ProductName,
        Quantity = l.Quantity,
        OriginalUnitPrice = l.OriginalUnitPrice,
        UnitPrice = l.UnitPrice,
        DiscountAmount = l.DiscountAmount,
        TaxableBase = l.TaxableBase,
        TaxAmount = l.TaxAmount,
        LineTotal = l.LineTotal,
        TaxTreatment = l.TaxTreatment,
        TaxRateName = l.TaxRateName,
        TaxRate = l.TaxRate,
        StockManaged = l.StockManaged,
    };

    private static SalePaymentResponse ToPaymentResponse(SalePaymentDto p) => new()
    {
        Id = p.Id,
        PaymentMethodCode = p.PaymentMethodCode,
        PaymentMethodName = p.PaymentMethodName,
        Kind = p.Kind,
        Amount = p.Amount,
        ExternalReference = p.ExternalReference,
        AccountTransactionId = p.AccountTransactionId,
        AccountTransactionReference = p.AccountTransactionReference,
    };

    private static ReceiptResponse ToReceiptResponse(ReceiptDto r) => new()
    {
        Id = r.Id,
        ReceiptNumber = r.ReceiptNumber,
        SaleId = r.SaleId,
        SaleNumber = r.SaleNumber,
        Status = r.Status,
        RegisterId = r.RegisterId,
        RegisterCode = r.RegisterCode,
        PosOutletId = r.PosOutletId,
        PosOutletCode = r.PosOutletCode,
        CashierUserId = r.CashierUserId,
        MemberId = r.MemberId,
        MemberNumber = r.MemberNumber,
        MemberName = r.MemberName,
        CurrencyCode = r.CurrencyCode,
        Subtotal = r.Subtotal,
        DiscountTotal = r.DiscountTotal,
        TaxTotal = r.TaxTotal,
        GrandTotal = r.GrandTotal,
        BusinessDateUtc = r.BusinessDateUtc,
        GeneratedAtUtc = r.GeneratedAtUtc,
        Lines = r.Lines.Select(ToLineResponse).ToList(),
        Payments = r.Payments.Select(ToPaymentResponse).ToList(),
    };

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}