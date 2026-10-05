using ClubStack.Application.Auditing;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Auditing;

public sealed class EfAuditRecorder : IAuditRecorder
{
    private readonly ClubStackDbContext _context;

    public EfAuditRecorder(ClubStackDbContext context)
    {
        _context = context;
    }

    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Add(new AuditLogEntity
        {
            Id = record.Id,
            TimestampUtc = record.TimestampUtc,
            ActorUserId = record.ActorUserId,
            ActorName = record.ActorName,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Reference = record.Reference,
            Result = record.Result,
            CorrelationId = record.CorrelationId,
            MetadataJson = record.MetadataJson,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetRecentAsync(string? action, int take = 50, CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLogEntity> query = _context.AuditLogs.AsNoTracking().OrderByDescending(e => e.TimestampUtc);

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(e => e.Action == action);
        }

        var rows = await query.Take(take).ToListAsync(cancellationToken);

        return rows
            .Select(ToAuditRecord)
            .ToList();
    }

    public async Task<AuditQueryResult> QueryAsync(AuditQueryInput query, CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLogEntity> baseQuery = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            baseQuery = baseQuery.Where(e => e.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
        {
            baseQuery = baseQuery.Where(e => e.ActorUserId == query.ActorUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorName))
        {
            baseQuery = baseQuery.Where(e => e.ActorName.Contains(query.ActorName));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            baseQuery = baseQuery.Where(e => e.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            baseQuery = baseQuery.Where(e => e.CorrelationId == query.CorrelationId);
        }

        if (query.From.HasValue)
        {
            baseQuery = baseQuery.Where(e => e.TimestampUtc >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            baseQuery = baseQuery.Where(e => e.TimestampUtc <= query.To.Value);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderByDescending(e => e.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AuditQueryResult(rows.Select(ToAuditRecord).ToList(), totalCount, page, pageSize);
    }

    private static AuditRecord ToAuditRecord(AuditLogEntity e) => new()
    {
        Id = e.Id,
        TimestampUtc = e.TimestampUtc,
        ActorUserId = e.ActorUserId,
        ActorName = e.ActorName,
        Action = e.Action,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        Reference = e.Reference,
        Result = e.Result,
        CorrelationId = e.CorrelationId,
        MetadataJson = e.MetadataJson ?? string.Empty,
    };
}