using System.ComponentModel.DataAnnotations;
using ClubStack.Application.Finance;
using ClubStack.Domain.Finance;

namespace ClubStack.Api.Contracts;

// ---------------------------------------------------------------------------
// D6 Finance Integration (PM Decision Pack B1-B19). Requests and responses
// only: every accounting decision lives in the application services.
// ---------------------------------------------------------------------------

// ---- Chart of accounts (B2, B3, B7, B17) ----

public sealed record FinancialAccountSearchRequest
{
    [MaxLength(64)]
    public string? Code { get; init; }

    [MaxLength(256)]
    public string? Name { get; init; }

    public FinancialAccountType? AccountType { get; init; }

    public FinancialAccountStatus? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record FinancialAccountResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public FinancialAccountType AccountType { get; init; }
    public NormalBalance NormalBalance { get; init; }
    public FinancialAccountStatus Status { get; init; }
    public Guid? ParentAccountId { get; init; }
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public FinancialCashKind CashKind { get; init; }
    public bool IsPostingAllowed { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record FinancialAccountSearchResponse
{
    public List<FinancialAccountResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateFinancialAccountRequest
{
    [Required, MaxLength(64)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(256)]
    public string Name { get; init; } = string.Empty;

    public FinancialAccountType AccountType { get; init; }

    public Guid? ParentAccountId { get; init; }

    [MaxLength(1024)]
    public string? Description { get; init; }

    public bool IsPostingAllowed { get; init; } = true;

    public FinancialCashKind CashKind { get; init; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed record UpdateFinancialAccountRequest
{
    [Required, MaxLength(256)]
    public string Name { get; init; } = string.Empty;

    public Guid? ParentAccountId { get; init; }

    [MaxLength(1024)]
    public string? Description { get; init; }

    public bool IsPostingAllowed { get; init; } = true;

    public FinancialCashKind CashKind { get; init; }
}

public sealed record SetBaseCurrencyRequest
{
    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed record FinancialPostingRuleMappingResponse
{
    public Guid Id { get; init; }
    public Guid RuleId { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public JournalDirection Direction { get; init; }
    public Guid FinancialAccountId { get; init; }
    public string FinancialAccountCode { get; init; } = string.Empty;
    public string FinancialAccountName { get; init; } = string.Empty;
    public string? Selector { get; init; }
}

public sealed record FinancialPostingRuleResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SourceSection { get; init; } = string.Empty;
    public FinancialPostingStrategy Strategy { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<FinancialPostingRuleMappingResponse> Mappings { get; init; } = [];
}

public sealed record UpsertPostingRuleMappingRequest
{
    public int Sequence { get; init; }

    public JournalDirection Direction { get; init; }

    public Guid FinancialAccountId { get; init; }

    [MaxLength(64)]
    public string? Selector { get; init; }
}

// ---- Financial periods (B5, B6, B19) ----

public sealed record FinancialPeriodSearchRequest
{
    public FinancialPeriodStatus? Status { get; init; }

    public int? Year { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record FinancialPeriodResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
    public FinancialPeriodStatus Status { get; init; }
    public DateTimeOffset OpenedAtUtc { get; init; }
    public string OpenedByUserId { get; init; } = string.Empty;
    public DateTimeOffset? ClosedAtUtc { get; init; }
    public string? ClosedByUserId { get; init; }
    public DateTimeOffset? ReopenedAtUtc { get; init; }
    public string? ReopenedByUserId { get; init; }
    public string? ReopenReason { get; init; }
    public int PostedJournalCount { get; init; }
    public decimal PostedDebitTotal { get; init; }
    public decimal PostedCreditTotal { get; init; }
}

public sealed record FinancialPeriodSearchResponse
{
    public List<FinancialPeriodResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record OpenFinancialPeriodRequest
{
    [Range(2000, 9999)]
    public int Year { get; init; }

    [Range(1, 12)]
    public int Month { get; init; }

    [MaxLength(128)]
    public string? Name { get; init; }
}

public sealed record ReopenFinancialPeriodRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

// ---- Journals (B1, B4, B8, B9, B10, B12, B19) ----

public sealed record JournalLineResponse
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid FinancialAccountId { get; init; }
    public string FinancialAccountCode { get; init; } = string.Empty;
    public string FinancialAccountName { get; init; } = string.Empty;
    public JournalDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseAmount { get; init; }
    public string? Description { get; init; }
    public Guid? SourceLineId { get; init; }
}

public sealed record JournalResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public JournalStatus Status { get; init; }
    public string Description { get; init; } = string.Empty;
    public Guid FinancialPeriodId { get; init; }
    public string FinancialPeriodReference { get; init; } = string.Empty;
    public DateTimeOffset TransactionDate { get; init; }
    public DateTimeOffset BusinessDate { get; init; }
    public string TransactionCurrencyCode { get; init; } = string.Empty;
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; }
    public DateTimeOffset RateDateUtc { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public string? SourceReference { get; init; }
    public Guid? ReversalOfJournalId { get; init; }
    public string? ReversalOfJournalReference { get; init; }
    public Guid? ReversedByJournalId { get; init; }
    public Guid? ReceivableId { get; init; }
    public Guid? PayableId { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public DateTimeOffset? PostedAtUtc { get; init; }
    public string? PostedByUserId { get; init; }
    public string? CancelledReason { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public List<JournalLineResponse> Lines { get; init; } = [];
}

public sealed record JournalSearchRequest
{
    public JournalStatus? Status { get; init; }

    public Guid? FinancialPeriodId { get; init; }

    public Guid? FinancialAccountId { get; init; }

    [MaxLength(64)]
    public string? SourceType { get; init; }

    public DateTimeOffset? FromDate { get; init; }

    public DateTimeOffset? ToDate { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record JournalSearchResponse
{
    public List<JournalResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateJournalLineRequest
{
    public Guid FinancialAccountId { get; init; }

    public JournalDirection Direction { get; init; }

    [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
    public decimal Amount { get; init; }

    [MaxLength(512)]
    public string? Description { get; init; }
}

public sealed record CreateJournalRequest
{
    [Required, MaxLength(512)]
    public string Description { get; init; } = string.Empty;

    public DateTimeOffset TransactionDate { get; init; }

    public DateTimeOffset? BusinessDate { get; init; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "1000000")]
    public decimal? ExchangeRate { get; init; }

    public DateTimeOffset? RateDateUtc { get; init; }

    public Guid? FinancialPeriodId { get; init; }

    [MinLength(2)]
    public List<CreateJournalLineRequest> Lines { get; init; } = [];

    [MaxLength(128)]
    public string? SourceReference { get; init; }
}

public sealed record JournalValidationResponse
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = [];
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public int LineCount { get; init; }
}

public sealed record CancelJournalRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ReverseJournalRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset? TransactionDate { get; init; }
}

// ---- Accounts receivable (B13) ----

public sealed record ReceivableAllocationResponse
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTimeOffset AllocatedAtUtc { get; init; }
    public string AllocatedByUserId { get; init; } = string.Empty;
}

public sealed record ReceivableResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public Guid MemberId { get; init; }
    public string MemberNumber { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public Guid? MemberAccountId { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public string? SourceReference { get; init; }
    public decimal OriginalAmount { get; init; }
    public decimal AllocatedAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public DateTimeOffset TransactionDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public ReceivableStatus Status { get; init; }
    public Guid? JournalId { get; init; }
    public string? JournalReference { get; init; }
    public string? Description { get; init; }
    public string CancelledReason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public List<ReceivableAllocationResponse> Allocations { get; init; } = [];
}

public sealed record ReceivableSearchRequest
{
    public Guid? MemberId { get; init; }

    public ReceivableStatus? Status { get; init; }

    public DateTimeOffset? AsOf { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record ReceivableSearchResponse
{
    public List<ReceivableResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public decimal TotalOutstanding { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateReceivableRequest
{
    public Guid MemberId { get; init; }

    [Required, MaxLength(64)]
    public string SourceType { get; init; } = string.Empty;

    public Guid SourceId { get; init; }

    [MaxLength(128)]
    public string? SourceReference { get; init; }

    [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
    public decimal Amount { get; init; }

    public DateTimeOffset TransactionDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "1000000")]
    public decimal? ExchangeRate { get; init; }

    public DateTimeOffset? RateDateUtc { get; init; }

    [MaxLength(512)]
    public string? Description { get; init; }
}

public sealed record CancelDocumentRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

// ---- Accounts payable (B13) ----

public sealed record PayableAllocationResponse
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTimeOffset AllocatedAtUtc { get; init; }
    public string AllocatedByUserId { get; init; } = string.Empty;
}

public sealed record PayableResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public string? SourceReference { get; init; }
    public decimal OriginalAmount { get; init; }
    public decimal AllocatedAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public DateTimeOffset TransactionDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public PayableStatus Status { get; init; }
    public Guid? JournalId { get; init; }
    public string? JournalReference { get; init; }
    public string? Description { get; init; }
    public string CancelledReason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public List<PayableAllocationResponse> Allocations { get; init; } = [];
}

public sealed record PayableSearchRequest
{
    public Guid? SupplierId { get; init; }

    public PayableStatus? Status { get; init; }

    public DateTimeOffset? AsOf { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record PayableSearchResponse
{
    public List<PayableResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public decimal TotalOutstanding { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreatePayableRequest
{
    public Guid SupplierId { get; init; }

    [Required, MaxLength(64)]
    public string SourceType { get; init; } = string.Empty;

    public Guid SourceId { get; init; }

    [MaxLength(128)]
    public string? SourceReference { get; init; }

    [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
    public decimal Amount { get; init; }

    public DateTimeOffset TransactionDate { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "1000000")]
    public decimal? ExchangeRate { get; init; }

    public DateTimeOffset? RateDateUtc { get; init; }

    [MaxLength(512)]
    public string? Description { get; init; }
}

// ---- Payments (B14) ----

public sealed record FinancePaymentAllocationRequest
{
    public Guid? ReceivableId { get; init; }

    public Guid? PayableId { get; init; }

    [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
    public decimal Amount { get; init; }
}

public sealed record PaymentAllocationResponse
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public Guid? ReceivableId { get; init; }
    public Guid? PayableId { get; init; }
    public string? ReceivableReference { get; init; }
    public string? PayableReference { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record PaymentResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public PaymentDirection Direction { get; init; }
    public Guid? MemberId { get; init; }
    public string? MemberNumber { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierCode { get; init; }
    public Guid? MemberAccountId { get; init; }
    public Guid? AccountTransactionId { get; init; }
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceReference { get; init; }
    public decimal Amount { get; init; }
    public decimal UnappliedAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset TransactionDate { get; init; }
    public DateTimeOffset? ValueDate { get; init; }
    public string? PaymentMethodCode { get; init; }
    public string? ExternalReference { get; init; }
    public Guid? JournalId { get; init; }
    public string? JournalReference { get; init; }
    public string? Description { get; init; }
    public string? IdempotencyKey { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public List<PaymentAllocationResponse> Allocations { get; init; } = [];
}

public sealed record PaymentSearchRequest
{
    public PaymentDirection? Direction { get; init; }

    public Guid? MemberId { get; init; }

    public Guid? SupplierId { get; init; }

    public DateTimeOffset? FromDate { get; init; }

    public DateTimeOffset? ToDate { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record PaymentSearchResponse
{
    public List<PaymentResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record RecordPaymentRequest
{
    public PaymentDirection Direction { get; init; }

    public Guid? MemberId { get; init; }

    public Guid? SupplierId { get; init; }

    public Guid? MemberAccountId { get; init; }

    public Guid? AccountTransactionId { get; init; }

    [MaxLength(64)]
    public string? SourceType { get; init; }

    public Guid? SourceId { get; init; }

    [MaxLength(128)]
    public string? SourceReference { get; init; }

    [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
    public decimal Amount { get; init; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = string.Empty;

    public DateTimeOffset TransactionDate { get; init; }

    public DateTimeOffset? ValueDate { get; init; }

    [MaxLength(32)]
    public string? PaymentMethodCode { get; init; }

    [MaxLength(128)]
    public string? ExternalReference { get; init; }

    [MaxLength(512)]
    public string? Description { get; init; }

    public List<FinancePaymentAllocationRequest>? Allocations { get; init; }

    [MaxLength(128)]
    public string? IdempotencyKey { get; init; }
}

// ---- Manual reconciliation (B15) ----

public sealed record ReconciliationMatchResponse
{
    public Guid Id { get; init; }
    public Guid JournalId { get; init; }
    public string JournalReference { get; init; } = string.Empty;
    public DateTimeOffset TransactionDate { get; init; }
    public decimal LedgerAmount { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset MatchedAtUtc { get; init; }
    public string MatchedByUserId { get; init; } = string.Empty;
}

public sealed record ReconciliationResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public Guid FinancialAccountId { get; init; }
    public string FinancialAccountCode { get; init; } = string.Empty;
    public string FinancialAccountName { get; init; } = string.Empty;
    public DateTimeOffset StatementDateUtc { get; init; }
    public decimal StatementClosingBalance { get; init; }
    public decimal? LedgerClosingBalance { get; init; }
    public decimal? Variance { get; init; }
    public FinancialReconciliationStatus Status { get; init; }
    public string? Note { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string? CompletedByUserId { get; init; }
    public string? CancelledReason { get; init; }
    public List<ReconciliationMatchResponse> Matches { get; init; } = [];
    public decimal MatchedLedgerTotal { get; init; }
}

public sealed record ReconciliationSearchRequest
{
    public Guid? FinancialAccountId { get; init; }

    public FinancialReconciliationStatus? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record ReconciliationSearchResponse
{
    public List<ReconciliationResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CreateReconciliationRequest
{
    public Guid FinancialAccountId { get; init; }

    public DateTimeOffset StatementDateUtc { get; init; }

    public decimal StatementClosingBalance { get; init; }

    [MaxLength(1024)]
    public string? Note { get; init; }
}

public sealed record MatchReconciliationLineRequest
{
    public Guid JournalId { get; init; }

    public decimal LedgerAmount { get; init; }

    [MaxLength(512)]
    public string? Note { get; init; }
}

public sealed record CompleteReconciliationRequest
{
    [MaxLength(1024)]
    public string? Note { get; init; }
}

// ---- Reports (B16) and overview ----

public sealed record TrialBalanceRowResponse
{
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public FinancialAccountType AccountType { get; init; }
    public NormalBalance NormalBalance { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal Balance { get; init; }
}

public sealed record TrialBalanceResponse
{
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset? AsOf { get; init; }
    public List<TrialBalanceRowResponse> Rows { get; init; } = [];
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public decimal Difference { get; init; }
}

public sealed record GeneralLedgerRowResponse
{
    public DateTimeOffset TransactionDate { get; init; }
    public string JournalReference { get; init; } = string.Empty;
    public string JournalDescription { get; init; } = string.Empty;
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public JournalDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public string? LineDescription { get; init; }
    public string? SourceReference { get; init; }
}

public sealed record GeneralLedgerResponse
{
    public Guid FinancialAccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public List<GeneralLedgerRowResponse> Entries { get; init; } = [];
}

public sealed record FinanceAgingBucketResponse
{
    public string Label { get; init; } = string.Empty;
    public int DaysFrom { get; init; }
    public int? DaysTo { get; init; }
    public decimal OutstandingAmount { get; init; }
    public int DocumentCount { get; init; }
}

public sealed record FinanceAgingDocumentResponse
{
    public Guid DocumentId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string CounterpartyCode { get; init; } = string.Empty;
    public string CounterpartyName { get; init; } = string.Empty;
    public DateTimeOffset TransactionDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public int DaysOutstanding { get; init; }
    public string BucketLabel { get; init; } = string.Empty;
    public decimal OriginalAmount { get; init; }
    public decimal AllocatedAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed record FinanceAgingResponse
{
    public bool IsReceivable { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset AsOf { get; init; }
    public List<FinanceAgingBucketResponse> Buckets { get; init; } = [];
    public List<FinanceAgingDocumentResponse> Documents { get; init; } = [];
    public decimal TotalOutstanding { get; init; }
}

public sealed record CashPositionRowResponse
{
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public FinancialCashKind CashKind { get; init; }
    public decimal DebitBalance { get; init; }
    public decimal CreditBalance { get; init; }
    public decimal NetBalance { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed record CashPositionResponse
{
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset AsOf { get; init; }
    public List<CashPositionRowResponse> Rows { get; init; } = [];
    public decimal TotalCash { get; init; }
    public decimal TotalBank { get; init; }
}

public sealed record FinancialPeriodsReportRowResponse
{
    public string Reference { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public string Name { get; init; } = string.Empty;
    public FinancialPeriodStatus Status { get; init; }
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
    public decimal PostedDebitTotal { get; init; }
    public decimal PostedCreditTotal { get; init; }
    public int PostedJournalCount { get; init; }
}

public sealed record FinancialPeriodsReportResponse
{
    public List<FinancialPeriodsReportRowResponse> Rows { get; init; } = [];
    public FinancialPeriodStatus? CurrentStatus { get; init; }
    public FinancialPeriodResponse? CurrentPeriod { get; init; }
}

public sealed record FinanceOverviewResponse
{
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset AsOf { get; init; }
    public FinancialPeriodResponse? CurrentPeriod { get; init; }
    public decimal OpenReceivables { get; init; }
    public decimal OpenPayables { get; init; }
    public decimal CashAndBank { get; init; }
    public decimal TrialBalanceDebits { get; init; }
    public decimal TrialBalanceCredits { get; init; }
    public decimal TrialBalanceDifference { get; init; }
    public int DraftJournalCount { get; init; }
    public int UnreconciledCashAccountCount { get; init; }
}
