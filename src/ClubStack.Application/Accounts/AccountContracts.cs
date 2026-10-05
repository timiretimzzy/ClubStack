namespace ClubStack.Application.Accounts;

public sealed record AccountBalanceDto(
    string CurrencyCode,
    decimal AmountDue,
    decimal CreditsBalance,
    decimal UnallocatedPayments,
    decimal OverdueBalance,
    DateTimeOffset AsOfDateUtc);

public sealed record MemberAccountDto(
    Guid Id,
    Guid MemberId,
    string MemberNumber,
    string MemberName,
    string AccountNumber,
    string Status,
    string CurrencyCode,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    AccountBalanceDto Balance,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AccountTransactionDto(
    Guid Id,
    Guid AccountId,
    string TransactionType,
    string Status,
    string CurrencyCode,
    decimal Amount,
    decimal SignedAmount,
    bool IsDebit,
    string Description,
    string Reference,
    string? ExternalReference,
    string? Reason,
    string? SourceType,
    Guid? SourceId,
    Guid? ReverseOfTransactionId,
    Guid? PaymentMethodId,
    string? PaymentMethodCode,
    string? PaymentState,
    DateTimeOffset TransactionDateUtc,
    DateTimeOffset BusinessDateUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record AccountTransactionSearchInput(Guid AccountId, int Page = 1, int PageSize = 50);

public sealed record AccountTransactionSearchResult(IReadOnlyList<AccountTransactionDto> Items, int TotalCount, int Page, int PageSize);

public sealed record ChargeInput(
    string Description,
    decimal Amount,
    string CurrencyCode,
    string? Reference,
    string? SourceType,
    Guid? SourceId,
    DateTimeOffset? BusinessDateUtc);

public sealed record PaymentAllocationInput(Guid ChargeTransactionId, decimal Amount);

public sealed record PaymentInput(
    string Description,
    decimal Amount,
    string CurrencyCode,
    string PaymentMethodCode,
    string? ExternalReference,
    DateTimeOffset? BusinessDateUtc,
    IReadOnlyList<PaymentAllocationInput>? Allocations);

public sealed record CreditInput(
    string Description,
    decimal Amount,
    string CurrencyCode,
    string? ExternalReference,
    DateTimeOffset? BusinessDateUtc,
    IReadOnlyList<PaymentAllocationInput>? Allocations,
    string? Reference = null);

public sealed record AdjustmentInput(string Description, decimal Amount, string Reason, DateTimeOffset? BusinessDateUtc);

public sealed record ReversalInput(string Reason);

public sealed record StatementPeriodInput(DateTimeOffset? FromDateUtc = null, DateTimeOffset? ToDateUtc = null);

public sealed record StatementDto(
    Guid AccountId,
    string AccountNumber,
    string MemberNumber,
    string MemberName,
    string CurrencyCode,
    string StatementNumber,
    DateTimeOffset FromDateUtc,
    DateTimeOffset ToDateUtc,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal CurrentBalance,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedByUserId,
    IReadOnlyList<AccountTransactionDto> Transactions);

public sealed record AgingBucketDto(string Label, int DaysFrom, int? DaysTo, decimal OutstandingAmount);

public sealed record AgingItemDto(Guid ChargeTransactionId, string Description, DateTimeOffset BusinessDateUtc, int DaysOutstanding, decimal OutstandingAmount);

public sealed record AgingDto(
    DateTimeOffset AsOfDateUtc,
    string CurrencyCode,
    int DaysOutstandingThreshold,
    decimal TotalOutstanding,
    decimal OpenItemCount,
    IReadOnlyList<AgingBucketDto> Buckets,
    IReadOnlyList<AgingItemDto> Items);

/// <summary>
/// Member account ledger (Phase 3 §17-20, Phase 2 ACC-001..014, Phase 5 §33/§34).
/// Balances are always derived from posted transactions; corrections use
/// adjustments and reversals and are audited.
/// </summary>
public interface IMemberAccountService
{
    /// <summary>Resolves (creating on first use, ACC-001) the account for a member.</summary>
    Task<MemberAccountDto> GetAccountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task<MemberAccountDto> GetAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<AccountBalanceDto> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<AccountTransactionSearchResult> SearchTransactionsAsync(AccountTransactionSearchInput input, CancellationToken cancellationToken = default);

    Task<AccountTransactionDto> ChargeAsync(Guid accountId, ChargeInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<AccountTransactionDto> RecordPaymentAsync(Guid accountId, PaymentInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<AccountTransactionDto> CreateCreditAsync(Guid accountId, CreditInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<AccountTransactionDto> AdjustAsync(Guid accountId, AdjustmentInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<AccountTransactionDto> ReverseAsync(Guid accountId, Guid transactionId, ReversalInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<StatementDto> GenerateStatementAsync(Guid accountId, StatementPeriodInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<AgingDto> GetAgingAsync(Guid accountId, CancellationToken cancellationToken = default);
}