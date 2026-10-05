namespace ClubStack.Domain.Accounts;

/// <summary>
/// Member account lifecycle status (Phase 3 §17 Status, OpenedAt, ClosedAt). An
/// account that is Suspended or Closed refuses new charges; the state machine is
/// exercised operationally (Phase 8 error example: "cannot be charged while the
/// account is suspended").
/// </summary>
public enum MemberAccountStatus
{
    Active,
    Suspended,
    Closed,
}

/// <summary>
/// Ledger transaction kinds (Phase 3 §18, Phase 2 ACC-003..ACC-007). The ledger
/// is append-oriented; corrections use <see cref="Reversal"/> or a signed
/// <see cref="Adjustment"/> and never silently mutate history (ACC-013).
/// </summary>
public enum AccountTransactionType
{
    Charge,
    Payment,
    Credit,
    Adjustment,
    Reversal,
}

/// <summary>
/// Ledger posting state (Phase 3 §18 Status/ReversalReference). Reversed
/// originals remain stored but are excluded from derived balances; their
/// compensating <see cref="AccountTransactionType.Reversal"/> entry carries the
/// ReversalReference link.
/// </summary>
public enum AccountTransactionStatus
{
    Posted,
    Reversed,
}

/// <summary>
/// Explicit payment states (PAY-004). D3 records local payments; a failed
/// payment is never persisted as a successful ledger entry (PAY-007).
/// </summary>
public enum AccountPaymentState
{
    Recorded,
    Settled,
    Failed,
}