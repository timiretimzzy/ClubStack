using ClubStack.Application.Outbox;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Outbox;

public sealed class EfOutboxStore : IOutboxStore
{
    private readonly ClubStackDbContext _context;

    public EfOutboxStore(ClubStackDbContext context)
    {
        _context = context;
    }

    public async Task EnqueueAsync(string eventType, string payloadJson, string correlationId, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Add(new OutboxMessageEntity
        {
            EventId = Guid.CreateVersion7(),
            EventType = eventType,
            PayloadJson = payloadJson,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = OutboxStatus.Pending,
            CorrelationId = correlationId,
        });

        // When a unit-of-work transaction is already open, stage the message and let
        // the caller's single CommitAsync flush it. Saving here would split the command
        // into two flushes; the second DetectChanges then re-flags freshly inserted
        // graph rows (e.g. sale payments, which carry no concurrency token) as Modified,
        // producing an UPDATE that matches zero rows and fails with a concurrency error.
        if (_context.Database.CurrentTransaction is null)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<OutboxMessageData>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var rows = await _context.OutboxMessages
            .AsNoTracking()
            .Where(e => e.Status == OutboxStatus.Pending)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(e => new OutboxMessageData
            {
                Id = e.Id,
                EventId = e.EventId,
                EventType = e.EventType,
                PayloadJson = e.PayloadJson,
                CreatedAtUtc = e.CreatedAtUtc,
                Status = e.Status,
                RetryCount = e.RetryCount,
                ProcessedAtUtc = e.ProcessedAtUtc,
                ErrorInfo = e.ErrorInfo,
                CorrelationId = e.CorrelationId,
            })
            .ToList();
    }

    public async Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (entity is not null)
        {
            entity.Status = OutboxStatus.Processed;
            entity.ProcessedAtUtc = processedAtUtc;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(Guid messageId, string errorInfo, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (entity is not null)
        {
            entity.Status = OutboxStatus.Failed;
            entity.ErrorInfo = errorInfo;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RecordRetryAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (entity is not null)
        {
            entity.RetryCount += 1;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}