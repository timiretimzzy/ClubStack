using System.Data;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClubStack.Api.Health;

/// <summary>
/// Database connectivity probe used by the readiness health check. It runs a
/// trivial query and never surfaces credentials or connection strings.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<ClubStackDbContext> _contextFactory;

    public DatabaseHealthCheck(IDbContextFactory<ClubStackDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var connection = dbContext.Database.GetDbConnection();
            var wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
            }
            finally
            {
                if (wasClosed)
                {
                    await connection.CloseAsync();
                }
            }

            return HealthCheckResult.Healthy("Database connectivity verified.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", exception);
        }
    }
}