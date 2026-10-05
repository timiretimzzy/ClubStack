using System.Security.Claims;
using ClubStack.Application.Auditing;
using ClubStack.Application.Correlation;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Platform;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Shared auditing plumbing for D1 command endpoints. Sensitive actions
/// (Phase 8 §5) are always recorded through the audit pipeline.
/// </summary>
public abstract class AuditedControllerBase : ControllerBase
{
    private readonly IAuditRecorder _auditRecorder;
    private readonly ICorrelationContext _correlation;
    private readonly ISystemClock _clock;

    protected AuditedControllerBase(IAuditRecorder auditRecorder, ICorrelationContext correlation, ISystemClock clock)
    {
        _auditRecorder = auditRecorder;
        _correlation = correlation;
        _clock = clock;
    }

    protected async Task RecordAuditAsync(
        string action,
        string entityType,
        string entityId = "",
        string reference = "",
        string result = "Success",
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorName = User.Identity?.Name ?? string.Empty;

        await _auditRecorder.RecordAsync(new AuditRecord
        {
            Id = Guid.CreateVersion7(),
            TimestampUtc = _clock.UtcNow,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reference = reference,
            Result = result,
            CorrelationId = _correlation.CorrelationId,
            MetadataJson = metadataJson ?? string.Empty,
        }, cancellationToken);
    }

    /// <summary>Resolves the primary club for club-scoped configuration (single-club deployment).</summary>
    protected static async Task<Guid> ResolvePrimaryClubIdAsync(IPlatformConfigurationStore store, CancellationToken cancellationToken)
    {
        var club = await store.GetPrimaryClubAsync(cancellationToken)
                   ?? throw new ConflictException("No club is configured yet. Complete club bootstrap first.");
        return club.Id;
    }

    /// <summary>Correlation id for distributed tracing of business operations.</summary>
    protected string CorrelationId => _correlation.CorrelationId;
}