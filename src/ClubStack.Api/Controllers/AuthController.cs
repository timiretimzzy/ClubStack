using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Identity;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using ClubStack.Domain.Common;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Authentication foundation: establishes the authenticated session through
/// ASP.NET Core Identity cookies with a corresponding server-side
/// <c>user_sessions</c> row (Phase 8 §23). User-management UI, registration
/// flows and password reset are D1 identity-administration endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ClubStackUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ClubStackUser> _claimsFactory;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ICorrelationContext _correlation;
    private readonly IUserSessionStore _sessionStore;
    private readonly SecurityOptions _securityOptions;
    private readonly ISystemClock _clock;

    public AuthController(
        UserManager<ClubStackUser> userManager,
        IUserClaimsPrincipalFactory<ClubStackUser> claimsFactory,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        IUserSessionStore sessionStore,
        IOptions<SecurityOptions> securityOptions,
        ISystemClock clock)
    {
        _userManager = userManager;
        _claimsFactory = claimsFactory;
        _auditRecorder = auditRecorder;
        _correlation = correlation;
        _sessionStore = sessionStore;
        _securityOptions = securityOptions.Value;
        _clock = clock;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByNameAsync(request.UserName)
                   ?? await _userManager.FindByEmailAsync(request.UserName);

        var passwordValid = user is not null && await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid || user is null || !user.IsActive)
        {
            await RecordAuthAuditAsync(request.UserName, "Failed");
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials.",
                type: $"https://clubstack.local/problems/{ApiErrorCode.AuthenticationRequired}",
                extensions: new Dictionary<string, object?> { ["code"] = ApiErrorCode.AuthenticationRequired });
        }

var sessionId = Guid.CreateVersion7();
        await _sessionStore.CreateAsync(
            sessionId,
            user.Id,
            user.UserName ?? user.Email ?? string.Empty,
            _clock.UtcNow,
            _clock.UtcNow.Add(_securityOptions.SessionLifetime),
            cancellationToken);

        var principal = await _claimsFactory.CreateAsync(user);
        principal.Identities.First().AddClaim(new Claim(
            PermissionClaimTypes.Session,
            sessionId.ToString(),
            ClaimValueTypes.String));

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        await RecordAuthAuditAsync(user.UserName ?? user.Email ?? string.Empty, "Success");

        return Ok(new LoginResponse
        {
            RequiresTwoFactor = false,
            UserName = user.UserName ?? string.Empty,
            DisplayName = string.Join(' ', new[] { user.FirstName, user.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } display
                ? display
                : (user.UserName ?? string.Empty),
        });
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var sessionClaim = User.FindFirst(PermissionClaimTypes.Session);
        if (sessionClaim is not null && Guid.TryParse(sessionClaim.Value, out var sessionId))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _sessionStore.RevokeAsync(
                sessionId,
                "Signed out by user.",
                Guid.TryParse(userId, out var parsed) ? parsed : null);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await RecordAuthAuditAsync(User.Identity?.Name ?? string.Empty, "SignedOut");
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User)
                   ?? throw new Application.Exceptions.UnauthorizedAccessException("Session user no longer exists.");

        var permissions = User.Claims
            .Where(claim => string.Equals(claim.Type, PermissionClaimTypes.Permission, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        var roles = User.Claims
            .Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        var displayName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.UserName ?? user.Email ?? string.Empty;
        }

        return Ok(new CurrentUserResponse
        {
            UserName = user.UserName ?? string.Empty,
            DisplayName = displayName,
            Permissions = permissions,
            Roles = roles,
        });
    }

    private async Task RecordAuthAuditAsync(string userName, string result)
    {
        await _auditRecorder.RecordAsync(new AuditRecord
        {
            Id = Guid.CreateVersion7(),
            TimestampUtc = _clock.UtcNow,
            ActorName = userName,
            Action = "Auth.Login",
            EntityType = "Authentication",
            Result = result,
            CorrelationId = _correlation.CorrelationId,
        }, CancellationToken.None);
    }
}