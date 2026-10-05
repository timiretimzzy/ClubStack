using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Identity;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>Server-side session administration (D1 Identity, Phase 8 §8.2 <c>user_sessions</c>, §23).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/sessions")]
[Authorize]
public sealed class SessionsController : AuditedControllerBase
{
    private readonly IUserSessionStore _sessionStore;

    public SessionsController(
        IUserSessionStore sessionStore,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _sessionStore = sessionStore;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserView)]
    [ProducesResponseType(typeof(IReadOnlyList<SessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var sessions = await _sessionStore.ListAsync(userId, cancellationToken);
        return Ok(sessions.Select(ToResponse).ToList());
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = "Permission:" + Permissions.IdentitySessionRevoke)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _sessionStore.GetAsync(id, cancellationToken)
                      ?? throw new Application.Exceptions.NotFoundException($"Session '{id}' was not found.");

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Revoked by administrator." : request.Reason;
        await _sessionStore.RevokeAsync(id, reason, null, cancellationToken);

        await RecordAuditAsync(
            "Identity.Session.Revoke",
            "UserSession",
            id.ToString(),
            session.UserName,
            metadataJson: System.Text.Json.JsonSerializer.Serialize(new { reason }),
            cancellationToken: cancellationToken);

        return NoContent();
    }

    private static SessionResponse ToResponse(UserSessionDto d) => new()
    {
        SessionId = d.SessionId,
        UserId = d.UserId,
        UserName = d.UserName,
        IssuedAtUtc = d.IssuedAtUtc,
        ExpiresAtUtc = d.ExpiresAtUtc,
        LastSeenAtUtc = d.LastSeenAtUtc,
        IsActive = d.IsActive,
        RevokedAtUtc = d.RevokedAtUtc,
        RevocationReason = d.RevocationReason,
    };
}