using ClubStack.Domain.Finance;

namespace ClubStack.Application.Finance;

// ---------------------------------------------------------------------------
// Chart of accounts, base currency and posting-rule configuration (B2, B3, B7, B17)
// ---------------------------------------------------------------------------

public sealed record FinancialAccountDto(
    Guid Id,
    string Reference,
    string Code,
    string Name,
    FinancialAccountType AccountType,
    NormalBalance NormalBalance,
    FinancialAccountStatus Status,
    Guid? ParentAccountId,
    string? Description,
    bool IsSystem,
    FinancialCashKind CashKind,
    bool IsPostingAllowed,
    string CurrencyCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateFinancialAccountInput(
    string Code,
    string Name,
    FinancialAccountType AccountType,
    Guid? ParentAccountId,
    string? Description,
    bool IsPostingAllowed,
    FinancialCashKind CashKind,
    string CurrencyCode);

public sealed record UpdateFinancialAccountInput(
    string Name,
    Guid? ParentAccountId,
    string? Description,
    bool IsPostingAllowed,
    FinancialCashKind CashKind);

public sealed record FinancialAccountSearchInput(
    string? Code,
    string? Name,
    FinancialAccountType? AccountType,
    FinancialAccountStatus? Status,
    int Page = 1,
    int PageSize = 50);

public sealed record FinancialAccountSearchResult(
    IReadOnlyList<FinancialAccountDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record FinancialPostingRuleMappingDto(
    Guid Id,
    Guid RuleId,
    string RuleCode,
    int Sequence,
    JournalDirection Direction,
    Guid FinancialAccountId,
    string FinancialAccountCode,
    string FinancialAccountName,
    string? Selector);

public sealed record FinancialPostingRuleDto(
    Guid Id,
    string Code,
    string Name,
    string SourceSection,
    FinancialPostingStrategy Strategy,
    string Description,
    IReadOnlyList<FinancialPostingRuleMappingDto> Mappings);

public sealed record UpsertPostingRuleMappingInput(
    int Sequence,
    JournalDirection Direction,
    Guid FinancialAccountId,
    string? Selector);

/// <summary>Chart of accounts, club base currency and posting-rule configuration.</summary>
public interface IFinancialAccountService
{
    Task<FinancialAccountSearchResult> SearchAccountsAsync(
        FinancialAccountSearchInput input,
        CancellationToken cancellationToken = default);

    Task<FinancialAccountDto> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialAccountDto>> GetSystemAccountsAsync(CancellationToken cancellationToken = default);

    Task<FinancialAccountDto> CreateAccountAsync(
        CreateFinancialAccountInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialAccountDto> UpdateAccountAsync(
        Guid id,
        UpdateFinancialAccountInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Deactivates an account; accounts are never destructively deleted (inactivation convention).</summary>
    Task<FinancialAccountDto> DeactivateAccountAsync(
        Guid id,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>B17: the club-level configurable base currency.</summary>
    Task<string> GetBaseCurrencyAsync(CancellationToken cancellationToken = default);

    Task SetBaseCurrencyAsync(string currencyCode, string actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialPostingRuleDto>> GetPostingRulesAsync(CancellationToken cancellationToken = default);

    Task<FinancialPostingRuleMappingDto> UpsertPostingRuleMappingAsync(
        string ruleCode,
        UpsertPostingRuleMappingInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task DeletePostingRuleMappingAsync(
        string ruleCode,
        Guid mappingId,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Journals (B1, B4, B8, B9, B10, B12, B19)
// ---------------------------------------------------------------------------

public sealed record JournalLineDto(
    Guid Id,
    int LineNumber,
    Guid FinancialAccountId,
    string FinancialAccountCode,
    string FinancialAccountName,
    JournalDirection Direction,
    decimal Amount,
    string CurrencyCode,
    decimal BaseAmount,
    string? Description,
    Guid? SourceLineId);

public sealed record JournalDto(
    Guid Id,
    string Reference,
    JournalStatus Status,
    string Description,
    Guid FinancialPeriodId,
    string FinancialPeriodReference,
    DateTimeOffset TransactionDate,
    DateTimeOffset BusinessDate,
    string TransactionCurrencyCode,
    string BaseCurrencyCode,
    decimal ExchangeRate,
    DateTimeOffset RateDateUtc,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    Guid? ReversalOfJournalId,
    string? ReversalOfJournalReference,
    Guid? ReversedByJournalId,
    Guid? ReceivableId,
    Guid? PayableId,
    decimal TotalDebit,
    decimal TotalCredit,
    DateTimeOffset? PostedAtUtc,
    string? PostedByUserId,
    string? CancelledReason,
    string CreatedByUserId,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<JournalLineDto> Lines);

public sealed record CreateJournalLineInput(
    Guid FinancialAccountId,
    JournalDirection Direction,
    decimal Amount,
    string? Description);

public sealed record CreateJournalInput(
    string Description,
    DateTimeOffset TransactionDate,
    DateTimeOffset? BusinessDate,
    string CurrencyCode,
    decimal? ExchangeRate,
    DateTimeOffset? RateDateUtc,
    Guid? FinancialPeriodId,
    IReadOnlyList<CreateJournalLineInput> Lines,
    string? SourceReference,
    Guid? ReversalOfJournalId = null);

public sealed record CancelJournalInput(string Reason);

public sealed record ReverseJournalInput(string Reason, DateTimeOffset? TransactionDate = null);

public sealed record JournalValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal TotalDebit,
    decimal TotalCredit,
    int LineCount);

public sealed record JournalSearchInput(
    JournalStatus? Status,
    Guid? FinancialPeriodId,
    Guid? FinancialAccountId,
    string? SourceType,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record JournalSearchResult(
    IReadOnlyList<JournalDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Journal lifecycle: draft, post, cancel and B4 reversal.</summary>
public interface IJournalService
{
    Task<JournalSearchResult> SearchJournalsAsync(JournalSearchInput input, CancellationToken cancellationToken = default);

    Task<JournalDto> GetJournalAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JournalDto> GetJournalByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Creates a Draft journal (B1). A supplied ReversalOfJournalId is recorded; the journal stays Draft.</summary>
    Task<JournalDto> CreateDraftAsync(CreateJournalInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Re-runs the §4 invariants against a draft and reports every failure.</summary>
    Task<JournalValidationResult> ValidateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>B1/B4: Draft → Posted. Requires the resolved period to be Open.</summary>
    Task<JournalDto> PostAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>B1: Draft → Cancelled.</summary>
    Task<JournalDto> CancelAsync(Guid id, CancelJournalInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// B4: creates a new compensating journal that inverts every line of the posted
    /// original, links it via ReversalOfJournalId, and posts it into an open period.
    /// The original journal is never modified.
    /// </summary>
    Task<JournalDto> ReverseAsync(Guid id, ReverseJournalInput input, string actorUserId, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Financial periods (B5, B6, B19)
// ---------------------------------------------------------------------------

public sealed record FinancialPeriodDto(
    Guid Id,
    string Reference,
    int Year,
    int Month,
    string Name,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    FinancialPeriodStatus Status,
    DateTimeOffset OpenedAtUtc,
    string OpenedByUserId,
    DateTimeOffset? ClosedAtUtc,
    string? ClosedByUserId,
    DateTimeOffset? ReopenedAtUtc,
    string? ReopenedByUserId,
    string? ReopenReason,
    int PostedJournalCount,
    decimal PostedDebitTotal,
    decimal PostedCreditTotal);

public sealed record OpenFinancialPeriodInput(int Year, int Month, string? Name);

public sealed record ReopenFinancialPeriodInput(string Reason);

public sealed record FinancialPeriodSearchInput(
    FinancialPeriodStatus? Status = null,
    int? Year = null,
    int Page = 1,
    int PageSize = 50);

public sealed record FinancialPeriodSearchResult(
    IReadOnlyList<FinancialPeriodDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Financial period lifecycle. Only Open and Closed exist (B5).</summary>
public interface IFinancialPeriodService
{
    Task<FinancialPeriodSearchResult> SearchPeriodsAsync(
        FinancialPeriodSearchInput input,
        CancellationToken cancellationToken = default);

    Task<FinancialPeriodDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves the period that owns a transaction date (B6: TransactionDate determines the period).</summary>
    Task<FinancialPeriodDto?> ResolveForTransactionDateAsync(
        DateTimeOffset transactionDate,
        CancellationToken cancellationToken = default);

    Task<FinancialPeriodDto> OpenAsync(
        OpenFinancialPeriodInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Closes a period after the required D6 checks.</summary>
    Task<FinancialPeriodDto> CloseAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>B5: Closed → Open, only for the most recently closed period, with a reason and audit.</summary>
    Task<FinancialPeriodDto> ReopenAsync(
        Guid id,
        ReopenFinancialPeriodInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Posting engine (B7, B18)
// ---------------------------------------------------------------------------

/// <summary>One amount component supplied to the posting engine (B7 selector source).</summary>
public sealed record FinancialPostingAmount(string? Selector, decimal Amount);

/// <summary>
/// Everything the posting engine needs to turn an operational event into a posted
/// journal. <see cref="SourceType"/> + <see cref="SourceId"/> are the duplicate
/// source identity enforced by Phase 8 §15.
/// </summary>
public sealed record FinancialPostingRequest(
    string RuleCode,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    DateTimeOffset TransactionDate,
    DateTimeOffset BusinessDate,
    string CurrencyCode,
    decimal? ExchangeRate,
    DateTimeOffset? RateDateUtc,
    string Description,
    IReadOnlyList<FinancialPostingAmount> Amounts,
    Guid? ReceivableId = null,
    Guid? PayableId = null,
    Guid? ReversalOfJournalId = null);

/// <summary>The accounting layer's entry point for every automated posting.</summary>
public interface IFinancePostingService
{
    /// <summary>
    /// Resolves the rule's configured mappings, builds and posts a balanced journal.
    /// Idempotent per (SourceType, SourceId): a replayed event returns the journal
    /// that already exists instead of double posting.
    /// </summary>
    Task<JournalDto> PostAsync(
        FinancialPostingRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// B4: builds and posts the compensating journal for a posted original. Used by
    /// the refund posting rule and by manual reversal.
    /// </summary>
    Task<JournalDto> PostReversalAsync(
        FinancialPostingRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Accounts receivable (B13)
// ---------------------------------------------------------------------------

public sealed record ReceivableAllocationDto(
    Guid Id,
    Guid PaymentId,
    string PaymentReference,
    decimal Amount,
    DateTimeOffset AllocatedAtUtc,
    string AllocatedByUserId);

public sealed record ReceivableDto(
    Guid Id,
    string Reference,
    Guid MemberId,
    string MemberNumber,
    string MemberName,
    Guid? MemberAccountId,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal OutstandingAmount,
    DateTimeOffset TransactionDate,
    DateTimeOffset? DueDate,
    string CurrencyCode,
    ReceivableStatus Status,
    Guid? JournalId,
    string? JournalReference,
    string? Description,
    string CancelledReason,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ReceivableAllocationDto> Allocations);

public sealed record CreateReceivableInput(
    Guid MemberId,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    decimal Amount,
    DateTimeOffset TransactionDate,
    DateTimeOffset? DueDate,
    string CurrencyCode,
    decimal? ExchangeRate,
    DateTimeOffset? RateDateUtc,
    string? Description,
    string? RuleCode = null,
    Guid? JournalId = null,
    IReadOnlyList<FinancialPostingAmount>? Amounts = null);

public sealed record ReceivableSearchInput(
    Guid? MemberId,
    ReceivableStatus? Status,
    DateTimeOffset? AsOf,
    int Page = 1,
    int PageSize = 50);

public sealed record ReceivableSearchResult(
    IReadOnlyList<ReceivableDto> Items,
    int TotalCount,
    decimal TotalOutstanding,
    int Page,
    int PageSize);

/// <summary>Accounts receivable. Member identity remains the existing Member/Member Account (B13).</summary>
public interface IReceivableService
{
    Task<ReceivableSearchResult> SearchAsync(ReceivableSearchInput input, CancellationToken cancellationToken = default);

    Task<ReceivableDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReceivableDto?> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the receivable and its linked journal in one transaction. Idempotent
    /// per (SourceType, SourceId).
    /// </summary>
    Task<ReceivableDto> CreateAsync(CreateReceivableInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// B19: a receivable is settled by allocation; cancelling writes a compensating
    /// journal, never a destructive delete.
    /// </summary>
    Task<ReceivableDto> CancelAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Accounts payable (B13: mirrors AR for suppliers)
// ---------------------------------------------------------------------------

public sealed record PayableAllocationDto(
    Guid Id,
    Guid PaymentId,
    string PaymentReference,
    decimal Amount,
    DateTimeOffset AllocatedAtUtc,
    string AllocatedByUserId);

public sealed record PayableDto(
    Guid Id,
    string Reference,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal OutstandingAmount,
    DateTimeOffset TransactionDate,
    DateTimeOffset? DueDate,
    string CurrencyCode,
    PayableStatus Status,
    Guid? JournalId,
    string? JournalReference,
    string? Description,
    string CancelledReason,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PayableAllocationDto> Allocations);

public sealed record CreatePayableInput(
    Guid SupplierId,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    decimal Amount,
    DateTimeOffset TransactionDate,
    DateTimeOffset? DueDate,
    string CurrencyCode,
    decimal? ExchangeRate,
    DateTimeOffset? RateDateUtc,
    string? Description,
    string? RuleCode = null,
    Guid? JournalId = null,
    IReadOnlyList<FinancialPostingAmount>? Amounts = null);

public sealed record PayableSearchInput(
    Guid? SupplierId,
    PayableStatus? Status,
    DateTimeOffset? AsOf,
    int Page = 1,
    int PageSize = 50);

public sealed record PayableSearchResult(
    IReadOnlyList<PayableDto> Items,
    int TotalCount,
    decimal TotalOutstanding,
    int Page,
    int PageSize);

/// <summary>Accounts payable. Supplier identity remains the existing Supplier (B13).</summary>
public interface IPayableService
{
    Task<PayableSearchResult> SearchAsync(PayableSearchInput input, CancellationToken cancellationToken = default);

    Task<PayableDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PayableDto?> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken = default);

    Task<PayableDto> CreateAsync(CreatePayableInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<PayableDto> CancelAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Payments (§10, B14)
// ---------------------------------------------------------------------------

public sealed record PaymentAllocationInput(
    Guid? ReceivableId,
    Guid? PayableId,
    decimal Amount);

public sealed record PaymentAllocationDto(
    Guid Id,
    Guid PaymentId,
    Guid? ReceivableId,
    Guid? PayableId,
    string? ReceivableReference,
    string? PayableReference,
    decimal Amount,
    DateTimeOffset CreatedAtUtc);

public sealed record PaymentDto(
    Guid Id,
    string Reference,
    PaymentDirection Direction,
    Guid? MemberId,
    string? MemberNumber,
    Guid? SupplierId,
    string? SupplierCode,
    Guid? MemberAccountId,
    Guid? AccountTransactionId,
    string? SourceType,
    Guid? SourceId,
    string? SourceReference,
    decimal Amount,
    decimal UnappliedAmount,
    string CurrencyCode,
    DateTimeOffset TransactionDate,
    DateTimeOffset? ValueDate,
    string? PaymentMethodCode,
    string? ExternalReference,
    Guid? JournalId,
    string? JournalReference,
    string? Description,
    string? IdempotencyKey,
    string CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PaymentAllocationDto> Allocations);

public sealed record RecordPaymentInput(
    PaymentDirection Direction,
    Guid? MemberId,
    Guid? SupplierId,
    Guid? MemberAccountId,
    Guid? AccountTransactionId,
    string? SourceType,
    Guid? SourceId,
    string? SourceReference,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset TransactionDate,
    DateTimeOffset? ValueDate,
    string? PaymentMethodCode,
    string? ExternalReference,
    string? Description,
    IReadOnlyList<PaymentAllocationInput>? Allocations = null,
    string? RuleCode = null,
    string? IdempotencyKey = null);

public sealed record PaymentSearchInput(
    PaymentDirection? Direction,
    Guid? MemberId,
    Guid? SupplierId,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record PaymentSearchResult(
    IReadOnlyList<PaymentDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Finance payment: the accounting representation and allocation mechanism (§10).</summary>
public interface IFinancePaymentService
{
    Task<PaymentSearchResult> SearchAsync(PaymentSearchInput input, CancellationToken cancellationToken = default);

    Task<PaymentDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaymentDto?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a payment, allocates it to receivables/payables, updates the document
    /// statuses and posts the cash/bank journal — all in one transaction. Idempotent
    /// on the supplied key and on (SourceType, SourceId).
    /// </summary>
    Task<PaymentDto> RecordAsync(RecordPaymentInput input, string actorUserId, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Manual reconciliation (B15)
// ---------------------------------------------------------------------------

public sealed record ReconciliationMatchDto(
    Guid Id,
    Guid JournalId,
    string JournalReference,
    DateTimeOffset TransactionDate,
    decimal LedgerAmount,
    string? Note,
    DateTimeOffset MatchedAtUtc,
    string MatchedByUserId);

public sealed record FinancialReconciliationDto(
    Guid Id,
    string Reference,
    Guid FinancialAccountId,
    string FinancialAccountCode,
    string FinancialAccountName,
    DateTimeOffset StatementDateUtc,
    decimal StatementClosingBalance,
    decimal? LedgerClosingBalance,
    decimal? Variance,
    FinancialReconciliationStatus Status,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? CompletedByUserId,
    string? CancelledReason,
    IReadOnlyList<ReconciliationMatchDto> Matches,
    decimal MatchedLedgerTotal);

public sealed record CreateReconciliationInput(
    Guid FinancialAccountId,
    DateTimeOffset StatementDateUtc,
    decimal StatementClosingBalance,
    string? Note);

public sealed record MatchReconciliationLineInput(Guid JournalId, decimal LedgerAmount, string? Note);

public sealed record CompleteReconciliationInput(string? Note);

public sealed record FinancialReconciliationSearchInput(
    Guid? FinancialAccountId,
    FinancialReconciliationStatus? Status,
    int Page = 1,
    int PageSize = 50);

public sealed record FinancialReconciliationSearchResult(
    IReadOnlyList<FinancialReconciliationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Manual reconciliation only. No bank API, no bank feed (B15).</summary>
public interface IFinancialReconciliationService
{
    Task<FinancialReconciliationSearchResult> SearchAsync(
        FinancialReconciliationSearchInput input,
        CancellationToken cancellationToken = default);

    Task<FinancialReconciliationDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FinancialReconciliationDto> CreateAsync(
        CreateReconciliationInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialReconciliationDto> AddMatchAsync(
        Guid id,
        MatchReconciliationLineInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialReconciliationDto> RemoveMatchAsync(
        Guid id,
        Guid matchId,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialReconciliationDto> CompleteAsync(
        Guid id,
        CompleteReconciliationInput input,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialReconciliationDto> CancelAsync(
        Guid id,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Reports (B16) and overview
// ---------------------------------------------------------------------------

public sealed record TrialBalanceRowDto(
    string AccountCode,
    string AccountName,
    FinancialAccountType AccountType,
    NormalBalance NormalBalance,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public sealed record TrialBalanceReportDto(
    string CurrencyCode,
    DateTimeOffset? AsOf,
    IReadOnlyList<TrialBalanceRowDto> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference);

public sealed record GeneralLedgerRowDto(
    DateTimeOffset TransactionDate,
    string JournalReference,
    string JournalDescription,
    string AccountCode,
    string AccountName,
    JournalDirection Direction,
    decimal Amount,
    string? LineDescription,
    string? SourceReference);

public sealed record GeneralLedgerReportDto(
    Guid FinancialAccountId,
    string AccountCode,
    string AccountName,
    string CurrencyCode,
    DateTimeOffset? From,
    DateTimeOffset? To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<GeneralLedgerRowDto> Entries);

public sealed record AgingBucketDto(string Label, int DaysFrom, int? DaysTo, decimal OutstandingAmount, int DocumentCount);

public sealed record AgingDocumentDto(
    Guid DocumentId,
    string Reference,
    string CounterpartyCode,
    string CounterpartyName,
    DateTimeOffset TransactionDate,
    DateTimeOffset? DueDate,
    int DaysOutstanding,
    string BucketLabel,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal OutstandingAmount,
    string CurrencyCode);

public sealed record AgingReportDto(
    bool IsReceivable,
    string CurrencyCode,
    DateTimeOffset AsOf,
    IReadOnlyList<AgingBucketDto> Buckets,
    IReadOnlyList<AgingDocumentDto> Documents,
    decimal TotalOutstanding);

public sealed record CashPositionRowDto(
    string AccountCode,
    string AccountName,
    FinancialCashKind CashKind,
    decimal DebitBalance,
    decimal CreditBalance,
    decimal NetBalance,
    string CurrencyCode);

public sealed record CashPositionReportDto(
    string BaseCurrencyCode,
    DateTimeOffset AsOf,
    IReadOnlyList<CashPositionRowDto> Rows,
    decimal TotalCash,
    decimal TotalBank);

public sealed record FinancialPeriodsReportRowDto(
    string Reference,
    int Year,
    int Month,
    string Name,
    FinancialPeriodStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    decimal PostedDebitTotal,
    decimal PostedCreditTotal,
    int PostedJournalCount);

public sealed record FinancialPeriodsReportDto(
    IReadOnlyList<FinancialPeriodsReportRowDto> Rows,
    FinancialPeriodStatus? CurrentStatus,
    FinancialPeriodDto? CurrentPeriod);

public sealed record FinanceOverviewDto(
    string BaseCurrencyCode,
    DateTimeOffset AsOf,
    FinancialPeriodDto? CurrentPeriod,
    decimal OpenReceivables,
    decimal OpenPayables,
    decimal CashAndBank,
    decimal TrialBalanceDebits,
    decimal TrialBalanceCredits,
    decimal TrialBalanceDifference,
    int DraftJournalCount,
    int UnreconciledCashAccountCount);

/// <summary>The six mandated reports plus the finance overview (B16).</summary>
public interface IFinanceReportService
{
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(
        Guid financialAccountId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<AgingReportDto> GetReceivableAgingAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task<AgingReportDto> GetPayableAgingAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task<CashPositionReportDto> GetCashPositionAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task<FinancialPeriodsReportDto> GetFinancialPeriodsAsync(CancellationToken cancellationToken = default);

    Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
