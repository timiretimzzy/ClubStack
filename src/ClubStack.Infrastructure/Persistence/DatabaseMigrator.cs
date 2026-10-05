using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Persistence;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseMigrator : IDatabaseMigrator
{
    private readonly IDbContextFactory<ClubStackDbContext> _contextFactory;

    public DatabaseMigrator(IDbContextFactory<ClubStackDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }
}