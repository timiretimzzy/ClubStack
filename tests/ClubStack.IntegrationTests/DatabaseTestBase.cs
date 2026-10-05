using ClubStack.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Common surface for tests in the "Database" collection. Every test class in
/// the collection shares one migrated PostgreSQL database plus the production
/// service wiring produced by <see cref="PostgreSqlTestFixture"/>.
/// </summary>
public abstract class DatabaseTestBase
{
    protected DatabaseTestBase(PostgreSqlTestFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgreSqlTestFixture Fixture { get; }

    protected IServiceProvider Services => Fixture.ServiceProvider;

    /// <summary>
    /// Uppercase, alphanumeric-only unique prefix (e.g. "MEM0A1B2C3D"). Must not
    /// contain '-' because business references are formatted/parsed as PREFIX-SEQUENCE,
    /// and the generator normalizes prefixes to upper-case on creation.
    /// </summary>
    protected static string UniquePrefix(string tag) => $"{tag}{Guid.NewGuid():N}"[..(tag.Length + 8)].ToUpperInvariant();

    /// <summary>
    /// Executes <see cref="OutboxProcessorJob"/> <paramref name="executions"/> times
    /// against the provider's own scope factory (mirrors the Quartz host).
    /// </summary>
    protected async Task RunOutboxJobAsync(IServiceProvider services, int executions = 1)
    {
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<OutboxProcessorJob>>();
        var job = new OutboxProcessorJob(scopeFactory, logger);

        for (var i = 0; i < executions; i++)
        {
            await job.Execute(null!, default);
        }
    }
}