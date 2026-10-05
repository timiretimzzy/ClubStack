using ClubStack.Infrastructure.Identity;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Permission catalogue row (Phase 8 §8.2 <c>permissions</c>). Seeded from the
/// authoritative <see cref="ClubStack.Application.Authorization.Permissions.Catalog"/>.
/// </summary>
public sealed class PermissionEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Authoritative <c>Module.Resource.Action</c> key.</summary>
    public string Key { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsSensitive { get; set; }

    /// <summary>System permissions cannot be deleted; they are catalogue entries.</summary>
    public bool IsSystem { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Role-permission assignment (Phase 8 §8.2 <c>role_permissions</c>).</summary>
public sealed class RolePermissionEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoleId { get; set; }

    public ClubStackRole Role { get; set; } = null!;

    public Guid PermissionId { get; set; }

    public PermissionEntity Permission { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Server-side user session row (Phase 8 §8.2 <c>user_sessions</c>, §23 sessions).
/// Enables administrator revocation, session listing and idle-timeout enforcement
/// independent of the client's auth cookie.
/// </summary>
public sealed class UserSessionEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public ClubStackUser User { get; set; } = null!;

    public string UserName { get; set; } = string.Empty;

    public DateTimeOffset IssuedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Revocation is triggered by logout for the acting session; null for administrator/expiry revocations.</summary>
    public Guid? RevokedByUserId { get; set; }

    public string RevocationReason { get; set; } = string.Empty;

    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>True when the session ended normally (logout) or via expiry/revocation.</summary>
    public bool IsTerminated { get; set; }
}