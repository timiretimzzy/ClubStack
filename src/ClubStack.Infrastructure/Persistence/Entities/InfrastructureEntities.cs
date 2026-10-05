using ClubStack.Application.Idempotency;
using ClubStack.Application.Outbox;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>Append-oriented audit log row. See Phase 8 §10 (immutable records).</summary>
public sealed class AuditLogEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset TimestampUtc { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string? MetadataJson { get; set; }
}

/// <summary>Per-prefix reference counter. Phase 4 §17 / Phase 8 reference foundation.</summary>
public sealed class ReferenceSequenceEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Prefix { get; set; } = string.Empty;

    public long LastNumber { get; set; }

    public int FormatWidth { get; set; } = 6;
}

public sealed class IdempotencyRecordEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Operation the key was claimed for (scopes the key namespace).</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Authenticated actor the key was claimed for (scopes the key namespace).</summary>
    public string ActorUserId { get; set; } = string.Empty;

    /// <summary>Hash of the canonical request payload the key was first used with.</summary>
    public string? RequestHash { get; set; }

    public IdempotencyStatus Status { get; set; }

    public string? ResultJson { get; set; }

    public string? ErrorInfo { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; } = Guid.CreateVersion7();

    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int RetryCount { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public string? ErrorInfo { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}