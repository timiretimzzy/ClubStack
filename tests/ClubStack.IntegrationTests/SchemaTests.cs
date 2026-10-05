using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class SchemaTests : DatabaseTestBase
{
    public SchemaTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Expected_tables_exist_in_public_schema()
    {
        const string query = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name
            """;

        var tables = await ReadStringsAsync(query);

        Assert.Contains("__EFMigrationsHistory", tables);
        Assert.Contains("AspNetRoles", tables);
        Assert.Contains("AspNetUsers", tables);
        Assert.Contains("AspNetRoleClaims", tables);
        Assert.Contains("AspNetUserClaims", tables);
        Assert.Contains("AspNetUserLogins", tables);
        Assert.Contains("AspNetUserRoles", tables);
        Assert.Contains("AspNetUserTokens", tables);
        Assert.Contains("audit_logs", tables);
        Assert.Contains("idempotency_records", tables);
        Assert.Contains("outbox_messages", tables);
        Assert.Contains("reference_sequences", tables);
    }

    [Fact]
    public async Task Foundation_indexes_exist_with_expected_uniqueness()
    {
        const string query = """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
            """;

        var (indexes, definitions) = await ReadIndexesAsync(query);

        foreach (var index in indexes)
        {
            var definition = definitions[index];
            switch (index)
            {
                case "IX_reference_sequences_Prefix":
                    Assert.Contains("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
                    break;
                case "IX_idempotency_records_IdempotencyKey_Operation_ActorUserId":
                    Assert.Contains("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
                    break;
                case "IX_outbox_messages_Status_CreatedAtUtc":
                    Assert.DoesNotContain("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("Status", definition, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("CreatedAtUtc", definition, StringComparison.OrdinalIgnoreCase);
                    break;
                case "IX_audit_logs_Action":
                case "IX_audit_logs_CorrelationId":
                case "IX_audit_logs_TimestampUtc":
                    Assert.DoesNotContain("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        Assert.Contains("IX_reference_sequences_Prefix", indexes);
        Assert.Contains("IX_idempotency_records_IdempotencyKey_Operation_ActorUserId", indexes);
        Assert.Contains("IX_outbox_messages_Status_CreatedAtUtc", indexes);
        Assert.Contains("IX_audit_logs_Action", indexes);
        Assert.Contains("IX_audit_logs_CorrelationId", indexes);
        Assert.Contains("IX_audit_logs_TimestampUtc", indexes);
    }

    [Fact]
    public async Task Enum_columns_are_stored_as_integers()
    {
        const string query = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'outbox_messages'
              AND column_name IN ('Status', 'RetryCount')
            ORDER BY column_name
            """;

        var (columns, types) = await ReadColumnsAsync(query);

        Assert.Equal("integer", types[columns.IndexOf("Status")]);
        Assert.Equal("integer", types[columns.IndexOf("RetryCount")]);
    }

    [Fact]
    public async Task Migration_history_records_the_applied_migration()
    {
        const string query = """
            SELECT "MigrationId", "ProductVersion"
            FROM "__EFMigrationsHistory"
            ORDER BY "MigrationId"
            """;

        var (migrationIds, versions) = await ReadMigrationHistoryAsync(query);

        Assert.Contains("20260923171639_InitialD0", migrationIds);
        Assert.Equal("10.0.12", versions[0]);
    }

    [Fact]
    public void Concurrency_tokens_are_configured_on_foundation_tables()
    {
        using var context = new ClubStackDbContext(CreateScopedOptions());

        AssertConcurrencyToken<ReferenceSequenceEntity>(context);
        AssertConcurrencyToken<IdempotencyRecordEntity>(context);
        AssertConcurrencyToken<OutboxMessageEntity>(context);

        // Audit is append-oriented and intentionally has no concurrency token.
        Assert.False(HasConcurrencyToken<AuditLogEntity>(context));
    }

    private static void AssertConcurrencyToken<TEntity>(ClubStackDbContext context)
        where TEntity : class
    {
        Assert.True(HasConcurrencyToken<TEntity>(context), $"{typeof(TEntity).Name} must have an xmin concurrency token.");
    }

    private static bool HasConcurrencyToken<TEntity>(ClubStackDbContext context)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        var property = entityType.FindProperty("xmin");
        if (property is null)
        {
            return false;
        }

        return property.IsConcurrencyToken;
    }

    private DbContextOptions<ClubStackDbContext> CreateScopedOptions()
    {
        var options = new DbContextOptionsBuilder<ClubStackDbContext>()
            .UseNpgsql(Fixture.ConnectionString)
            .Options;

        return options;
    }

    private async Task<List<string>> ReadStringsAsync(string sql)
    {
        var values = new List<string>();
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private async Task<(List<string> Indexes, Dictionary<string, string> Definitions)> ReadIndexesAsync(string sql)
    {
        var indexes = new List<string>();
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
            definitions[reader.GetString(0)] = reader.GetString(1);
        }

        return (indexes, definitions);
    }

    private async Task<(List<string> Columns, List<string> Types)> ReadColumnsAsync(string sql)
    {
        var columns = new List<string>();
        var types = new List<string>();
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
            types.Add(reader.GetString(1));
        }

        return (columns, types);
    }

    private async Task<(List<string> MigrationIds, List<string> Versions)> ReadMigrationHistoryAsync(string sql)
    {
        var migrationIds = new List<string>();
        var versions = new List<string>();
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            migrationIds.Add(reader.GetString(0));
            versions.Add(reader.GetString(1));
        }

        return (migrationIds, versions);
    }
}