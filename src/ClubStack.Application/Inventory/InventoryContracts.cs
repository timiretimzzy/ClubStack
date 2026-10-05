namespace ClubStack.Application.Inventory;

// ---- DTOs ----

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    Guid CategoryId,
    string CategoryName,
    string UnitOfMeasure,
    string? Barcode,
    bool StockManaged,
    decimal Cost,
    string CurrencyCode,
    decimal? ReorderLevel,
    Guid? SupplierId,
    string? SupplierName,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string TaxTreatment = "Inclusive",
    Guid? TaxRateId = null,
    string? TaxRateName = null,
    decimal? TaxRate = null);

public sealed record ProductPriceDto(
    Guid Id,
    Guid ProductId,
    string PriceType,
    string CurrencyCode,
    decimal Amount,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record ProductCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record InventoryLocationDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SupplierDto(
    Guid Id,
    string SupplierCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? PaymentTerms,
    string? TaxId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record StockBalanceDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid LocationId,
    string LocationCode,
    string LocationName,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal? ReorderLevel,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record StockMovementDto(
    Guid Id,
    string Reference,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid LocationId,
    string LocationCode,
    decimal Quantity,
    string MovementType,
    decimal UnitCost,
    string CurrencyCode,
    DateTimeOffset BusinessDateUtc,
    string? SourceType,
    Guid? SourceId,
    string? Reason,
    string UserId,
    DateTimeOffset CreatedAtUtc);

public sealed record StockTransferLineDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitCost);

public sealed record StockTransferDto(
    Guid Id,
    string TransferNumber,
    Guid FromLocationId,
    string FromLocationCode,
    Guid ToLocationId,
    string ToLocationCode,
    string Status,
    string CreatedByUserId,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<StockTransferLineDto> Lines);

public sealed record StockCountLineDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal ExpectedQuantity,
    decimal? CountedQuantity,
    decimal Variance);

public sealed record StockCountDto(
    Guid Id,
    string CountNumber,
    Guid LocationId,
    string LocationCode,
    string Status,
    string CreatedByUserId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<StockCountLineDto> Lines);

public sealed record StockAdjustmentLineDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitCost);

/// <summary>Stock adjustments and wastage share one header; the type distinguishes them.</summary>
public sealed record StockAdjustmentDto(
    Guid Id,
    string AdjustmentNumber,
    string AdjustmentType,
    Guid LocationId,
    string LocationCode,
    string Reason,
    DateTimeOffset BusinessDateUtc,
    string CreatedByUserId,
    DateTimeOffset PostedAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<StockAdjustmentLineDto> Lines);

// ---- Search results ----

public sealed record ProductSearchResult(IReadOnlyList<ProductDto> Items, int TotalCount, int Page, int PageSize);

public sealed record StockSearchResult(IReadOnlyList<StockBalanceDto> Items, int TotalCount, int Page, int PageSize);

public sealed record MovementSearchResult(IReadOnlyList<StockMovementDto> Items, int TotalCount, int Page, int PageSize);

public sealed record StockTransferSearchResult(IReadOnlyList<StockTransferDto> Items, int TotalCount, int Page, int PageSize);

public sealed record StockCountSearchResult(IReadOnlyList<StockCountDto> Items, int TotalCount, int Page, int PageSize);

public sealed record StockAdjustmentSearchResult(IReadOnlyList<StockAdjustmentDto> Items, int TotalCount, int Page, int PageSize);

public sealed record SupplierSearchResult(IReadOnlyList<SupplierDto> Items, int TotalCount, int Page, int PageSize);

// ---- Inputs ----

public sealed record CreateProductInput(
    string Sku,
    string Name,
    Guid CategoryId,
    string UnitOfMeasure,
    string? Barcode,
    bool StockManaged,
    decimal Cost,
    decimal? PriceAmount,
    decimal? ReorderLevel,
    Guid? SupplierId,
    string? CurrencyCode,
    Guid? TaxRateId = null,
    string? TaxTreatment = null);

public sealed record UpdateProductInput(
    string Name,
    Guid CategoryId,
    string UnitOfMeasure,
    string? Barcode,
    bool StockManaged,
    decimal Cost,
    decimal? ReorderLevel,
    Guid? SupplierId,
    Guid? TaxRateId = null,
    string? TaxTreatment = null);

public sealed record SetProductPriceInput(decimal Amount, string? CurrencyCode);

public sealed record CreateCategoryInput(string Code, string Name, string? Description);

public sealed record UpdateCategoryInput(string Name, string? Description, bool IsActive);

public sealed record CreateInventoryLocationInput(string Code, string Name, string Type);

public sealed record UpdateInventoryLocationInput(string Name, string Type, bool IsActive);

public sealed record CreateSupplierInput(
    string SupplierCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? PaymentTerms,
    string? TaxId);

public sealed record UpdateSupplierInput(
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? PaymentTerms,
    string? TaxId,
    bool IsActive);

public sealed record ProductSearchInput(
    string? Query = null,
    Guid? CategoryId = null,
    bool? StockManagedOnly = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 50);

public sealed record StockSearchInput(
    Guid? ProductId = null,
    Guid? LocationId = null,
    bool? BelowReorderOnly = null,
    int Page = 1,
    int PageSize = 250);

