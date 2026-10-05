using System.ComponentModel.DataAnnotations;

namespace ClubStack.Api.Contracts;

// ---- Products (Phase 2 INV-001..010) ----

public sealed record ProductSearchRequest
{
    [MaxLength(64)]
    public string? Query { get; init; }

    public Guid? CategoryId { get; init; }

    public bool? StockManagedOnly { get; init; }

    [MaxLength(16)]
    public string? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record ProductPriceResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string PriceType { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record ProductResponse
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string UnitOfMeasure { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public bool StockManaged { get; init; }
    public decimal Cost { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal? ReorderLevel { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string TaxTreatment { get; init; } = "Inclusive";
    public Guid? TaxRateId { get; init; }
    public string? TaxRateName { get; init; }
    public decimal? TaxRate { get; init; }
}

public sealed record ProductSearchResponse
{
    public IReadOnlyList<ProductResponse> Items { get; init; } = Array.Empty<ProductResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateProductRequest
{
    [Required, MaxLength(32)]
    public string Sku { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    public required Guid CategoryId { get; init; }

    [Required, MaxLength(24)]
    public string UnitOfMeasure { get; init; } = string.Empty;

    [MaxLength(64)]
    public string? Barcode { get; init; }

    public bool StockManaged { get; init; }

    [Range(0, 9999999999999)]
    public decimal Cost { get; init; }

    [Range(0.01, 9999999999999, ErrorMessage = "The price must be positive.")]
    public decimal? PriceAmount { get; init; }

    [Range(0, 9999999999999)]
    public decimal? ReorderLevel { get; init; }

    public Guid? SupplierId { get; init; }

    [MaxLength(8)]
    public string? CurrencyCode { get; init; }

    public Guid? TaxRateId { get; init; }

    [MaxLength(24)]
    public string? TaxTreatment { get; init; }
}

public sealed record UpdateProductRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    public required Guid CategoryId { get; init; }

    [Required, MaxLength(24)]
    public string UnitOfMeasure { get; init; } = string.Empty;

    [MaxLength(64)]
    public string? Barcode { get; init; }

    public bool StockManaged { get; init; }

    [Range(0, 9999999999999)]
    public decimal Cost { get; init; }

    [Range(0, 9999999999999)]
    public decimal? ReorderLevel { get; init; }

    public Guid? SupplierId { get; init; }

    public Guid? TaxRateId { get; init; }

    [MaxLength(24)]
    public string? TaxTreatment { get; init; }
}

public sealed record SetProductPriceRequest
{
    [Range(0.01, 9999999999999, ErrorMessage = "The price must be positive.")]
    public decimal Amount { get; init; }

    [MaxLength(8)]
    public string? CurrencyCode { get; init; }
}

public sealed record SetProductStatusRequest
{
    public required bool Active { get; init; }
}

// ---- Product categories (Phase 2 INV-004) ----

public sealed record ProductCategoryResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record CreateProductCategoryRequest
{
    [Required, MaxLength(24)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; init; }
}

public sealed record UpdateProductCategoryRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;
}

// ---- Inventory locations (Phase 2 INV-020, Phase 3 §30) ----

public sealed record InventoryLocationResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record CreateInventoryLocationRequest
{
    [Required, MaxLength(24)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(24)]
    public string Type { get; init; } = string.Empty;
}

public sealed record UpdateInventoryLocationRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(24)]
    public string Type { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

// ---- Suppliers (Phase 2 PROC-001) ----

public sealed record SupplierResponse
{
    public Guid Id { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? PaymentTerms { get; init; }
    public string? TaxId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record SupplierSearchResponse
{
    public IReadOnlyList<SupplierResponse> Items { get; init; } = Array.Empty<SupplierResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateSupplierRequest
{
    [Required, MaxLength(24)]
    public string SupplierCode { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(160)]
    public string? ContactPerson { get; init; }

    [MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

    [MaxLength(128)]
    public string? PaymentTerms { get; init; }

    [MaxLength(64)]
    public string? TaxId { get; init; }
}

public sealed record UpdateSupplierRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(160)]
    public string? ContactPerson { get; init; }

    [MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

    [MaxLength(128)]
    public string? PaymentTerms { get; init; }

    [MaxLength(64)]
    public string? TaxId { get; init; }

    public bool IsActive { get; init; } = true;
}

// ---- Stock ledger: positions, movements, receipts (Phase 2 INV-021..030) ----

public sealed record StockBalanceResponse
{
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public decimal OnHand { get; init; }
    public decimal Reserved { get; init; }
    public decimal Available { get; init; }
    public decimal? ReorderLevel { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record StockSearchResponse
{
    public IReadOnlyList<StockBalanceResponse> Items { get; init; } = Array.Empty<StockBalanceResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record StockMovementResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset BusinessDateUtc { get; init; }
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public string? Reason { get; init; }
    public string UserId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record MovementSearchResponse
{
    public IReadOnlyList<StockMovementResponse> Items { get; init; } = Array.Empty<StockMovementResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record ReceiveStockLineRequest
{
    public required Guid ProductId { get; init; }

    [Range(0.01, 9999999999999, ErrorMessage = "The quantity must be positive.")]
    public decimal Quantity { get; init; }

    [Range(0, 9999999999999)]
    public decimal? UnitCost { get; init; }
}

public sealed record ReceiveStockRequest
{
    public required Guid LocationId { get; init; }

    [MinLength(1)]
    public IReadOnlyList<ReceiveStockLineRequest> Lines { get; init; } = Array.Empty<ReceiveStockLineRequest>();

    public DateTimeOffset? BusinessDateUtc { get; init; }

    [MaxLength(128)]
    public string? Reference { get; init; }
}

// ---- Stock transfers (Phase 2 INV-025, Phase 3 §33) ----

public sealed record StockTransferLineResponse
{
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

public sealed record StockTransferResponse
{
    public Guid Id { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public Guid FromLocationId { get; init; }
    public string FromLocationCode { get; init; } = string.Empty;
    public Guid ToLocationId { get; init; }
    public string ToLocationCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? CancelledAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyList<StockTransferLineResponse> Lines { get; init; } = Array.Empty<StockTransferLineResponse>();
}

public sealed record StockTransferSearchResponse
{
    public IReadOnlyList<StockTransferResponse> Items { get; init; } = Array.Empty<StockTransferResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateStockTransferLineRequest
{
    public required Guid ProductId { get; init; }

    [Range(0.01, 9999999999999, ErrorMessage = "The quantity must be positive.")]
    public decimal Quantity { get; init; }

    [Range(0, 9999999999999)]
    public decimal? UnitCost { get; init; }
}

public sealed record CreateStockTransferRequest
{
    public required Guid FromLocationId { get; init; }

    public required Guid ToLocationId { get; init; }

    [MinLength(1)]
    public IReadOnlyList<CreateStockTransferLineRequest> Lines { get; init; } = Array.Empty<CreateStockTransferLineRequest>();
}

// ---- Stock counts (Phase 2 INV-040..045, Phase 3 §34) ----

public sealed record StockCountLineResponse
{
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal ExpectedQuantity { get; init; }
    public decimal? CountedQuantity { get; init; }
    public decimal Variance { get; init; }
}

public sealed record StockCountResponse
{
    public Guid Id { get; init; }
    public string CountNumber { get; init; } = string.Empty;
    public Guid LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? ApprovedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyList<StockCountLineResponse> Lines { get; init; } = Array.Empty<StockCountLineResponse>();
}

public sealed record StockCountSearchResponse
{
    public IReadOnlyList<StockCountResponse> Items { get; init; } = Array.Empty<StockCountResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record InitiateStockCountRequest
{
    public required Guid LocationId { get; init; }

    public Guid? ProductId { get; init; }
}

public sealed record RecordStockCountLineRequest
{
    public required Guid ProductId { get; init; }

    [Range(0, 9999999999999, ErrorMessage = "The counted quantity must not be negative.")]
    public decimal CountedQuantity { get; init; }
}

public sealed record RecordStockCountLinesRequest
{
    [MinLength(1)]
    public IReadOnlyList<RecordStockCountLineRequest> Items { get; init; } = Array.Empty<RecordStockCountLineRequest>();
}

// ---- Stock adjustments and wastage (Phase 2 INV-026/INV-027, Phase 3 §34) ----

public sealed record StockAdjustmentLineResponse
{
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

public sealed record StockAdjustmentResponse
{
    public Guid Id { get; init; }
    public string AdjustmentNumber { get; init; } = string.Empty;
    public string AdjustmentType { get; init; } = string.Empty;
    public Guid LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset BusinessDateUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset PostedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyList<StockAdjustmentLineResponse> Lines { get; init; } = Array.Empty<StockAdjustmentLineResponse>();
}

public sealed record StockAdjustmentSearchResponse
{
    public IReadOnlyList<StockAdjustmentResponse> Items { get; init; } = Array.Empty<StockAdjustmentResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateStockAdjustmentLineRequest
{
    public required Guid ProductId { get; init; }

    public decimal Quantity { get; init; }

    [Range(0, 9999999999999)]
    public decimal? UnitCost { get; init; }
}

public sealed record CreateStockAdjustmentRequest
{
    [Required, MaxLength(24)]
    public string AdjustmentType { get; init; } = string.Empty;

    public required Guid LocationId { get; init; }

    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<CreateStockAdjustmentLineRequest> Lines { get; init; } = Array.Empty<CreateStockAdjustmentLineRequest>();

    public DateTimeOffset? BusinessDateUtc { get; init; }
}