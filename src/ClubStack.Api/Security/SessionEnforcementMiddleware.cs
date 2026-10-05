using System.Security.Claims;
using ClubStack.Application.Authorization;
using ClubStack.Application.Identity;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ClubStack.Api.Security;

/// <summary>
/// Enforces the server-side session model (Phase 8 §23) for every authenticated
/// request: the signed-in principal must carry a valid <c>session_id</c> claim
/// whose <c>user_sessions</c> row is active, not revoked and within its absolute
/// lifetime and idle-timeout. Violations terminate the server session, sign the
/// user out and return 401. This runs after authentication and before authorization.
/// </summary>
public sealed class SessionEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityOptions _securityOptions;

    public SessionEnforcementMiddleware(RequestDelegate next, IOptions<SecurityOptions> securityOptions)
    {
        _next = next;
        _securityOptions = securityOptions.Value;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUserSessionStore sessionStore,
        UserManager<ClubStackUser> userManager)
    {
        var principal = context.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var sessionClaim = principal.FindFirst(PermissionClaimTypes.Session);
        if (sessionClaim is null || !Guid.TryParse(sessionClaim.Value, out var sessionId))
        {
            await TerminateAsync(context, "session_claim_missing",
                "The signed-in session is not recognized. Please sign in again.");
            return;
        }

        var session = await sessionStore.GetAsync(sessionId, context.RequestAborted);
        if (session is null || session.RevokedAtUtc is not null || !session.IsActive)
        {
            await TerminateAsync(context, "session_revoked",
                session is null ? "The session no longer exists. Please sign in again." : "This session has been revoked. Please sign in again.");
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;

        if (session.ExpiresAtUtc <= utcNow)
        {
            await sessionStore.RevokeAsync(session.SessionId, "Absolute session lifetime expired.", null, context.RequestAborted);
            await TerminateAsync(context, "session_expired", "This session has expired. Please sign in again.");
            return;
        }

        if (utcNow - session.LastSeenAtUtc > _securityOptions.IdleTimeout)
        {
            await sessionStore.RevokeAsync(session.SessionId, "Idle timeout exceeded.", null, context.RequestAborted);
            await TerminateAsync(context, "session_idle_timeout", "This session was idle too long. Please sign in again.");
            return;
        }

        var user = await userManager.FindByIdAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
        if (user is null || !user.IsActive)
        {
            await sessionStore.RevokeAsync(session.SessionId, user is null ? "User no longer exists." : "User deactivated.", null, context.RequestAborted);
            await TerminateAsync(context, "session_account_inactive", "This account is no longer active.");
            return;
        }

        if (utcNow - session.LastSeenAtUtc >= TimeSpan.FromMinutes(1))
        {
            await sessionStore.TouchAsync(session.SessionId, utcNow, context.RequestAborted);
        }

        await _next(context);
    }

    private static async Task TerminateAsync(HttpContext context, string code, string detail)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://clubstack.local/problems/session",
            title = "Session is no longer valid.",
            status = StatusCodes.Status401Unauthorized,
            detail,
            code,
        });
    }
}
