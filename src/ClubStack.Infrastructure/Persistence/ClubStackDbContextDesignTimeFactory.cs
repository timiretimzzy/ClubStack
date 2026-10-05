using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClubStack.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF tooling (<c>migrations add</c>, <c>database update</c>)
/// can target this project directly without booting the API host. Connection
/// settings come from the environment and default to the local development
/// database; no credential is embedded in source. Scaffolding migrations works
/// offline (an empty password is sufficient because no connection is opened);
/// applying migrations requires <c>CLUBSTACK_DB_PASSWORD</c> to be set.
/// </summary>
public sealed class ClubStackDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ClubStackDbContext>
{
    public ClubStackDbContext CreateDbContext(string[] args)
    {
        var password = Environment.GetEnvironmentVariable("CLUBSTACK_DB_PASSWORD") ?? string.Empty;
        var database = Environment.GetEnvironmentVariable("CLUBSTACK_DB_NAME") ?? "clubstack";
        var user = Environment.GetEnvironmentVariable("CLUBSTACK_DB_USER") ?? "clubstack";
        var host = Environment.GetEnvironmentVariable("CLUBSTACK_DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("CLUBSTACK_DB_PORT") ?? "5432";

        var connectionString =
            $"Host={host};Port={port};Database={database};Username={user};Password={password};Pooling=true";

        var options = new DbContextOptionsBuilder<ClubStackDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ClubStackDbContext(options);
    }
}
