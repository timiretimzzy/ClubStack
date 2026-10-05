using ClubStack.Application.Finance;
using ClubStack.Domain.Finance;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Chart of accounts (D6). <c>Reference</c> is the FA-###### business reference
/// (B12); <c>Code</c> is the human account code. <c>NormalBalance</c> is derived
/// from <c>AccountType</c> per B3 and is stored so historical rows keep the
/// balance they were posted under. Accounts are inactivated, never deleted.
/// </summary>
public sealed class FinancialAccountEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public FinancialAccountType AccountType { get; set; }

    public NormalBalance NormalBalance { get; set; }

    public FinancialAccountStatus Status { get; set; } = FinancialAccountStatus.Active;

    public Guid? ParentAccountId { get; set; }

    public FinancialAccountEntity? ParentAccount { get; set; }

    public List<FinancialAccountEntity> ChildAccounts { get; set; } = new();

    public string? Description { get; set; }

    /// <summary>True for accounts seeded by D6; user accounts may be deactivated by operators.</summary>
    public bool IsSystem { get; set; }

    public bool IsPostingAllowed { get; set; } = true;

    /// <summary>
    /// Report-scoping flag for the B16 Cash Position report. It is deliberately not an
    /// account type, because B2 fixes the account types to exactly five. No account
    /// code is hard-coded anywhere; the seeded chart of accounts is what sets this.
    /// </summary>
    public FinancialCashKind CashKind { get; set; } = FinancialCashKind.None;

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<JournalLineEntity> JournalLines { get; set; } = new();
}

/// <summary>
/// A financial posting rule. The eight seeded codes are the authoritative Phase 8
/// §14.1-§14.8 identities (B7); no other rule identity may be added without a PM
/// decision.
/// </summary>
public sealed class FinancialPostingRuleEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Authoritative source section, e.g. "Phase 8 14.1".</summary>
    public string SourceSection { get; set; } = string.Empty;

    public FinancialPostingStrategy PostingStrategy { get; set; }

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<FinancialPostingRuleMappingEntity> Mappings { get; set; } = new();
}

/// <summary>
/// Maps a posting rule to a financial account (B7: rule + sequence + direction +
/// account + optional selector). The posting engine reads only these rows, so
/// account identifiers are never hard-coded in a posting service.
/// </summary>
public sealed class FinancialPostingRuleMappingEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RuleId { get; set; }

    public FinancialPostingRuleEntity Rule { get; set; } = null!;

    public int Sequence { get; set; }

    public JournalDirection Direction { get; set; }

    public Guid FinancialAccountId { get; set; }

    public FinancialAccountEntity FinancialAccount { get; set; } = null!;

    /// <summary>
    /// Optional amount component or routing key. Null means "the request's single
    /// unnamed amount"; otherwise it must match an amount supplied under the same
    /// selector (for example <c>Gross</c>, <c>Net</c>, <c>Tax</c>).
    /// </summary>
    public string? Selector { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Financial period. <see cref="Year"/>/<see cref="Month"/> are unique because the
/// period is resolved from the journal TransactionDate (B6). Only Open and Closed
/// exist (B5).
/// </summary>
public sealed class FinancialPeriodEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public FinancialPeriodStatus Status { get; set; } = FinancialPeriodStatus.Open;

    public DateTimeOffset OpenedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string OpenedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public string? ClosedByUserId { get; set; }

    public DateTimeOffset? ReopenedAtUtc { get; set; }

    public string? ReopenedByUserId { get; set; }

    public string? ReopenReason { get; set; }

    public int ReopenCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<JournalEntity> Journals { get; set; } = new();
}

