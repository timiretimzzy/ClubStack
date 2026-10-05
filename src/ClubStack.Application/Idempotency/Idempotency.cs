namespace ClubStack.Application.Idempotency;

public enum IdempotencyStatus
{
    InProgress = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>A previously stored idempotent operation result.</summary>
public sealed record StoredIdempotencyResult
{
    public required string IdempotencyKey { get; init; }

    public required IdempotencyStatus Status { get; init; }

    public string? ResultJson { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public string ActorUserId { get; init; } = string.Empty;

    /// <summary>Hash of the canonical request payload the key was first used with.</summary>
    public string? RequestHash { get; init; }
}

/// <summary>
/// Stable fingerprint of a request payload, so a key reused with a materially
/// different request is detected instead of replaying an unrelated result.
/// </summary>
public static class IdempotencyRequestHash
{
    public static string Compute(string canonicalPayload)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonicalPayload));
        return System.Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Durable store for idempotent command execution. Later modules use this to
/// guarantee that a retried command returns the stored result instead of
/// executing a second time.
/// </summary>
public interface IIdempotencyStore
{
    Task<StoredIdempotencyResult?> GetAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a key within its (key, operation, actor) scope. Returns the
    /// stored result if the key already exists for that scope, otherwise creates an
    /// <see cref="IdempotencyStatus.InProgress"/> record. A previously
    /// <see cref="IdempotencyStatus.Failed"/> record is re-claimed so a failed
    /// operation stays retryable with the same key.
    /// </summary>
    Task<(bool Created, StoredIdempotencyResult? Existing)> TryAcquireAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        string? requestHash,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        string resultJson,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        string error,
        CancellationToken cancellationToken = default);
}