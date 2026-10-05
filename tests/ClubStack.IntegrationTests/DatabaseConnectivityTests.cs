using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class DatabaseConnectivityTests : DatabaseTestBase
{
    public DatabaseConnectivityTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Raw_connection_reaches_postgres_and_executes_query()
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1L, Convert.ToInt64(result));
    }

    [Fact]
    public async Task EFCore_reports_connectable_database()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();

        Assert.True(await context.Database.CanConnectAsync());
    }
}