public sealed record MovementSearchInput(
    Guid? ProductId = null,
    Guid? LocationId = null,
    string? MovementType = null,
    string? Query = null,
    DateTimeOffset? FromDateUtc = null,
    DateTimeOffset? ToDateUtc = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ReceiveStockLineInput(Guid ProductId, decimal Quantity, decimal? UnitCost);

public sealed record ReceiveStockInput(
    Guid LocationId,
    IReadOnlyList<ReceiveStockLineInput> Lines,
    DateTimeOffset? BusinessDateUtc,
    string? Reference);

public sealed record CreateStockTransferLineInput(Guid ProductId, decimal Quantity, decimal? UnitCost);

public sealed record CreateStockTransferInput(
    Guid FromLocationId,
    Guid ToLocationId,
    IReadOnlyList<CreateStockTransferLineInput> Lines);

public sealed record InitiateStockCountInput(Guid LocationId, Guid? ProductId);

public sealed record RecordStockCountLineInput(Guid ProductId, decimal CountedQuantity);

public sealed record RecordStockCountLinesInput(IReadOnlyList<RecordStockCountLineInput> Items);

public sealed record CreateStockAdjustmentLineInput(Guid ProductId, decimal Quantity, decimal? UnitCost);

public sealed record CreateStockAdjustmentInput(
    string AdjustmentType,
    Guid LocationId,
    string Reason,
    IReadOnlyList<CreateStockAdjustmentLineInput> Lines,
    DateTimeOffset? BusinessDateUtc);

// ---- Service interfaces ----

/// <summary>
/// Product catalogue (Phase 2 INV-001..010, Phase 3 §21-22, Phase 5 §53).
/// Prices are operational data persisted as Price rows so historical prices
/// remain unchanged when the selling price changes (Phase 3 §22).
/// </summary>
public interface IProductService
{
    Task<ProductSearchResult> SearchAsync(ProductSearchInput input, CancellationToken cancellationToken = default);

    Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductDto> CreateAsync(CreateProductInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateAsync(Guid id, UpdateProductInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<ProductDto> SetStatusAsync(Guid id, bool active, string actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceDto>> GetPricesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductPriceDto> SetPriceAsync(Guid id, SetProductPriceInput input, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Product categories (Phase 2 INV-004). Phase 8 §4.2 defines no category
/// permission; creation is gated by <c>Inventory.Product.Create</c> and edits by
/// <c>Inventory.Product.Edit</c>.</summary>
public interface IProductCategoryService
{
    Task<IReadOnlyList<ProductCategoryDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<ProductCategoryDto> CreateAsync(CreateCategoryInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<ProductCategoryDto> UpdateAsync(Guid id, UpdateCategoryInput input, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Stock locations (Phase 2 INV-020, Phase 3 §30).</summary>
public interface IInventoryLocationService
{
    Task<IReadOnlyList<InventoryLocationDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<InventoryLocationDto> CreateAsync(CreateInventoryLocationInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<InventoryLocationDto> UpdateAsync(Guid id, UpdateInventoryLocationInput input, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Supplier master data (Phase 2 PROC-001, Phase 3 §35). The procurement
/// workflow (requisitions, purchase orders, goods receipts, invoices) is out of
/// D4 scope; this is the shared supplier foundation later slices consume.</summary>
public interface ISupplierService
{
    Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SupplierDto> CreateAsync(CreateSupplierInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<SupplierSearchResult> SearchAsync(string? query = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stock ledger and positions (Phase 2 INV-021..030, Phase 3 §31-32, Phase 5 §54-55).
/// Positions are derived from the movement ledger; a cached balance row supports
/// reads but the ledger is authoritative. All stock-changing operations acquire a
/// per (product, location) advisory lock so concurrent changes never drive stock
/// negative or lose movements.
/// </summary>
public interface IStockLedgerService
{
    Task<StockSearchResult> GetStockAsync(StockSearchInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(Guid productId, Guid? locationId = null, int limit = 50, CancellationToken cancellationToken = default);

    Task<MovementSearchResult> SearchMovementsAsync(MovementSearchInput input, CancellationToken cancellationToken = default);

    /// <summary>Receives goods into a location, creating Receipt movements (INV-024).
    /// In D4 a receipt is recorded directly; a goods-receipt-driven flow arrives
    /// with procurement later.</summary>
    Task<IReadOnlyList<StockMovementDto>> ReceiveAsync(ReceiveStockInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<decimal> GetQuantityAsync(Guid productId, Guid locationId, CancellationToken cancellationToken = default);
}

/// <summary>Stock transfers (Phase 2 INV-025, Phase 3 §33). Completion atomically
/// creates the Transfer Out and Transfer In movements referencing the same
/// transfer; both directions are validated so stock never goes negative.</summary>
public interface IStockTransferService
{
    Task<StockTransferSearchResult> SearchAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<StockTransferDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StockTransferDto> CreateAsync(CreateStockTransferInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<StockTransferDto> CompleteAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<StockTransferDto> CancelAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Stock counts (Phase 2 INV-040..045, Phase 3 §34). Counted variance is
/// reconciliation-adjusted on approval via the elevated Inventory.Stock.ApproveAdjustment
/// permission; completed counts stay historical (INV-045).</summary>
public interface IStockCountService
{
    Task<StockCountSearchResult> SearchAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<StockCountDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StockCountDto> InitiateAsync(InitiateStockCountInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<StockCountDto> RecordLinesAsync(Guid id, RecordStockCountLinesInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<StockCountDto> CompleteAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<StockCountDto> ApproveAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<StockCountDto> CancelAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Stock adjustments and wastage (Phase 2 INV-026/INV-027, Phase 3 §34,
/// Phase 8 §5). Both are sensitive audited actions executed under their own
/// privileged permission and post their movement immediately; no two-step
/// approval workflow exists in D4 (mirrors the accepted D3 accounts decision).</summary>
public interface IStockAdjustmentService
{
    Task<StockAdjustmentSearchResult> SearchAsync(string? adjustmentType = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<StockAdjustmentDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentInput input, string actorUserId, CancellationToken cancellationToken = default);
}