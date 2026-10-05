namespace ClubStack.Application.Finance;

/// <summary>
/// D6 outbox event type names. Every name maps to exactly one Phase 8 §14 posting
/// rule, and each event is enqueued inside the transaction that commits the
/// operational record it describes — so an operational row and its outbox message
/// commit atomically.
/// </summary>
public static class FinanceEventTypes
{
    /// <summary>Phase 8 §14.4 — membership fee assessment (D3 membership fees).</summary>
    public const string MembershipFeeAssessed = "Finance.MembershipFeeAssessed";

    /// <summary>Phase 8 §14.3 — a payment received into a member account (D3 accounts).</summary>
    public const string MemberAccountPayment = "Finance.MemberAccountPayment";
}

/// <summary>
/// Phase 8 §14.4. A membership fee assessment debits member receivable and credits
/// membership revenue; the amount is a single component, so the mappings use the
/// request's unnamed amount.
/// </summary>
public sealed record MembershipFeeAssessedEvent(
    Guid AssessmentId,
    Guid MembershipId,
    Guid MemberId,
    Guid MemberAccountId,
    string Reference,
    string Description,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset TransactionDateUtc,
    DateTimeOffset BusinessDateUtc,
    string CorrelationId);

/// <summary>
/// Phase 8 §14.3. A payment into a member account debits the cash/bank clearing
/// account and credits member receivable.
/// </summary>
public sealed record MemberAccountPaymentEvent(
    Guid AccountTransactionId,
    Guid MemberAccountId,
    Guid MemberId,
    string Reference,
    string Description,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset TransactionDateUtc,
    DateTimeOffset BusinessDateUtc,
    string CorrelationId);
