using System.Security.Claims;
using ClubStack.Application.Authorization;
using ClubStack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubStack.Infrastructure.Identity;

/// <summary>
/// Claims factory that materializes effective permissions for the signed-in
/// principal. A user's effective permission set is the union of
/// (a) permission claims assigned to the user's roles (legacy claim model) and
/// (b) the stored <c>role_permissions</c> assignments (Phase 8 §8.2). This keeps
/// both representations consistent during the D1 transition.
/// </summary>
public sealed class ClubStackUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ClubStackUser, ClubStackRole>
{
    private readonly ClubStackDbContext _context;

    public ClubStackUserClaimsPrincipalFactory(
        UserManager<ClubStackUser> userManager,
        RoleManager<ClubStackRole> roleManager,
        IOptions<IdentityOptions> options,
        ClubStackDbContext context)
        : base(userManager, roleManager, options)
    {
        _context = context;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ClubStackUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

        if (!user.IsActive)
        {
            identity.AddClaim(new Claim(PermissionClaimTypes.AccountStatus, PermissionClaimTypes.Deactivated));
        }

        var roleNames = await UserManager.GetRolesAsync(user);

        foreach (var roleName in roleNames)
        {
            var role = await RoleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            foreach (var claim in await RoleManager.GetClaimsAsync(role))
            {
                if (string.Equals(claim.Type, PermissionClaimTypes.Permission, StringComparison.Ordinal))
                {
                    identity.AddClaim(new Claim(PermissionClaimTypes.Permission, claim.Value));
                }
            }
        }

        var roleIds = roleNames
            .Select(name => _context.Roles
                .Where(r => r.Name == name)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefault())
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();

        if (roleIds.Length > 0)
        {
            var storedPermissionKeys = await _context.RolePermissions
                .AsNoTracking()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission!.Key)
                .Distinct()
                .ToListAsync();

            foreach (var key in storedPermissionKeys)
            {
                identity.AddClaim(new Claim(PermissionClaimTypes.Permission, key));
            }
        }

        return identity;
    }
}