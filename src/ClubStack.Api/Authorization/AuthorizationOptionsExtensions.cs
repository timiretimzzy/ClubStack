using ClubStack.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace ClubStack.Api.Authorization;

public static class AuthorizationOptionsExtensions
{
    /// <summary>Adds an authorization policy that requires the named permission claim.</summary>
    public static void AddPermissionPolicy(this AuthorizationOptions options, string permission)
    {
        options.AddPolicy($"Permission:{permission}", policy =>
            policy.RequireClaim(PermissionClaimTypes.Permission, permission));
    }

    /// <summary>
    /// Adds an authorization policy satisfied by <em>any</em> of the named
    /// permissions (D5 POS member identification: a cashier resolving a member's
    /// card is not necessarily a back-office member record viewer).
    /// </summary>
    public static void AddAnyPermissionPolicy(this AuthorizationOptions options, string policyName, params string[] permissions)
    {
        options.AddPolicy(policyName, policy =>
            policy.RequireAssertion(context =>
                permissions.Any(p => context.User.HasClaim(PermissionClaimTypes.Permission, p))));
    }
}