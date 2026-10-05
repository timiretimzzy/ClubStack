namespace ClubStack.Domain.Finance;

/// <summary>
/// Chart-of-accounts classification (D6 PM Decision Pack B2). The five values are
/// authoritative; no additional account type may be introduced without a PM
/// decision.
/// </summary>
public enum FinancialAccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4,
}

/// <summary>
/// Normal balance of an account (D6 PM Decision Pack B3):
/// Asset/Expense = Debit, Liability/Equity/Revenue = Credit.
/// </summary>
public enum NormalBalance
{
    Debit = 0,
    Credit = 1,
}

/// <summary>
/// Accounts are inactivated rather than deleted so historical journals keep a
/// valid account reference.
/// </summary>
public enum FinancialAccountStatus
{
    Active = 0,
    Inactive = 1,
}

/// <summary>
/// Report-scoping classification used only by the B16 Cash Position report. It is
/// deliberately not an account type: B2 fixes the account types to exactly five.
/// Default <c>None</c> keeps every user-created account out of Cash Position until
/// an operator classifies it.
/// </summary>
public enum FinancialCashKind
{
    None = 0,
    CashOnHand = 1,
    Bank = 2,
}

/// <summary>
/// Journal lifecycle (D6 PM Decision Pack B1). Draft may become Posted or
/// Cancelled. Posted is terminal and immutable; there is deliberately no
/// <c>Reversed</c> state because a reversal is a new compensating journal.
/// </summary>
public enum JournalStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>
/// Side of a journal line (D6 PM Decision Pack B10): the Phase 9 model wins, so the
/// account sits on the line and each line carries an explicit Debit/Credit
/// direction instead of a signed amount.
/// </summary>
public enum JournalDirection
{
    Debit = 0,
    Credit = 1,
}

/// <summary>Financial period lifecycle (D6 PM Decision Pack B5).</summary>
public enum FinancialPeriodStatus
{
    Open = 0,
    Closed = 1,
}

/// <summary>
/// Receivable status (D6 PM Decision Pack B13).
/// </summary>
public enum ReceivableStatus
{
    Open = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Cancelled = 3,
}

/// <summary>
/// Payable status. Mirrors <see cref="ReceivableStatus"/> (B13: "AP mirrors AR").
/// </summary>
public enum PayableStatus
{
    Open = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Cancelled = 3,
}

/// <summary>
/// Direction of a finance payment: cash/bank received settles a receivable,
/// cash/bank paid settles a payable.
/// </summary>
public enum PaymentDirection
{
    /// <summary>Money received from a member; settles accounts receivable.</summary>
    Received = 0,

    /// <summary>Money paid to a supplier; settles accounts payable.</summary>
    Made = 1,
}

/// <summary>
/// Manual bank/cash reconciliation lifecycle (D6 PM Decision Pack B15). There is
/// no bank feed and no automatic statement import, so a reconciliation is opened
/// by a user, matched by hand, and then finalized.
/// </summary>
public enum FinancialReconciliationStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2,
}
