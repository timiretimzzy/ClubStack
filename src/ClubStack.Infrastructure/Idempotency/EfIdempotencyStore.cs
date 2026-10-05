using ClubStack.Application.Idempotency;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Idempotency;

/// <summary>
/// Durable idempotency store scoped to the (key, operation, authenticated actor)
/// triple. A completed record replays its stored result; a failed record is
/// re-claimed so the client can safely retry with the same key.
/// </summary>
public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly ClubStackDbContext _context;

    public EfIdempotencyStore(ClubStackDbContext context)
    {
        _context = context;
    }

    public async Task<StoredIdempotencyResult?> GetAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.IdempotencyKey == idempotencyKey
                     && e.Operation == operation
                     && e.ActorUserId == actorUserId,
                cancellationToken);

        return entity is null ? null : ToResult(entity);
    }

    public async Task<(bool Created, StoredIdempotencyResult? Existing)> TryAcquireAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        string? requestHash,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(
                e => e.IdempotencyKey == idempotencyKey
                     && e.Operation == operation
                     && e.ActorUserId == actorUserId,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == IdempotencyStatus.Failed)
            {
                // The previous attempt failed and rolled back, so re-claim the key and
                // let the caller retry the operation rather than wedging it forever.
                existing.Status = IdempotencyStatus.InProgress;
                existing.ResultJson = null;
                existing.ErrorInfo = null;
                existing.CompletedAtUtc = null;
                existing.RequestHash = requestHash;
                existing.CorrelationId = correlationId;
                await _context.SaveChangesAsync(cancellationToken);
                return (true, null);
            }

            return (false, ToResult(existing));
        }

        // The composite unique index protects the race between two concurrent claims.
        _context.IdempotencyRecords.Add(new IdempotencyRecordEntity
        {
            IdempotencyKey = idempotencyKey,
            Operation = operation,
            ActorUserId = actorUserId,
            RequestHash = requestHash,
            Status = IdempotencyStatus.InProgress,
            CorrelationId = correlationId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the race: the key was created by a concurrent request.
            _context.ChangeTracker.Clear();

            var winner = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    e => e.IdempotencyKey == idempotencyKey
                         && e.Operation == operation
                         && e.ActorUserId == actorUserId,
                    cancellationToken);

            return winner is null ? (false, null) : (false, ToResult(winner));
        }

        return (true, null);
    }

    public async Task CompleteAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(idempotencyKey, operation, actorUserId, cancellationToken);

        if (entity is not null)
        {
            entity.Status = IdempotencyStatus.Completed;
            entity.ResultJson = resultJson;
            entity.ErrorInfo = null;
            entity.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task FailAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(idempotencyKey, operation, actorUserId, cancellationToken);

        if (entity is not null)
        {
            entity.Status = IdempotencyStatus.Failed;
            entity.ErrorInfo = error;
            entity.ResultJson = null;
            entity.CompletedAtUtc = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private Task<IdempotencyRecordEntity?> FindAsync(
        string idempotencyKey,
        string operation,
        string actorUserId,
        CancellationToken cancellationToken)
        => _context.IdempotencyRecords.FirstOrDefaultAsync(
            e => e.IdempotencyKey == idempotencyKey
                 && e.Operation == operation
                 && e.ActorUserId == actorUserId,
            cancellationToken);

    private static StoredIdempotencyResult ToResult(IdempotencyRecordEntity entity) => new()
    {
        IdempotencyKey = entity.IdempotencyKey,
        Status = entity.Status,
        ResultJson = entity.ResultJson,
        CompletedAtUtc = entity.CompletedAtUtc,
        CorrelationId = entity.CorrelationId,
        Operation = entity.Operation,
        ActorUserId = entity.ActorUserId,
        RequestHash = entity.RequestHash,
    };
}
