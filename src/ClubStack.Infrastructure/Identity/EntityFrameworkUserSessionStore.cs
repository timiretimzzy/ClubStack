using ClubStack.Application.Identity;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Identity;

/// <inheritdoc />
public sealed class EntityFrameworkUserSessionStore : IUserSessionStore
{
    private readonly ClubStackDbContext _context;

    public EntityFrameworkUserSessionStore(ClubStackDbContext context)
    {
        _context = context;
    }

    public async Task<UserSessionDto> CreateAsync(
        Guid sessionId,
        Guid userId,
        string userName,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        _context.UserSessions.Add(new UserSessionEntity
        {
            Id = sessionId,
            UserId = userId,
            UserName = userName,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            LastSeenAtUtc = issuedAtUtc,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(await _context.UserSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, cancellationToken));
    }

    public async Task<UserSessionDto?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task TouchAsync(Guid sessionId, DateTimeOffset lastSeenAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastSeenAtUtc = lastSeenAtUtc;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid sessionId, string reason, Guid? revokedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (entity is null || entity.IsTerminated)
        {
            return;
        }

        entity.RevokedAtUtc = DateTimeOffset.UtcNow;
        entity.RevokedByUserId = revokedByUserId;
        entity.RevocationReason = reason;
        entity.IsTerminated = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, Guid? revokedByUserId, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var entity in await _context.UserSessions.Where(s => s.UserId == userId && !s.IsTerminated).ToListAsync(cancellationToken))
        {
            entity.RevokedAtUtc = nowUtc;
            entity.RevokedByUserId = revokedByUserId;
            entity.RevocationReason = reason;
            entity.IsTerminated = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSessionDto>> ListAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        IQueryable<UserSessionEntity> query = _context.UserSessions.AsNoTracking().OrderByDescending(s => s.IssuedAtUtc);

        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    private static UserSessionDto ToDto(UserSessionEntity e) => new(
        e.Id,
        e.UserId,
        e.UserName,
        e.IssuedAtUtc,
        e.ExpiresAtUtc,
        e.LastSeenAtUtc,
        e is { IsTerminated: false, RevokedAtUtc: null } && e.ExpiresAtUtc > DateTimeOffset.UtcNow,
        e.RevokedAtUtc,
        e.RevocationReason);
}