namespace ClubStack.Application.Auditing;

/// <summary>The technical shape of an audit record. Do not store secrets here.</summary>
public sealed record AuditRecord
{
    public required Guid Id { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public string ActorUserId { get; init; } = string.Empty;

    public string ActorName { get; init; } = string.Empty;

    public required string Action { get; init; }

    public string EntityType { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string MetadataJson { get; init; } = string.Empty;
}

/// <summary>Filterable, paged audit query.</summary>
public sealed record AuditQueryInput(
    string? Action = null,
    string? ActorUserId = null,
    string? ActorName = null,
    string? EntityType = null,
    string? CorrelationId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditQueryResult(IReadOnlyList<AuditRecord> Items, int TotalCount, int Page, int PageSize);

public interface IAuditRecorder
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>Reads recent audit records, optionally filtered by action, newest first.</summary>
    Task<IReadOnlyList<AuditRecord>> GetRecentAsync(string? action, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Runs a paged audit query (D1, Phase 8 §8.2 audit + Phase 5 §71).</summary>
    Task<AuditQueryResult> QueryAsync(AuditQueryInput query, CancellationToken cancellationToken = default);
}