using System.ComponentModel.DataAnnotations;

namespace ClubStack.Api.Contracts;

// ---- Credentials (Phase 2 CRED-001..010, Phase 8 §7.4) ----

public sealed record CredentialSearchRequest
{
    [MaxLength(64)]
    public string? Query { get; init; }

    public Guid? MemberId { get; init; }

    [MaxLength(16)]
    public string? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record CredentialSearchResponse
{
    public IReadOnlyList<CredentialResponse> Items { get; init; } = Array.Empty<CredentialResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record CredentialResponse
{
    public Guid Id { get; init; }
    public string CredentialNumber { get; init; } = string.Empty;
    public Guid? MemberId { get; init; }
    public string? MemberNumber { get; init; }
    public string? MemberName { get; init; }
    public string CredentialType { get; init; } = string.Empty;
    public string CredentialValue { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? ActivatedAtUtc { get; init; }
    public DateTimeOffset? SuspendedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public Guid? ReplacedByCredentialId { get; init; }
    public string? ReplacedByCredentialNumber { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record CredentialHistoryResponse
{
    public Guid Id { get; init; }
    public Guid CredentialId { get; init; }
    public string PreviousState { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public string ChangedByUserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; init; }
}

public sealed record ResolveCredentialResponse
{
    public Guid CredentialId { get; init; }
    public string CredentialNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid MemberId { get; init; }
    public string MemberNumber { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public string MemberStatus { get; init; } = string.Empty;
}

public sealed record ResolveCredentialRequest
{
    [Required, MaxLength(64)]
    public string CredentialValue { get; init; } = string.Empty;
}

public sealed record IssueCredentialRequest
{
    [Required, MaxLength(16)]
    public string CredentialType { get; init; } = string.Empty;

    [Required, MaxLength(64)]
    public string CredentialValue { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed record ActivateCredentialRequest
{
    public required Guid MemberId { get; init; }
}

public sealed record CredentialTransitionRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ReplaceCredentialRequest
{
    [Required, MaxLength(16)]
    public string CredentialType { get; init; } = string.Empty;

    [Required, MaxLength(64)]
    public string CredentialValue { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

// ---- Member accounts (Phase 2 ACC-001..014, Phase 3 §17-20) ----

public sealed record AccountBalanceResponse
{
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal AmountDue { get; init; }
    public decimal CreditsBalance { get; init; }
    public decimal UnallocatedPayments { get; init; }
    public decimal OverdueBalance { get; init; }
    public DateTimeOffset AsOfDateUtc { get; init; }
}

public sealed record MemberAccountResponse
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberNumber { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset OpenedAtUtc { get; init; }
    public DateTimeOffset? ClosedAtUtc { get; init; }
    public AccountBalanceResponse Balance { get; init; } = new();
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record AccountTransactionResponse
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal SignedAmount { get; init; }
    public bool IsDebit { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string? Reason { get; init; }
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public Guid? ReverseOfTransactionId { get; init; }
    public Guid? PaymentMethodId { get; init; }
    public string? PaymentMethodCode { get; init; }
    public string? PaymentState { get; init; }
    public DateTimeOffset TransactionDateUtc { get; init; }
    public DateTimeOffset BusinessDateUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record AccountTransactionSearchResponse
{
    public IReadOnlyList<AccountTransactionResponse> Items { get; init; } = Array.Empty<AccountTransactionResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record ChargeRequest
{
    [Required, MaxLength(256)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 9999999999999, ErrorMessage = "The amount must be positive.")]
    public decimal Amount { get; init; }

    [MaxLength(8)]
    public string? CurrencyCode { get; init; }

    [MaxLength(32)]
    public string? Reference { get; init; }

    [MaxLength(64)]
    public string? SourceType { get; init; }

    public Guid? SourceId { get; init; }

    public DateTimeOffset? BusinessDateUtc { get; init; }
}

public sealed record PaymentAllocationRequest
{
    public required Guid ChargeTransactionId { get; init; }

    [Range(0.01, 9999999999999, ErrorMessage = "The allocation amount must be positive.")]
    public decimal Amount { get; init; }
}

public sealed record PaymentRequest
{
    [Required, MaxLength(256)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 9999999999999, ErrorMessage = "The amount must be positive.")]
    public decimal Amount { get; init; }

    [MaxLength(8)]
    public string? CurrencyCode { get; init; }

    [Required, MaxLength(32)]
    public string PaymentMethodCode { get; init; } = string.Empty;

    [MaxLength(128)]
    public string? ExternalReference { get; init; }

    public DateTimeOffset? BusinessDateUtc { get; init; }

    public IReadOnlyList<PaymentAllocationRequest>? Allocations { get; init; }
}

public sealed record CreditRequest
{
    [Required, MaxLength(256)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 9999999999999, ErrorMessage = "The amount must be positive.")]
    public decimal Amount { get; init; }

    [MaxLength(8)]
    public string? CurrencyCode { get; init; }

    [MaxLength(128)]
    public string? ExternalReference { get; init; }

    public DateTimeOffset? BusinessDateUtc { get; init; }

    public IReadOnlyList<PaymentAllocationRequest>? Allocations { get; init; }
}

public sealed record AdjustmentRequest
{
    [Required, MaxLength(256)]
    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset? BusinessDateUtc { get; init; }
}

public sealed record ReversalRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record StatementRequest
{
    public DateTimeOffset? FromDateUtc { get; init; }

    public DateTimeOffset? ToDateUtc { get; init; }
}

public sealed record StatementResponse
{
    public Guid AccountId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string MemberNumber { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public string StatementNumber { get; init; } = string.Empty;
    public DateTimeOffset FromDateUtc { get; init; }
    public DateTimeOffset ToDateUtc { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public decimal CurrentBalance { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public string GeneratedByUserId { get; init; } = string.Empty;
    public IReadOnlyList<AccountTransactionResponse> Transactions { get; init; } = Array.Empty<AccountTransactionResponse>();
}

public sealed record AgingBucketResponse
{
    public string Label { get; init; } = string.Empty;
    public int DaysFrom { get; init; }
    public int? DaysTo { get; init; }
    public decimal OutstandingAmount { get; init; }
}

public sealed record AgingItemResponse
{
    public Guid ChargeTransactionId { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset BusinessDateUtc { get; init; }
    public int DaysOutstanding { get; init; }
    public decimal OutstandingAmount { get; init; }
}

public sealed record AgingResponse
{
    public DateTimeOffset AsOfDateUtc { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public int DaysOutstandingThreshold { get; init; }
    public decimal TotalOutstanding { get; init; }
    public decimal OpenItemCount { get; init; }
    public IReadOnlyList<AgingBucketResponse> Buckets { get; init; } = Array.Empty<AgingBucketResponse>();
    public IReadOnlyList<AgingItemResponse> Items { get; init; } = Array.Empty<AgingItemResponse>();
}