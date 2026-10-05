using ClubStack.Domain.Inventory;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Product catalogue item (Phase 2 INV-001..010, Phase 3 §21, Phase 8 §8.7
/// <c>products</c>). The SKU is the stable, user-supplied unique key; the barcode is
/// an optional additional identifier. Pricing is kept in <see cref="ProductPriceEntity"/>
/// so historical prices remain unchanged when the selling price changes (Phase 3 §22).
/// ReorderLevel is a product-level safety stock used by stock alerts (Phase 2 INV-030).
/// </summary>
public sealed class ProductEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public ProductCategoryEntity Category { get; set; } = null!;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    /// <summary>True when the item participates in stock balances (Phase 3 §21 StockManaged).</summary>
    public bool StockManaged { get; set; }

    public decimal Cost { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal? ReorderLevel { get; set; }

    /// <summary>Preferred supplier (Phase 2 PROC-001/Product-Supplier); informational in D4.</summary>
    public Guid? SupplierId { get; set; }

    public SupplierEntity? Supplier { get; set; }

    /// <summary>
    /// Selling tax rate reference (D5 POS §7). Null means no tax configured, which
    /// sells as exempt. The rate name and fraction are snapshotted onto every sale
    /// line so receipts survive later rate changes.
    /// </summary>
    public Guid? TaxRateId { get; set; }

    public TaxRateEntity? TaxRate { get; set; }

    /// <summary>Selling tax treatment (D5 POS §7); see <see cref="ProductTaxTreatment"/>.</summary>
    public ProductTaxTreatment TaxTreatment { get; set; } = ProductTaxTreatment.Inclusive;

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<ProductPriceEntity> Prices { get; set; } = new();
}

/// <summary>Operational selling price rows (Phase 3 §22, Phase 8 §8.7 <c>product_prices</c>).</summary>
public sealed class ProductPriceEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }

    public ProductEntity Product { get; set; } = null!;

    /// <summary>Standard now; historical rows keep the price trail. Member reserved for D5 POS.</summary>
    public string PriceType { get; set; } = "Standard";

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTimeOffset EffectiveFromUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Product category (Phase 2 INV-004, Phase 8 §8.7 <c>product_categories</c>).</summary>
public sealed class ProductCategoryEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<ProductEntity> Products { get; set; } = new();
}

/// <summary>Stock location (Phase 2 INV-020, Phase 3 §30, Phase 8 §8.7 <c>inventory_locations</c>).</summary>
public sealed class InventoryLocationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public StockLocationType Type { get; set; }

    public StockLocationStatus Status { get; set; } = StockLocationStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Supplier master data (Phase 2 PROC-001, Phase 3 §35, Phase 8 §8.7 <c>suppliers</c>).</summary>
public sealed class SupplierEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string SupplierCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? PaymentTerms { get; set; }

    public string? TaxId { get; set; }

    public SupplierStatus Status { get; set; } = SupplierStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Cached stock position (Phase 3 §32, Phase 8 §8.7 <c>stock_balances</c>). Derived from
/// the movement ledger via the <see cref="StockLedger"/> formula; the ledger is always
/// authoritative (Phase 9 §14). One row per (product, location); updated transactionally
/// so balances never diverge from the ledger.
/// </summary>
public sealed class StockBalanceEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }

    public ProductEntity Product { get; set; } = null!;

    public Guid LocationId { get; set; }

    public InventoryLocationEntity Location { get; set; } = null!;

    public decimal Quantity { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A finalized stock movement (Phase 2 INV-023/INV-026, Phase 3 §31-32, Phase 8 §8.7
/// <c>stock_movements</c>). Movement immutable once written; corrections are audited
/// compensations (Phase 10 §10). Quantity is the signed ledger contribution (positive in,
/// negative out) so the derived quantity is strictly <c>sum(Quantity)</c>. Reason carries
/// operational context (e.g. wastage cause) for movement history drill-down (Phase 5 §55).
/// </summary>
public sealed class StockMovementEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public ProductEntity Product { get; set; } = null!;

    public Guid LocationId { get; set; }

    public InventoryLocationEntity Location { get; set; } = null!;

    /// <summary>Signed contribution to the ledger (positive in, negative out).</summary>
    public decimal Quantity { get; set; }

    public StockMovementType MovementType { get; set; }

    public decimal UnitCost { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTimeOffset BusinessDateUtc { get; set; }

    /// <summary>Source document family (StockTransfer / StockCount / StockAdjustment).</summary>
    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public string? Reason { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Stock transfer header (Phase 2 INV-025, Phase 3 §33, Phase 8 §8.7 <c>stock_transfers</c>).</summary>
public sealed class StockTransferEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string TransferNumber { get; set; } = string.Empty;

    public Guid FromLocationId { get; set; }

    public InventoryLocationEntity FromLocation { get; set; } = null!;

    public Guid ToLocationId { get; set; }

    public InventoryLocationEntity ToLocation { get; set; } = null!;

    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<StockTransferLineEntity> Lines { get; set; } = new();
}

/// <summary>Stock transfer line (Phase 8 §8.7 <c>stock_transfer_lines</c>).</summary>
public sealed class StockTransferLineEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TransferId { get; set; }

    public StockTransferEntity Transfer { get; set; } = null!;

    public Guid ProductId { get; set; }

    public ProductEntity Product { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}

/// <summary>Stock count header (Phase 2 INV-040..045, Phase 3 §34, Phase 8 §8.7 <c>stock_counts</c>).</summary>
public sealed class StockCountEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string CountNumber { get; set; } = string.Empty;

    public Guid LocationId { get; set; }

    public InventoryLocationEntity Location { get; set; } = null!;

    public StockCountStatus Status { get; set; } = StockCountStatus.InProgress;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<StockCountLineEntity> Lines { get; set; } = new();
}

/// <summary>
/// Stock count line: expected (ledger) vs counted with the arithmetic variance
/// (Phase 8 §8.7 <c>stock_count_lines</c>, Phase 3 §34 count-variance → adjustment).
/// </summary>
public sealed class StockCountLineEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CountId { get; set; }

    public StockCountEntity Count { get; set; } = null!;

    public Guid ProductId { get; set; }

    public ProductEntity Product { get; set; } = null!;

    public decimal ExpectedQuantity { get; set; }

    public decimal? CountedQuantity { get; set; }

    /// <summary>Counted - expected; source of the CountReconciliation movement.</summary>
    public decimal Variance { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Manual stock adjustment or wastage header (Phase 2 INV-026/INV-027, Phase 3 §34,
/// Phase 8 §8.7 <c>stock_adjustments</c> + <c>stock_adjustment_lines</c>). Type
/// distinguishes Adjustment from Wastage; both are sensitive, privileged, audited
/// actions that post their movement immediately.
/// </summary>
public sealed class StockAdjustmentEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string AdjustmentNumber { get; set; } = string.Empty;

    public StockAdjustmentType AdjustmentType { get; set; }

    public Guid LocationId { get; set; }

    public InventoryLocationEntity Location { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset BusinessDateUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset PostedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<StockAdjustmentLineEntity> Lines { get; set; } = new();
}

/// <summary>Stock adjustment / wastage line (signed quantity, per Phase 8 §8.7).</summary>
public sealed class StockAdjustmentLineEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AdjustmentId { get; set; }

    public StockAdjustmentEntity Adjustment { get; set; } = null!;

    public Guid ProductId { get; set; }

    public ProductEntity Product { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}