/// <summary>
/// A journal posting. <see cref="Reference"/> is the JRN-###### business reference
/// (B12). <see cref="SourceType"/> + <see cref="SourceId"/> are the duplicate
/// source identity required by Phase 8 §15. A reversal is a separate row linked
/// through <see cref="ReversalOfJournalId"/>; the original is never modified
/// (B1/B4).
/// </summary>
public sealed class JournalEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public JournalStatus Status { get; set; } = JournalStatus.Draft;

    public string Description { get; set; } = string.Empty;

    public Guid FinancialPeriodId { get; set; }

    public FinancialPeriodEntity FinancialPeriod { get; set; } = null!;

    /// <summary>B6: the TransactionDate is what determines the financial period.</summary>
    public DateTimeOffset TransactionDate { get; set; }

    /// <summary>B6: retained as operational metadata only; it never selects the period.</summary>
    public DateTimeOffset BusinessDate { get; set; }

    public string TransactionCurrencyCode { get; set; } = string.Empty;

    public string BaseCurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public DateTimeOffset RateDateUtc { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public string? SourceReference { get; set; }

    public Guid? ReversalOfJournalId { get; set; }

    public JournalEntity? ReversalOfJournal { get; set; }

    public Guid? ReceivableId { get; set; }

    public AccountsReceivableEntity? Receivable { get; set; }

    public Guid? PayableId { get; set; }

    public AccountsPayableEntity? Payable { get; set; }

    public DateTimeOffset? PostedAtUtc { get; set; }

    public string? PostedByUserId { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string? CancelledReason { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<JournalLineEntity> Lines { get; set; } = new();
}

/// <summary>
/// Journal line. B8 fixes the field set: the account sits on the line and the
/// direction is an explicit Debit/Credit (B10). <see cref="SourceLineId"/> links a
/// reversal line back to the line it compensates.
/// </summary>
public sealed class JournalLineEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid JournalId { get; set; }

    public JournalEntity Journal { get; set; } = null!;

    public int LineNumber { get; set; }

    public Guid FinancialAccountId { get; set; }

    public FinancialAccountEntity FinancialAccount { get; set; } = null!;

    public JournalDirection Direction { get; set; }

    /// <summary>Transaction-currency amount (B17: decimal(19,4)).</summary>
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Base-currency amount (B17: decimal(19,4)).</summary>
    public decimal BaseAmount { get; set; }

    public string? Description { get; set; }

    /// <summary>B4: the original line this line compensates, for reversal journals only.</summary>
    public Guid? SourceLineId { get; set; }

    public JournalLineEntity? SourceLine { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Accounts receivable (B13). Member identity is the existing
/// Member/MemberAccount — no duplicate member master. (SourceType, SourceId) is
/// unique so one operational charge can never become two receivables.
/// </summary>
public sealed class AccountsReceivableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public Guid MemberId { get; set; }

    public MemberEntity Member { get; set; } = null!;

    public Guid? MemberAccountId { get; set; }

    public MemberAccountEntity? MemberAccount { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public string? SourceReference { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public ReceivableStatus Status { get; set; } = ReceivableStatus.Open;

    public Guid? JournalId { get; set; }

    public JournalEntity? Journal { get; set; }

    public string? Description { get; set; }

    public string? CancelledReason { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<AccountsReceivableAllocationEntity> Allocations { get; set; } = new();
}

/// <summary>Allocation of a finance payment to a receivable (B13: "Use allocation records").</summary>
public sealed class AccountsReceivableAllocationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ReceivableId { get; set; }

    public AccountsReceivableEntity Receivable { get; set; } = null!;

    public Guid PaymentId { get; set; }

    public PaymentEntity Payment { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTimeOffset AllocatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string AllocatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Accounts payable (B13: mirrors AR for suppliers). Supplier identity is the
/// existing Supplier — no duplicate supplier master.
/// </summary>
public sealed class AccountsPayableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public SupplierEntity Supplier { get; set; } = null!;

    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public string? SourceReference { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public PayableStatus Status { get; set; } = PayableStatus.Open;

    public Guid? JournalId { get; set; }

    public JournalEntity? Journal { get; set; }

    public string? Description { get; set; }

    public string? CancelledReason { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<AccountsPayableAllocationEntity> Allocations { get; set; } = new();
}

/// <summary>Allocation of a finance payment to a payable (B13).</summary>
public sealed class AccountsPayableAllocationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PayableId { get; set; }

    public AccountsPayableEntity Payable { get; set; } = null!;

    public Guid PaymentId { get; set; }

    public PaymentEntity Payment { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTimeOffset AllocatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string AllocatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Finance payment (§10): the accounting representation of money received from a
/// member or paid to a supplier, and the mechanism that allocates it to
/// receivables/payables. It is not a second POS tender ledger. Idempotency is
/// enforced by a unique key on IdempotencyKey and on (SourceType, SourceId).
/// </summary>
public sealed class PaymentEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public PaymentDirection Direction { get; set; }

    public Guid? MemberId { get; set; }

    public MemberEntity? Member { get; set; }

    public Guid? SupplierId { get; set; }

    public SupplierEntity? Supplier { get; set; }

    public Guid? MemberAccountId { get; set; }

    public MemberAccountEntity? MemberAccount { get; set; }

    /// <summary>The operational account transaction this payment accounts for, when it originated in D3.</summary>
    public Guid? AccountTransactionId { get; set; }

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public string? SourceReference { get; set; }

    public decimal Amount { get; set; }

    public decimal UnappliedAmount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset? ValueDate { get; set; }

    public string? PaymentMethodCode { get; set; }

    public string? ExternalReference { get; set; }

    public Guid? JournalId { get; set; }

    public JournalEntity? Journal { get; set; }

    public string? Description { get; set; }

    public string? IdempotencyKey { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<FinancePaymentAllocationEntity> Allocations { get; set; } = new();
}

/// <summary>
/// One allocation of a finance payment to a receivable or payable (B13). Distinct
/// from the D3 <c>PaymentAllocationEntity</c>, which settles a member-account charge
/// from an account transaction; this record allocates to a D6 finance document.
/// </summary>
public sealed class FinancePaymentAllocationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PaymentId { get; set; }

    public PaymentEntity Payment { get; set; } = null!;

    public Guid? ReceivableId { get; set; }

    public AccountsReceivableEntity? Receivable { get; set; }

    public Guid? PayableId { get; set; }

    public AccountsPayableEntity? Payable { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Manual reconciliation (B15). There is no bank API and no statement feed: a user
/// records the statement closing balance, matches ledger journals by hand, and
/// finalizes with the resulting variance.
/// </summary>
public sealed class FinancialReconciliationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Reference { get; set; } = string.Empty;

    public Guid FinancialAccountId { get; set; }

    public FinancialAccountEntity FinancialAccount { get; set; } = null!;

    public DateTimeOffset StatementDateUtc { get; set; }

    public decimal StatementClosingBalance { get; set; }

    public decimal? LedgerClosingBalance { get; set; }

    public decimal? Variance { get; set; }

    public FinancialReconciliationStatus Status { get; set; } = FinancialReconciliationStatus.InProgress;

    public string? Note { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? CompletedByUserId { get; set; }

    public string? CancelledReason { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<FinancialReconciliationMatchEntity> Matches { get; set; } = new();
}

/// <summary>One hand-matched ledger journal inside a reconciliation (B15).</summary>
public sealed class FinancialReconciliationMatchEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ReconciliationId { get; set; }

    public FinancialReconciliationEntity Reconciliation { get; set; } = null!;

    public Guid JournalId { get; set; }

    public JournalEntity Journal { get; set; } = null!;

    public decimal LedgerAmount { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset MatchedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string MatchedByUserId { get; set; } = string.Empty;
}
