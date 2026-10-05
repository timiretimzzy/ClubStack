namespace ClubStack.Application.Credentials;

public sealed record CredentialDto(
    Guid Id,
    string CredentialNumber,
    Guid? MemberId,
    string? MemberNumber,
    string? MemberName,
    string CredentialType,
    string CredentialValue,
    string Status,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    Guid? ReplacedByCredentialId,
    string? ReplacedByCredentialNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CredentialHistoryDto(
    Guid Id,
    Guid CredentialId,
    string PreviousState,
    string NewState,
    string ChangedByUserId,
    string Reason,
    DateTimeOffset ChangedAtUtc);

public sealed record ResolveCredentialResult(
    Guid CredentialId,
    string CredentialNumber,
    string Status,
    Guid MemberId,
    string MemberNumber,
    string MemberName,
    string MemberStatus);

public sealed record IssueCredentialInput(string CredentialType, string CredentialValue, DateTimeOffset? ExpiresAtUtc);

public sealed record ActivateCredentialInput(Guid MemberId);

public sealed record SuspendCredentialInput(string Reason);

public sealed record LostCredentialInput(string Reason);

public sealed record StolenCredentialInput(string Reason);

public sealed record ReactivateCredentialInput(string Reason);

public sealed record ReplaceCredentialInput(string CredentialType, string CredentialValue, DateTimeOffset? ExpiresAtUtc, string Reason);

public sealed record CredentialSearchInput(string? Query = null, Guid? MemberId = null, string? Status = null, int Page = 1, int PageSize = 50);

public sealed record CredentialSearchResult(IReadOnlyList<CredentialDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Credential lifecycle and resolution (Phase 8 §7.4, Phase 2 CRED-001..010,
/// Phase 6 §10 D3 acceptance). Every state transition is validated against the
/// approved state machine, recorded in credential history and audited.
/// </summary>
public interface ICredentialService
{
    Task<CredentialSearchResult> SearchAsync(CredentialSearchInput input, CancellationToken cancellationToken = default);

    Task<CredentialDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CredentialHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CredentialDto> IssueAsync(IssueCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Assigns the credential to a member and activates it (CRED-004/CRED-005).</summary>
    Task<CredentialDto> ActivateAsync(Guid id, ActivateCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<CredentialDto> SuspendAsync(Guid id, SuspendCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<CredentialDto> MarkLostAsync(Guid id, LostCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<CredentialDto> MarkStolenAsync(Guid id, StolenCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<CredentialDto> ReactivateAsync(Guid id, ReactivateCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Retires the old credential and activates a replacement bound to the same member (CRED-008).</summary>
    Task<CredentialDto> ReplaceAsync(Guid id, ReplaceCredentialInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a card identifier to the live member context (Phase 4 §13, Phase 5 §32).
    /// Unknown cards, inactive credentials and expired credentials are rejected.
    /// </summary>
    Task<ResolveCredentialResult> ResolveAsync(string credentialValue, CancellationToken cancellationToken = default);
}