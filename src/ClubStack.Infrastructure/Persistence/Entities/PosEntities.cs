using ClubStack.Domain.Inventory;
using ClubStack.Domain.Pos;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Point-of-sale outlet (Phase 8 §8.x <c>pos_outlets</c>, D5 POS). A POS outlet is
/// a front-of-house selling point; it is deliberately distinct from the
/// organisational <c>outlets</c> reference data and carries only POS state.
/// </summary>
public sealed class PosOutletEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public PosOutletStatus Status { get; set; } = PosOutletStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<PosRegisterEntity> Registers { get; set; } = new();
}

/// <summary>
/// POS register (Phase 8 §8.x <c>pos_registers</c>, D5 POS). A register is a
/// physical selling station bound to a POS outlet and the inventory location its
/// sales draw stock from.
/// </summary>
public sealed class PosRegisterEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PosOutletId { get; set; }

    public PosOutletEntity PosOutlet { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid InventoryLocationId { get; set; }

    public InventoryLocationEntity InventoryLocation { get; set; } = null!;

    public PosRegisterStatus Status { get; set; } = PosRegisterStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Cashier session (Phase 8 §8.x <c>cashier_sessions</c>, D5 Cashiering). A session
/// opens with a float, activates on its first sale, closes with a cash
/// declaration and is then reconciled. Expected cash and variance are derived;
/// variance is preserved through reconciliation.
/// </summary>
public sealed class CashierSessionEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string SessionNumber { get; set; } = string.Empty;

    public Guid RegisterId { get; set; }

    public PosRegisterEntity Register { get; set; } = null!;

    public string CashierUserId { get; set; } = string.Empty;

    public CashierSessionStatus Status { get; set; } = CashierSessionStatus.Open;

    public decimal OpeningFloat { get; set; }

    public decimal DeclaredCash { get; set; }

    public decimal ExpectedCash { get; set; }

    public decimal? Variance { get; set; }

    public bool VarianceApproved { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? ClosingStartedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public DateTimeOffset? ReconciledAtUtc { get; set; }

    public string? ReconciledByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<CashMovementEntity> Movements { get; set; } = new();

    public List<SaleEntity> Sales { get; set; } = new();
}

/// <summary>Cash movement on a cashier session (Phase 8 §8.x <c>cash_movements</c>).</summary>
public sealed class CashMovementEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SessionId { get; set; }

    public CashierSessionEntity Session { get; set; } = null!;

    public CashMovementType MovementType { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Signed contribution to the drawer (Float/PayIn positive, PayOut negative).</summary>
    public decimal SignedAmount { get; set; }

    public string? Reason { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Point-of-sale sale (Phase 8 §8.x <c>sales</c>, D5 POS). Completed sales are
/// immutable; corrections are refunds (see <c>sale_refunds</c>), never edits.
/// Lines, payments, discounts and the linked receipt are preserved as snapshots so
/// receipts and refunds are unaffected by later catalogue changes.
/// </summary>
public sealed class SaleEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string SaleNumber { get; set; } = string.Empty;

    public SaleStatus Status { get; set; } = SaleStatus.Open;

    public Guid RegisterId { get; set; }

    public PosRegisterEntity Register { get; set; } = null!;

    public Guid PosOutletId { get; set; }

    public PosOutletEntity? PosOutlet { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>Inventory location the sale draws stock from (the register's inventory location).</summary>
    public InventoryLocationEntity? Location { get; set; }

    public Guid? CashierSessionId { get; set; }

    public CashierSessionEntity? CashierSession { get; set; }

    public string CashierUserId { get; set; } = string.Empty;

    public Guid? MemberId { get; set; }

    public string? MemberNumber { get; set; }

    public string? MemberName { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset BusinessDateUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? VoidedAtUtc { get; set; }

    public string? VoidReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<SaleLineEntity> Lines { get; set; } = new();

    public List<SalePaymentEntity> Payments { get; set; } = new();

    public List<SaleDiscountEntity> Discounts { get; set; } = new();

    public List<ReceiptEntity> Receipts { get; set; } = new();

    public List<SaleRefundEntity> Refunds { get; set; } = new();
}

/// <summary>Sale line (Phase 8 §8.x <c>sale_lines</c>). Prices and tax are snapshotted.</summary>
public sealed class SaleLineEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SaleId { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    public Guid ProductId { get; set; }

    public ProductEntity? Product { get; set; }

    public string ProductSku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public bool StockManaged { get; set; }

    public decimal Quantity { get; set; }

    public decimal OriginalUnitPrice { get; set; }

    public decimal UnitPrice { get; set; }

    public SaleDiscountType? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }

    public string? DiscountReason { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxableBase { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public ProductTaxTreatment TaxTreatment { get; set; }

    public string? TaxRateName { get; set; }

    public decimal? TaxRate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Sale tender line (Phase 8 §8.x <c>sale_payments</c>). Also records the cash-drawer
/// counterpart: refunds disbursed in cash add an <see cref="IsRefund"/> row bound to
/// the cashier session that paid them out, so expected-cash accounting can journal
/// cash in and out without consulting non-relational payloads.
/// </summary>
public sealed class SalePaymentEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SaleId { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    /// <summary>Cashier session whose drawer this row contributes to (cash tenders and cash refunds only).</summary>
    public Guid? CashierSessionId { get; set; }

    public CashierSessionEntity? CashierSession { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentMethodEntity? PaymentMethod { get; set; }

    public string PaymentMethodCode { get; set; } = string.Empty;

    public string PaymentMethodName { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>False for sale tenders; true when the row records a cash refund disbursement.</summary>
    public bool IsRefund { get; set; }

    /// <summary>Portion of a sale tender already returned via refunds (outstanding tracking).</summary>
    public decimal? RefundedAmount { get; set; }

    public string? ExternalReference { get; set; }

    public Guid? AccountTransactionId { get; set; }

    public AccountTransactionEntity? AccountTransaction { get; set; }

    public string? AccountTransactionReference { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Applied discount (Phase 8 §8.x <c>sale_discounts</c>). Records the original and
/// effective price intent per line along with the authorising operator and reason;
/// discount application is permission-gated and audited.
/// </summary>
public sealed class SaleDiscountEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SaleId { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    /// <summary>Sale-level or line-level discount (forward compatibility; D5 applies line-level).</summary>
    public string Level { get; set; } = "Line";

    public Guid? LineId { get; set; }

    public SaleLineEntity? Line { get; set; }

    public SaleDiscountType DiscountType { get; set; }

    public decimal Value { get; set; }

    public string? Reason { get; set; }

    public string AuthorizedByUserId { get; set; } = string.Empty;

    public DateTimeOffset AppliedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Sale refund (Phase 8 §8.x <c>sale_refunds</c>, D5 POS §refunds). Refunds restore
/// stock (Return movements) and reverse account charges; the parallel ledger effect
/// means a refund is never editable either. Refund lines and tenders are stored as
/// JSON snapshots so the mandated table list is not extended.
/// </summary>
public sealed class SaleRefundEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string RefundNumber { get; set; } = string.Empty;

    public Guid SaleId { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    public string SaleNumber { get; set; } = string.Empty;

    public string Status { get; set; } = "Completed";

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string RefundedByUserId { get; set; } = string.Empty;

    public DateTimeOffset RefundedAtUtc { get; set; }

    public string LinesJson { get; set; } = "[]";

    public string PaymentsJson { get; set; } = "[]";

    public string? CorrelationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Sale receipt (Phase 8 §8.x <c>receipts</c>, D5 POS §23). Generated at completion
/// and re-printable; a single snapshot receipt row supports retrieval without
/// recomputation.
/// </summary>
public sealed class ReceiptEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid SaleId { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    public string SaleNumber { get; set; } = string.Empty;

    public string Status { get; set; } = "Issued";

    public Guid RegisterId { get; set; }

    public string RegisterCode { get; set; } = string.Empty;

    public Guid PosOutletId { get; set; }

    public string PosOutletCode { get; set; } = string.Empty;

    public string CashierUserId { get; set; } = string.Empty;

    public Guid? MemberId { get; set; }

    public string? MemberNumber { get; set; }

    public string? MemberName { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public DateTimeOffset BusinessDateUtc { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }
}