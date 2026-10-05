namespace ClubStack.Application.Outbox;

public enum OutboxStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2,
}

/// <summary>Payload shape of a durable outbox message.</summary>
public sealed record OutboxMessageData
{
    public required Guid Id { get; init; }

    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required string PayloadJson { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public OutboxStatus Status { get; init; }

    public int RetryCount { get; init; }

    public DateTimeOffset? ProcessedAtUtc { get; init; }

    public string? ErrorInfo { get; init; }

    public string CorrelationId { get; init; } = string.Empty;
}

public interface IOutboxStore
{
    /// <summary>Adds a message to the outbox in the caller's active transaction.</summary>
    Task EnqueueAsync(
        string eventType,
        string payloadJson,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessageData>> GetPendingAsync(int take, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid messageId, string errorInfo, CancellationToken cancellationToken = default);

    Task RecordRetryAsync(Guid messageId, CancellationToken cancellationToken = default);
}

/// <summary>Delivers an outbox message to the registered dispatch pipeline.</summary>
public interface IEventDispatcher
{
    Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken = default);
}