namespace ClubStack.Domain.Inventory;

/// <summary>
/// Product lifecycle status (Phase 2 INV-010, Phase 3 §21). Products can be
/// deactivated (inactive) to stop new operational use while retained for
/// history. Business records are never physically deleted (Phase 3 §70).
/// </summary>
public enum ProductStatus
{
    Active,
    Inactive,
}

/// <summary>
/// Product category status (Phase 2 INV-004). Categories group products for
/// operational classification; the code is the stable business key.
/// </summary>
public enum ProductCategoryStatus
{
    Active,
    Inactive,
}

/// <summary>
/// Stock location type (Phase 3 §30). Operational classification only; the
/// precise place is captured by the location code and name. The list is kept
/// general so club-specific structure remains configuration, not code
/// (Phase 1 §5.5).
/// </summary>
public enum StockLocationType
{
    Warehouse,
    Store,
    Kitchen,
    Service,
    Other,
}

/// <summary>
/// Stock location status (Phase 3 §30 Status). An inactive location still
/// explains historic balances but refuses new stock-changing operations
/// (Phase 9 §14: stock locations stay authoritative for history).
/// </summary>
public enum StockLocationStatus
{
    Active,
    Inactive,
}

/// <summary>
/// The stock ledger's movement kinds (Phase 3 §31, Phase 9 §14). D4 produces
/// Receipt, TransferIn, TransferOut, Adjustment, CountReconciliation and
/// Wastage movements only; Sale, Return, Damage and Consumption belong to later
/// slices (D5 POS, F&B) but are part of the canonical movement catalogue now so
/// historical transactions never need a schema migration when those slices land.
/// </summary>
public enum StockMovementType
{
    Receipt,
    Sale,
    TransferIn,
    TransferOut,
    Adjustment,
    CountReconciliation,
    Wastage,
    Damage,
    Return,
    Consumption,
}

/// <summary>
/// Stock transfer lifecycle (Phase 3 §33). Completion atomically creates the
/// linked Transfer Out + Transfer In movements; both sides reference the same
/// transfer (Phase 6 §11 acceptance: Created → Transferred).
/// </summary>
public enum StockTransferStatus
{
    Draft,
    Completed,
    Cancelled,
}

/// <summary>
/// Stock count lifecycle (Phase 2 INV-040..045, Phase 3 §34). Counted variance
/// is approved into CountReconciliation movements via the elevated
/// Inventory.Stock.ApproveAdjustment permission (Phase 8 §4.2). Approved is the
/// terminal state; completed counts remain historical (INV-045).
/// </summary>
public enum StockCountStatus
{
    InProgress,
    Completed,
    Approved,
    Cancelled,
}

/// <summary>
/// Stock adjustment/wastage kind (Phase 2 INV-026/INV-027, Phase 8 §5). Wastage
/// is a stock adjustment whose movement type is <see cref="StockMovementType.Wastage"/>;
/// both are sensitive actions that must be authorised and audited.
/// </summary>
public enum StockAdjustmentType
{
    Adjustment,
    Wastage,
}

/// <summary>
/// Supplier master-data status (Phase 3 §35, Phase 2 PROC-001). Deactivation
/// keeps the record available for historical supplier transactions.
/// </summary>
public enum SupplierStatus
{
    Active,
    Inactive,
}