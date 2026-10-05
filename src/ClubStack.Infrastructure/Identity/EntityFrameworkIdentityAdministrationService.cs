using ClubStack.Application.Authorization;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Identity;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Identity;

/// <inheritdoc />
public sealed class EntityFrameworkIdentityAdministrationService : IIdentityAdministrationService
{
    private readonly UserManager<ClubStackUser> _userManager;
    private readonly RoleManager<ClubStackRole> _roleManager;
    private readonly ClubStackDbContext _context;
    private readonly IUserSessionStore _sessionStore;

    public EntityFrameworkIdentityAdministrationService(
        UserManager<ClubStackUser> userManager,
        RoleManager<ClubStackRole> roleManager,
        ClubStackDbContext context,
        IUserSessionStore sessionStore)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _sessionStore = sessionStore;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync(cancellationToken);
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            result.Add(await BuildUserDtoAsync(user, cancellationToken));
        }

        return result;
    }

    public async Task<UserDto> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"User '{id}' was not found.");
        return await BuildUserDtoAsync(user, cancellationToken);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserInput input, CancellationToken cancellationToken = default)
    {
        var user = new ClubStackUser
        {
            UserName = input.UserName,
            Email = input.Email,
            FirstName = input.FirstName,
            LastName = input.LastName,
            PhoneNumber = input.PhoneNumber,
            EmailConfirmed = true,
            IsActive = true,
        };

        var created = await _userManager.CreateAsync(user, input.Password);
        if (!created.Succeeded)
        {
            throw new ValidationException("The user could not be created.", IdentityErrorsToFieldErrors(created.Errors));
        }

        await SyncRolesAsync(user, input.Roles, cancellationToken);
        return await BuildUserDtoAsync(user, cancellationToken);
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserInput input, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"User '{id}' was not found.");

        user.FirstName = input.FirstName;
        user.LastName = input.LastName;
        user.Email = input.Email;
        user.PhoneNumber = input.PhoneNumber;
        user.IsActive = input.IsActive;

        var updated = await _userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new ValidationException("The user could not be updated.", IdentityErrorsToFieldErrors(updated.Errors));
        }

        await SyncRolesAsync(user, input.Roles, cancellationToken);

        if (!input.IsActive)
        {
            await _sessionStore.RevokeAllForUserAsync(user.Id, $"User deactivated by administrator.", null, cancellationToken);
        }

        return await BuildUserDtoAsync(user, cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"User '{id}' was not found.");

        if (!user.IsActive)
        {
            return;
        }

        user.IsActive = false;
        var updated = await _userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new ValidationException("The user could not be deactivated.", IdentityErrorsToFieldErrors(updated.Errors));
        }

        await _sessionStore.RevokeAllForUserAsync(user.Id, reason, null, cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"User '{id}' was not found.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException("The password could not be reset.", IdentityErrorsToFieldErrors(result.Errors));
        }

        await _sessionStore.RevokeAllForUserAsync(user.Id, "Password reset; all sessions revoked.", null, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(cancellationToken);
        var result = new List<RoleDto>();
        foreach (var role in roles)
        {
            result.Add(await BuildRoleDtoAsync(role, cancellationToken));
        }

        return result;
    }

    public async Task<RoleDto> GetRoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"Role '{id}' was not found.");
        return await BuildRoleDtoAsync(role, cancellationToken);
    }

    public async Task<RoleDto> CreateRoleAsync(string name, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("A role name is required.", new Dictionary<string, string[]> { ["name"] = ["A role name is required."] });
        }

        if (await _roleManager.RoleExistsAsync(name))
        {
            throw new ConflictException($"A role named '{name}' already exists.");
        }

        var role = new ClubStackRole(name);
        var created = await _roleManager.CreateAsync(role);
        if (!created.Succeeded)
        {
            throw new ValidationException("The role could not be created.", IdentityErrorsToFieldErrors(created.Errors));
        }

        await ReplaceRolePermissionsAsync(role, permissionKeys, cancellationToken);
        return await BuildRoleDtoAsync(role, cancellationToken);
    }

    public async Task<RoleDto> UpdateRolePermissionsAsync(Guid id, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"Role '{id}' was not found.");

        await ReplaceRolePermissionsAsync(role, permissionKeys, cancellationToken);
        return await BuildRoleDtoAsync(role, cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException($"Role '{id}' was not found.");

        if (role.IsSystem)
        {
            throw new ConflictException($"Role '{role.Name}' is a system role and cannot be deleted.");
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name ?? string.Empty);
        if (usersInRole.Count > 0)
        {
            throw new ConflictException($"Role '{role.Name}' still has {usersInRole.Count} assigned user(s).");
        }

        await ReplaceRolePermissionsAsync(role, Array.Empty<string>(), cancellationToken);

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            throw new ValidationException("The role could not be deleted.", IdentityErrorsToFieldErrors(result.Errors));
        }
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Permissions.AsNoTracking().OrderBy(p => p.Key).ToListAsync(cancellationToken);
        return rows.Select(p => new PermissionDto(p.Key, p.Module, p.Description, p.IsSensitive)).ToList();
    }

    private async Task<UserDto> BuildUserDtoAsync(ClubStackUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var lastLogin = await _context.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.IssuedAtUtc)
            .Select(s => (DateTimeOffset?)s.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.IsActive,
            user.CreatedAtUtc,
            lastLogin,
            roles.OrderBy(r => r, StringComparer.Ordinal).ToList());
    }

    private async Task<RoleDto> BuildRoleDtoAsync(ClubStackRole role, CancellationToken cancellationToken)
    {
        var permissions = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission!.Key)
            .OrderBy(k => k)
            .ToListAsync(cancellationToken);

        return new RoleDto(role.Id, role.Name ?? string.Empty, role.IsSystem, permissions);
    }

    private async Task SyncRolesAsync(ClubStackUser user, IReadOnlyList<string> desiredRoles, CancellationToken cancellationToken)
    {
        var target = desiredRoles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validated = new List<string>();
        foreach (var roleName in target)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                throw new ValidationException($"Role '{roleName}' does not exist.", new Dictionary<string, string[]> { ["roles"] = [$"Role '{roleName}' does not exist."] });
            }

            validated.Add(roleName);
        }

        var current = await _userManager.GetRolesAsync(user);
        foreach (var roleName in current.Except(validated, StringComparer.OrdinalIgnoreCase))
        {
            var removed = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!removed.Succeeded)
            {
                throw new ValidationException("A role could not be removed.", IdentityErrorsToFieldErrors(removed.Errors));
            }
        }

        foreach (var roleName in validated.Except(current, StringComparer.OrdinalIgnoreCase))
        {
            var added = await _userManager.AddToRoleAsync(user, roleName);
            if (!added.Succeeded)
            {
                throw new ValidationException("A role could not be assigned.", IdentityErrorsToFieldErrors(added.Errors));
            }
        }
    }

    private async Task ReplaceRolePermissionsAsync(ClubStackRole role, IReadOnlyList<string> permissionKeys, CancellationToken cancellationToken)
    {
        var normalized = permissionKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();

        var existing = await _context.Permissions
            .AsNoTracking()
            .Where(p => normalized.Contains(p.Key))
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);

        var unknown = normalized.Except(existing, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(
                "One or more permissions are not part of the catalogue.",
                new Dictionary<string, string[]> { ["permissions"] = unknown.ToArray() });
        }

        var assignments = await _context.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync(cancellationToken);
        _context.RolePermissions.RemoveRange(assignments);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var key in normalized)
        {
            var permissionId = await _context.Permissions
                .Where(p => p.Key == key)
                .Select(p => p.Id)
                .FirstAsync(cancellationToken);

            _context.RolePermissions.Add(new RolePermissionEntity
            {
                RoleId = role.Id,
                PermissionId = permissionId,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> IdentityErrorsToFieldErrors(IEnumerable<IdentityError> errors)
    {
        return errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
    }
}