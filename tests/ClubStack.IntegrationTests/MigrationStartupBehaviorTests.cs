using ClubStack.Api;
using Npgsql;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class MigrationStartupBehaviorTests : DatabaseTestBase
{
    public MigrationStartupBehaviorTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Startup_applies_migrations_and_skips_seed_when_disabled()
    {
        var (connectionString, databaseName) = await PostgreSqlTestFixture.CreateScratchDatabaseAsync();
        try
        {
            using var env = SetEnvironment(new Dictionary<string, string?>
            {
                ["ConnectionStrings__Default"] = connectionString,
                ["App__MigrateOnStartup"] = "true",
                ["App__StopOnMigrationFailure"] = "true",
                ["App__EnableDevelopmentSeed"] = "false",
            });

            var application = await ApiBootstrapper.CreateApplicationAsync([]);
            await application.DisposeAsync();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // D5 added the POS/Cashiering migration, the D5 continuation added
            // idempotency scoping and D5 referential integrity, D5.5 added
            // fiscalisation plus the refund-integrity hardening, D6 added the
            // finance integration migration, and D7 added the golf operations
            // migration, so a clean startup now applies the D0, D1, D2, D3, D4, D5,
            // two D5-continuation, two D5.5, one D6 and one D7 migration.
            await using (var command = new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\"", connection))
            {
                Assert.Equal(12L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }

            // D7's eight golf tables exist, and the integrity objects EF cannot
            // express are installed with them.
            await using (var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename IN ('golf_courses','golf_holes','golf_tees','tee_slots','golf_guest_records','golf_bookings','golf_booking_players','golf_charges')",
                connection))
            {
                Assert.Equal(8L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }

            await using (var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_trigger WHERE tgname IN ('TR_golf_bookings_state_machine','TR_golf_booking_players_slot_capacity') AND NOT tgisinternal",
                connection))
            {
                Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }

            // System reference data (baseline roles, Phase 9 §42) is seeded at
            // startup; the assertion is that the *development* seed (users) is
            // skipped when disabled.
            await using (var command = new NpgsqlCommand("SELECT count(*) FROM \"AspNetRoles\"", connection))
            {
                Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) > 0L);
            }

            await using (var command = new NpgsqlCommand("SELECT count(*) FROM \"AspNetUsers\"", connection))
            {
                Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }
        }
        finally
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task Startup_throws_when_migration_fails_and_stopping_is_required()
    {
        using var env = SetEnvironment(new Dictionary<string, string?>
        {
            ["ConnectionStrings__Default"] =
                "Host=127.0.0.1;Port=1;Database=nowhere;Username=clubstack;Password=wrong;Pooling=false;Timeout=2",
            ["App__MigrateOnStartup"] = "true",
            ["App__StopOnMigrationFailure"] = "true",
            ["App__EnableDevelopmentSeed"] = "false",
        });

        await Assert.ThrowsAnyAsync<Exception>(() => ApiBootstrapper.CreateApplicationAsync([]));
    }

    [Fact]
    public async Task Startup_tolerates_migration_failure_when_stopping_is_optional()
    {
        using var env = SetEnvironment(new Dictionary<string, string?>
        {
            ["ConnectionStrings__Default"] =
                "Host=127.0.0.1;Port=1;Database=nowhere;Username=clubstack;Password=wrong;Pooling=false;Timeout=2",
            ["App__MigrateOnStartup"] = "true",
            ["App__StopOnMigrationFailure"] = "false",
            ["App__EnableDevelopmentSeed"] = "false",
        });

        var application = await ApiBootstrapper.CreateApplicationAsync([]);
        await application.DisposeAsync();
    }

    [Fact]
    public async Task Startup_does_not_migrate_when_migration_is_disabled()
    {
        var (connectionString, databaseName) = await PostgreSqlTestFixture.CreateScratchDatabaseAsync();
        try
        {
            using var env = SetEnvironment(new Dictionary<string, string?>
            {
                ["ConnectionStrings__Default"] = connectionString,
                ["App__MigrateOnStartup"] = "false",
                ["App__StopOnMigrationFailure"] = "true",
                ["App__EnableDevelopmentSeed"] = "false",
            });

            var application = await ApiBootstrapper.CreateApplicationAsync([]);
            await application.DisposeAsync();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // No migrations and no schema: the app must not have applied anything.
            await using var history = new NpgsqlCommand(
                "SELECT count(*) FROM pg_tables WHERE schemaname = 'public'", connection);
            Assert.Equal(0L, Convert.ToInt64(await history.ExecuteScalarAsync()));
        }
        finally
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(databaseName);
        }
    }

    private static EnvScope SetEnvironment(IReadOnlyDictionary<string, string?> values)
    {
        var previous = new Dictionary<string, string?>(values.Count);
        foreach (var (key, value) in values)
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        return new EnvScope(previous);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string?> _previous;

        public EnvScope(IReadOnlyDictionary<string, string?> previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}