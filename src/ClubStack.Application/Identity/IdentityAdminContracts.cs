namespace ClubStack.Application.Identity;

public sealed record UserDto(
    Guid Id,
    string UserName,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    IReadOnlyList<string> Roles);

public sealed record CreateUserInput(
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Password,
    IReadOnlyList<string> Roles);

public sealed record UpdateUserInput(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record RoleDto(
    Guid Id,
    string Name,
    bool IsSystem,
    IReadOnlyList<string> Permissions);

public sealed record PermissionDto(string Key, string Module, string Description, bool IsSensitive);

public sealed record UserSessionDto(
    Guid SessionId,
    Guid UserId,
    string UserName,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset LastSeenAtUtc,
    bool IsActive,
    DateTimeOffset? RevokedAtUtc,
    string RevocationReason);

/// <summary>
/// Identity administration for D1 (Phase 8 §8.2): user, role and role-permission
/// management backed by the Identity tables. Effective permissions remain the
/// union of legacy role claims and the stored <c>role_permissions</c>.
/// </summary>
public interface IIdentityAdministrationService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserDto> GetUserAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserDto> CreateUserAsync(CreateUserInput input, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserInput input, CancellationToken cancellationToken = default);

    Task DeactivateUserAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<RoleDto> GetRoleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RoleDto> CreateRoleAsync(string name, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateRolePermissionsAsync(Guid id, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Server-side session store (Phase 8 §8.2 <c>user_sessions</c>). Session rows
/// are created at sign-in, touched on activity and revoked on logout, expiry,
/// admin revocation or user deactivation.
/// </summary>
public interface IUserSessionStore
{
    Task<UserSessionDto> CreateAsync(
        Guid sessionId,
        Guid userId,
        string userName,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<UserSessionDto?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task TouchAsync(Guid sessionId, DateTimeOffset lastSeenAtUtc, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid sessionId, string reason, Guid? revokedByUserId, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, string reason, Guid? revokedByUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSessionDto>> ListAsync(Guid? userId, CancellationToken cancellationToken = default);
}