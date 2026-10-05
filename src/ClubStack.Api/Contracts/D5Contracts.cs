using System.ComponentModel.DataAnnotations;

namespace ClubStack.Api.Contracts;

// ---- POS catalog (D5) ----

public sealed record PosProductResponse
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public bool StockManaged { get; init; }
    public decimal UnitPrice { get; init; }
    public string PriceSourceType { get; init; } = string.Empty;
    public bool IsOverride { get; init; }
    public decimal? AvailableQuantity { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string TaxTreatment { get; init; } = string.Empty;
    public string? TaxRateName { get; init; }
    public decimal? TaxRate { get; init; }
}

public sealed record PosProductSearchResponse
{
    public IReadOnlyList<PosProductResponse> Items { get; init; } = Array.Empty<PosProductResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record PosMemberResponse
{
    public Guid MemberId { get; init; }
    public string MemberNumber { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? CredentialId { get; init; }
    public string? CredentialNumber { get; init; }
    public string? CredentialStatus { get; init; }
}

public sealed record PosMemberSearchResponse
{
    public IReadOnlyList<PosMemberResponse> Items { get; init; } = Array.Empty<PosMemberResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

// ---- POS configuration (D5) ----

public sealed record CreatePosOutletRequest
{
    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed record PosOutletResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record CreatePosRegisterRequest
{
    public required Guid PosOutletId { get; init; }

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    public required Guid InventoryLocationId { get; init; }
}

public sealed record PosRegisterResponse
{
    public Guid Id { get; init; }
    public Guid PosOutletId { get; init; }
    public string PosOutletCode { get; init; } = string.Empty;
    public string PosOutletName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid InventoryLocationId { get; init; }
    public string InventoryLocationCode { get; init; } = string.Empty;
    public string InventoryLocationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? CurrentSessionId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record PosConfigurationResponse
{
    public IReadOnlyList<PosOutletResponse> Outlets { get; init; } = Array.Empty<PosOutletResponse>();
    public IReadOnlyList<PosRegisterResponse> Registers { get; init; } = Array.Empty<PosRegisterResponse>();
}

// ---- Sales (D5) ----

public sealed record SaleLineRequest
{
    public required Guid ProductId { get; init; }

    [Range(0.0001, 9999999999999)]
    public decimal Quantity { get; init; }

    [Range(0, 9999999999999)]
    public decimal? UnitPriceOverride { get; init; }

    public SaleDiscountLineRequest? Discount { get; init; }
}

public sealed record SaleDiscountLineRequest
{
    [Required, MaxLength(16)]
    public string DiscountType { get; init; } = string.Empty;

    [Range(0, 9999999999999)]
    public decimal Value { get; init; }

    [MaxLength(200)]
    public string? Reason { get; init; }
}

public sealed record CreateSaleRequest
{
    public required Guid RegisterId { get; init; }

    public Guid? MemberId { get; init; }

    [MaxLength(64)]
    public string? CredentialValue { get; init; }

    [MinLength(1), MaxLength(200)]
    public IReadOnlyList<SaleLineRequest> Lines { get; init; } = Array.Empty<SaleLineRequest>();

    [MaxLength(500)]
    public string? Note { get; init; }

    public DateTimeOffset? BusinessDateUtc { get; init; }
}

public sealed record SalePaymentRequest
{
    [Required, MaxLength(16)]
    public string PaymentMethodCode { get; init; } = string.Empty;

    [Range(0.01, 9999999999999)]
    public decimal Amount { get; init; }

    [MaxLength(80)]
    public string? ExternalReference { get; init; }
}

public sealed record CompleteSaleRequest
{
    public Guid? MemberId { get; init; }

    [MinLength(1), MaxLength(50)]
    public IReadOnlyList<SalePaymentRequest> Payments { get; init; } = Array.Empty<SalePaymentRequest>();

    [MaxLength(64)]
    public string? IdempotencyKey { get; init; }
}

public sealed record VoidSaleRequest
{
    [Required, MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record RefundSaleRequest
{
    [Required, MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<SalePaymentRequest>? Payments { get; init; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; init; }
}

public sealed record SaleLineResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal OriginalUnitPrice { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxableBase { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal LineTotal { get; init; }
    public string TaxTreatment { get; init; } = string.Empty;
    public string? TaxRateName { get; init; }
    public decimal? TaxRate { get; init; }
    public bool StockManaged { get; init; }
}

public sealed record SalePaymentResponse
{
    public Guid Id { get; init; }
    public string PaymentMethodCode { get; init; } = string.Empty;
    public string PaymentMethodName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? ExternalReference { get; init; }
    public Guid? AccountTransactionId { get; init; }
    public string? AccountTransactionReference { get; init; }
}

public sealed record SaleDiscountResponse
{
    public Guid Id { get; init; }
    public string Level { get; init; } = string.Empty;
    public Guid? LineId { get; init; }
    public string DiscountType { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string? Reason { get; init; }
    public string AuthorizedByUserId { get; init; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; init; }
}

public sealed record ReceiptResponse
{
    public Guid Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public Guid SaleId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public string RegisterCode { get; init; } = string.Empty;
    public Guid PosOutletId { get; init; }
    public string PosOutletCode { get; init; } = string.Empty;
    public string CashierUserId { get; init; } = string.Empty;
    public Guid? MemberId { get; init; }
    public string? MemberNumber { get; init; }
    public string? MemberName { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal GrandTotal { get; init; }
    public DateTimeOffset BusinessDateUtc { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public IReadOnlyList<SaleLineResponse> Lines { get; init; } = Array.Empty<SaleLineResponse>();
    public IReadOnlyList<SalePaymentResponse> Payments { get; init; } = Array.Empty<SalePaymentResponse>();
}

public sealed record SaleResponse
{
    public Guid Id { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public string RegisterCode { get; init; } = string.Empty;
    public Guid PosOutletId { get; init; }
    public string PosOutletCode { get; init; } = string.Empty;
    public Guid? LocationId { get; init; }
    public string? LocationCode { get; init; }
    public string CashierUserId { get; init; } = string.Empty;
    public Guid? MemberId { get; init; }
    public string? MemberNumber { get; init; }
    public string? MemberName { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal GrandTotal { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset BusinessDateUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? VoidedAtUtc { get; init; }
    public string? VoidReason { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public IReadOnlyList<SaleLineResponse> Lines { get; init; } = Array.Empty<SaleLineResponse>();
    public IReadOnlyList<SalePaymentResponse> Payments { get; init; } = Array.Empty<SalePaymentResponse>();
    public IReadOnlyList<SaleDiscountResponse> Discounts { get; init; } = Array.Empty<SaleDiscountResponse>();
    public ReceiptResponse? Receipt { get; init; }
}

public sealed record SaleSearchResponse
{
    public IReadOnlyList<SaleResponse> Items { get; init; } = Array.Empty<SaleResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record SaleRefundLineResponse
{
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed record SaleRefundPaymentResponse
{
    public string PaymentMethodCode { get; init; } = string.Empty;
    public string PaymentMethodName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? ExternalReference { get; init; }
}

public sealed record SaleRefundResponse
{
    public Guid Id { get; init; }
    public string RefundNumber { get; init; } = string.Empty;
    public Guid SaleId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string RefundedByUserId { get; init; } = string.Empty;
    public DateTimeOffset RefundedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyList<SaleRefundLineResponse> Lines { get; init; } = Array.Empty<SaleRefundLineResponse>();
    public IReadOnlyList<SaleRefundPaymentResponse> Payments { get; init; } = Array.Empty<SaleRefundPaymentResponse>();
}

// ---- Cashier sessions (D5 Cashiering) ----

public sealed record OpenCashierSessionRequest
{
    public required Guid RegisterId { get; init; }

    [Range(0, 9999999999999)]
    public decimal OpeningFloat { get; init; }

    [MaxLength(500)]
    public string? Note { get; init; }
}

public sealed record CashMovementRequest
{
    [Required, MaxLength(16)]
    public string MovementType { get; init; } = string.Empty;

    [Range(0.01, 9999999999999)]
    public decimal Amount { get; init; }

    [Required, MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record CloseCashierSessionRequest
{
    [Range(0, 9999999999999)]
    public decimal DeclaredCash { get; init; }
}

public sealed record CashMovementResponse
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal SignedAmount { get; init; }
    public string? Reason { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record CashierSessionResponse
{
    public Guid Id { get; init; }
    public string SessionNumber { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public string RegisterCode { get; init; } = string.Empty;
    public string CashierUserId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal OpeningFloat { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal? DeclaredCash { get; init; }
    public decimal? Variance { get; init; }
    public bool VarianceApproved { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset OpenedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? ClosingStartedAtUtc { get; init; }
    public DateTimeOffset? ClosedAtUtc { get; init; }
    public DateTimeOffset? ReconciledAtUtc { get; init; }
    public string? ReconciledByUserId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public IReadOnlyList<CashMovementResponse> Movements { get; init; } = Array.Empty<CashMovementResponse>();
}

public sealed record CashierSessionSearchResponse
{
    public IReadOnlyList<CashierSessionResponse> Items { get; init; } = Array.Empty<CashierSessionResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}