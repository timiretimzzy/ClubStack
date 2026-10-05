using ClubStack.Api;
using ClubStack.Application.Bootstrap;
using ClubStack.Application.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class D1BootstrapTests : DatabaseTestBase
{
    public D1BootstrapTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Full_bootstrap_flow_completes_and_prevents_repeat()
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
                ["App__EnableBootstrap"] = "true",
            });

            var application = await ApiBootstrapper.CreateApplicationAsync([]);
            try
            {
                await using (var scope = application.Services.CreateAsyncScope())
                {
                    var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapService>();

                    var initial = await bootstrap.GetStatusAsync();
                    Assert.False(initial.Completed);
                    Assert.False(initial.AdministratorCreated);
                    Assert.False(initial.ClubCreated);

                    var admin = await bootstrap.CreateAdministratorAsync(
                        new CreateAdministratorInput("main-admin", "admin@example.com", "Str0ng!Pass1", "Main", "Administrator"));
                    Assert.True(admin.AdministratorCreated);
                    Assert.False(admin.Completed);

                    var club = await bootstrap.CreateClubAsync(new CreateClubInput("Main Country Club", "MCC", "ZAR", "South Africa"));
                    Assert.True(club.ClubCreated);
                    Assert.True(club.Completed);
                    Assert.NotNull(club.ClubId);

                    await Assert.ThrowsAsync<ConflictException>(() =>
                        bootstrap.CreateAdministratorAsync(new CreateAdministratorInput("other", "o@example.com", "Str0ng!Pass1", "O", "Admin")));
                    await Assert.ThrowsAsync<ConflictException>(() =>
                        bootstrap.CreateClubAsync(new CreateClubInput("Second Club", "SEC", "ZAR", "South Africa")));
                }
            }
            finally
            {
                await application.DisposeAsync();
            }
        }
        finally
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task Bootstrap_endpoints_are_forbidden_when_gate_is_disabled()
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
                ["App__EnableBootstrap"] = "false",
            });

            var application = await ApiBootstrapper.CreateApplicationAsync([]);
            try
            {
                await using var scope = application.Services.CreateAsyncScope();
                var bootstrap = scope.ServiceProvider.GetRequiredService<IBootstrapService>();

                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    bootstrap.CreateAdministratorAsync(new CreateAdministratorInput("main-admin", "admin@example.com", "Str0ng!Pass1", "Main", "Administrator")));
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    bootstrap.CreateClubAsync(new CreateClubInput("Main Country Club", "MCC", "ZAR", "South Africa")));
            }
            finally
            {
                await application.DisposeAsync();
            }
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