using ClubStack.Application;
using ClubStack.Infrastructure;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Builds a real service container using the same <c>AddInfrastructure</c>
/// registration the API/Host use, against a PostgreSQL connection. An optional
/// hook lets a single test augment registrations (e.g. a capturing outbox
/// handler) without duplicating the production wiring.
/// </summary>
public static class TestServiceProvider
{
    public static ServiceProvider Build(string connectionString, Action<IServiceCollection>? configureServices = null)
    {
        return Build(connectionString, null, configureServices);
    }

    public static ServiceProvider Build(
        string connectionString,
        IReadOnlyDictionary<string, string?>? additionalConfiguration,
        Action<IServiceCollection>? configureServices = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["App:DevelopmentTools"] = "false",
            ["App:MigrateOnStartup"] = "false",
            ["App:EnableDevelopmentSeed"] = "false",
            ["App:EnableBootstrap"] = "false",
            ["App:DevelopmentAdminUserName"] = "",
            ["App:DevelopmentAdminPassword"] = "",
        };

        if (additionalConfiguration is not null)
        {
            foreach (var (key, value) in additionalConfiguration)
            {
                settings[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    public static async Task<ServiceProvider> BuildAgainstFreshDatabaseAsync(
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    {
        var (connectionString, _) = await PostgreSqlTestFixture.CreateScratchDatabaseAsync();
        return Build(connectionString, additionalConfiguration);
    }
}

/// <summary>
/// Creates and drops ephemeral, uniquely named PostgreSQL databases for the
/// D0 integration suite. Uses the 'clubstack' role (granted CREATEDB) so a
/// superuser is not required by the tests.
/// </summary>
public sealed class PostgreSqlTestFixture : IAsyncLifetime
{
    public string DatabaseName { get; private set; } = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public ServiceProvider ServiceProvider { get; private set; } = null!;

    public IDbContextFactory<ClubStackDbContext> ContextFactory { get; private set; } = null!;

    private string _adminConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        (ConnectionString, DatabaseName) = await CreateScratchDatabaseAsync();

        ServiceProvider = TestServiceProvider.Build(ConnectionString);
        ContextFactory = ServiceProvider.GetRequiredService<IDbContextFactory<ClubStackDbContext>>();

        var migrator = ServiceProvider.GetRequiredService<IDatabaseMigrator>();
        await migrator.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (ServiceProvider is not null)
        {
            await ServiceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(DatabaseName))
        {
            await DropDatabaseAsync(DatabaseName);
        }
    }

    /// <summary>Builds an admin connection string for the maintenance database.</summary>
    public static string BuildAdminConnectionString()
    {
        var password = Environment.GetEnvironmentVariable("CLUBSTACK_DB_PASSWORD")
            ?? throw new InvalidOperationException(
                "CLUBSTACK_DB_PASSWORD must be set to run the D0 integration tests. " +
                "See src/ClubStack.Api/appsettings.json and docs for local setup.");
        var host = Environment.GetEnvironmentVariable("CLUBSTACK_DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("CLUBSTACK_DB_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("CLUBSTACK_DB_USER") ?? "clubstack";

        return $"Host={host};Port={port};Database=postgres;Username={user};Password={password};Pooling=false";
    }

    public static async Task<(string ConnectionString, string DatabaseName)> CreateScratchDatabaseAsync()
    {
        var databaseName = $"clubstack_it_{Guid.NewGuid():N}";

        await using var admin = new NpgsqlConnection(BuildAdminConnectionString());
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();

        var host = Environment.GetEnvironmentVariable("CLUBSTACK_DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("CLUBSTACK_DB_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("CLUBSTACK_DB_USER") ?? "clubstack";
        var password = Environment.GetEnvironmentVariable("CLUBSTACK_DB_PASSWORD") ?? string.Empty;

        var connectionString =
            $"Host={host};Port={port};Database={databaseName};Username={user};Password={password};Pooling=true;Maximum Pool Size=64";

        return (connectionString, databaseName);
    }

    public static async Task DropDatabaseAsync(string databaseName)
    {
        // Test hosts keep pooled connections open; close them so the drop can proceed.
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(BuildAdminConnectionString());
        await admin.OpenAsync();

        // WITH (FORCE) terminates lingering backends, which needs pg_signal_backend.
        // The test role only has CREATEDB, so fall back to a plain drop and, if
        // connections are still attached, leave the uniquely named scratch database
        // behind rather than failing an otherwise passing test.
        try
        {
            await using var forced = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await forced.ExecuteNonQueryAsync();
            return;
        }
        catch (PostgresException)
        {
        }

        try
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }
        catch (PostgresException)
        {
        }
    }
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<PostgreSqlTestFixture>
{
}
