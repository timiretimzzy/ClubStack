using ClubStack.Domain.Accounts;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Member account relationship (ACC-001, Phase 3 §17, Phase 8 §8.5
/// <c>member_accounts</c>). The account is not the member; one account exists per
/// member and balances are derived from account transactions.
/// </summary>
public sealed class MemberAccountEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MemberId { get; set; }

    public MemberEntity Member { get; set; } = null!;

    public string AccountNumber { get; set; } = string.Empty;

    public MemberAccountStatus Status { get; set; } = MemberAccountStatus.Active;

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTimeOffset OpenedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<AccountTransactionEntity> Transactions { get; set; } = new();
}

/// <summary>
/// One ledger entry (Phase 3 §18, Phase 8 §8.5 <c>account_transactions</c>).
/// Amounts for charges are magnitudes; adjustments and reversals store their
/// signed value. The balance is the sum of the signed contributions of Posted
/// entries (ACC-002/ACC-010, Phase 3 §19).
/// </summary>
public sealed class AccountTransactionEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AccountId { get; set; }

    public MemberAccountEntity Account { get; set; } = null!;

    public AccountTransactionType TransactionType { get; set; }

    public AccountTransactionStatus Status { get; set; } = AccountTransactionStatus.Posted;

    public AccountPaymentState? PaymentState { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string? ExternalReference { get; set; }

    /// <summary>Reason captured for adjustments and reversals (ACC-013/ACC-014).</summary>
    public string? Reason { get; set; }

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public Guid? ReverseOfTransactionId { get; set; }

    public AccountTransactionEntity? ReverseOfTransaction { get; set; }

    public Guid? PaymentMethodId { get; set; }

    public PaymentMethodEntity? PaymentMethod { get; set; }

    public DateTimeOffset TransactionDateUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset BusinessDateUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<PaymentAllocationEntity> AllocationsSent { get; set; } = new();

    public List<PaymentAllocationEntity> AllocationsReceived { get; set; } = new();
}

/// <summary>
/// Settlement link from a payment/credit to an eligible charge (ACC-008, AR-003,
/// Phase 3 §20, Phase 8 §8.5 <c>payment_allocations</c>). One payment may settle
/// multiple charges; the remainder stays unallocated (ACC-009).
/// </summary>
public sealed class PaymentAllocationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AccountId { get; set; }

    public MemberAccountEntity Account { get; set; } = null!;

    /// <summary>The payment or credit that allocated funds.</summary>
    public Guid FromTransactionId { get; set; }

    public AccountTransactionEntity FromTransaction { get; set; } = null!;

    /// <summary>The charge being settled.</summary>
    public Guid ToChargeTransactionId { get; set; }

    public AccountTransactionEntity ToChargeTransaction { get; set; } = null!;

    public decimal AllocatedAmount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Generated statement header (ACC-011, Phase 8 §8.5 <c>account_statements</c>).
/// The statement is a snapshot: opening balance, transaction range, closing
/// balance, generator and number; detail is reproducible from the ledger.
/// </summary>
public sealed class AccountStatementEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AccountId { get; set; }

    public MemberAccountEntity Account { get; set; } = null!;

    public string StatementNumber { get; set; } = string.Empty;

    public DateTimeOffset FromDateUtc { get; set; }

    public DateTimeOffset ToDateUtc { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal ClosingBalance { get; set; }

    public string GeneratedByUserId { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}