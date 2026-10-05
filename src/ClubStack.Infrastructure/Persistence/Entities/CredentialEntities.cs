using ClubStack.Domain.Credentials;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// A credential (membership card) separate from the member record (CRED-001,
/// Phase 3 §15, Phase 8 §8.4 <c>credentials</c>). The current state is the single
/// authoritative state; every change is recorded in credential history.
/// </summary>
public sealed class CredentialEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string CredentialNumber { get; set; } = string.Empty;

    public CredentialType CredentialType { get; set; }

    /// <summary>Minimum card identifier used for resolution; never raw track data (Phase 9).</summary>
    public string CredentialValue { get; set; } = string.Empty;

    public CredentialStatus Status { get; set; } = CredentialStatus.Issued;

    /// <summary>Member the credential is assigned to once activated (CRED-004).</summary>
    public Guid? MemberId { get; set; }

    public MemberEntity? Member { get; set; }

    public DateTimeOffset? ActivatedAtUtc { get; set; }

    public DateTimeOffset? SuspendedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>The replacement credential that superseded this one (CRED-008).</summary>
    public Guid? ReplacedByCredentialId { get; set; }

    public CredentialEntity? ReplacedByCredential { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<CredentialHistoryEntity> History { get; set; } = new();
}

/// <summary>
/// Append-only credential state history (CRED-009, Phase 8 §8.4
/// <c>credential_history</c>). Previous and new states are stored so the full
/// lifecycle remains available after replacement or expiry.
/// </summary>
public sealed class CredentialHistoryEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CredentialId { get; set; }

    public CredentialEntity Credential { get; set; } = null!;

    public CredentialStatus PreviousState { get; set; }

    public CredentialStatus NewState { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}