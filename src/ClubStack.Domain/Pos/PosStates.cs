namespace ClubStack.Domain.Pos;

/// <summary>
/// Point-of-sale outlet lifecycle (Phase 8 <c>pos_outlets</c>, D5 POS). A POS
/// outlet is a front-of-house selling point (e.g. the clubhouse shop, a bar or
/// the pro shop). It is deliberately independent of the organisational
/// <c>outlets</c> reference data: it carries only the POS-relevant state.
/// </summary>
public enum PosOutletStatus
{
    Active,
    Inactive,
}

/// <summary>
/// POS register lifecycle (Phase 8 <c>pos_registers</c>, D5 POS). A register is
/// a physical selling station bound to a POS outlet and to the inventory
/// location its sales draw stock from.
/// </summary>
public enum PosRegisterStatus
{
    Active,
    Inactive,
}

/// <summary>
/// Sale lifecycle (D5 POS §state machine). A sale starts <see cref="Open"/> once
/// the goods are scanned/selected. Completing a sale moves it through
/// <see cref="PendingPayment"/> (authorising payments, charging accounts and
/// posting stock inside the completion transaction) to <see cref="Completed"/>.
/// An <see cref="Open"/> sale may be <see cref="Voided"/> before any tender;
/// a <see cref="PendingPayment"/> attempt that must not proceed is
/// <see cref="Cancelled"/>. Completed sales are immutable; corrections are
/// refunds (see <c>sale_refunds</c>), never edits.
/// </summary>
public enum SaleStatus
{
    Open,
    PendingPayment,
    Completed,
    Voided,
    Cancelled,
}

/// <summary>Discount mode used by a sale line discount (D5 POS §discounts).</summary>
public enum SaleDiscountType
{
    Amount,
    Percent,
}

/// <summary>
/// Cashier session lifecycle (D5 Cashiering §state machine). Opening a session
/// records the opening float and creates an <see cref="Open"/> session. The
/// first sale created on the session activates it (<see cref="Active"/>). The
/// cashier starts closing (preparing the closing declaration:
/// <see cref="Closing"/>), finishes it (<see cref="Closed"/>, variance
/// preserved) and a supervisor reconciles it (<see cref="Reconciled"/>).
/// </summary>
public enum CashierSessionStatus
{
    Open,
    Active,
    Closing,
    Closed,
    Reconciled,
}

/// <summary>
/// Cash movement kinds (D5 Cashiering §cash movements). <see cref="Float"/>
/// records the opening float. <see cref="PayIn"/> adds cash to the drawer and
/// <see cref="PayOut"/> removes cash from the drawer (e.g. safe drops or petty
/// payouts); both affect the session's expected cash.
/// </summary>
public enum CashMovementType
{
    Float,
    PayIn,
    PayOut,
}

/// <summary>
/// Authoritative sale transitions (D5 POS §state machine). Open → PendingPayment
/// and Open → Voided; PendingPayment → Completed and PendingPayment → Cancelled.
/// A completed sale never changes; corrections use refunds.
/// </summary>
public static class SaleStateTransitions
{
    public static bool CanTransition(SaleStatus from, SaleStatus to)
    {
        return (from, to) switch
        {
            (SaleStatus.Open, SaleStatus.PendingPayment) => true,
            (SaleStatus.Open, SaleStatus.Voided) => true,
            (SaleStatus.PendingPayment, SaleStatus.Completed) => true,
            (SaleStatus.PendingPayment, SaleStatus.Cancelled) => true,
            _ => false,
        };
    }
}

/// <summary>
/// Authoritative cashier session transitions (D5 Cashiering §state machine).
/// Open → Active (automatic on the first sale), Active → Closing → Closed, and
/// Closed → Reconciled. A closed session is never silently reopened.
/// </summary>
public static class CashierSessionStateTransitions
{
    public static bool CanTransition(CashierSessionStatus from, CashierSessionStatus to)
    {
        return (from, to) switch
        {
            (CashierSessionStatus.Open, CashierSessionStatus.Active) => true,
            (CashierSessionStatus.Active, CashierSessionStatus.Closing) => true,
            (CashierSessionStatus.Closing, CashierSessionStatus.Closed) => true,
            (CashierSessionStatus.Closed, CashierSessionStatus.Reconciled) => true,
            _ => false,
        };
    }
}