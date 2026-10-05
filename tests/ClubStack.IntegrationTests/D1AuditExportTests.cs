using System.Text;
using ClubStack.Api;
using ClubStack.Application.Auditing;
using ClubStack.Application.Identity;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// API-level (HTTP) verification of the audit CSV export surface
/// (Phase 8 §8.2 audit / D1): content type, headers, BOM, records,
/// filtering/paging, unauthorized access, empty results and correct
/// CSV escaping/quoting. Uses the real <see cref="Api.Program"/> pipeline
/// via WebApplicationFactory so cookies, sessions and permission policies
/// behave exactly as in production.
/// </summary>
public sealed class D1AuditExportTests : IAsyncLifetime
{
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        (_connectionString, _databaseName) = await PostgreSqlTestFixture.CreateScratchDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_databaseName is not null && _databaseName.Length > 0)
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task AuditExport_end_to_end_csv_behavior()
    {
        const string userName = "audit-exporter";
        const string password = "Str0ng!Pass12";
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // For minimal-hosting apps, test-host ConfigureAppConfiguration is NOT
                    // reliably honoured. The deterministic injection point is DI: swap the
                    // singleton DbContextOptions so every EF consumer (DbContext AND the
                    // singleton IDbContextFactory) targets the hermetic scratch database.
                    var optionsDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ClubStackDbContext>));
                    if (optionsDescriptor is not null)
                    {
                        services.Remove(optionsDescriptor);
                    }

                    services.AddSingleton<DbContextOptions<ClubStackDbContext>>(_ =>
                    {
                        var builder = new DbContextOptionsBuilder<ClubStackDbContext>();
                        builder.UseNpgsql(_connectionString);
                        return builder.Options;
                    });

                    // Tests transport plain HTTP; the production cookie is Secure=Always,
                    // which .NET clients refuse to echo over http. SameAsRequest is a
                    // test-host-only relaxation and does not change production behaviour.
                    services.PostConfigure<CookieAuthenticationOptions>(
                        IdentityConstants.ApplicationScheme,
                        options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
                });
            });

        try
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });

            var actionTag = $"Audit.Export.{Guid.NewGuid():N}";
            var otherActionTag = $"Audit.Export.Other.{Guid.NewGuid():N}";
            var exporterId = Guid.CreateVersion7().ToString();

            // Ensure schema + reference data deterministically (the API bootstrapper also
            // seeds on startup, but it swallows failures when StopOnMigrationFailure=false).
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                await provider.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
                await provider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();
            }

            // Seed an authenticated System Administrator.
            using (var scope = factory.Services.CreateScope())
            {
                var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
                await identity.CreateUserAsync(new CreateUserInput(
                    userName, $"{userName}@example.com", "Audit", "Exporter", "0000000000",
                    password, ["System Administrator"]));
            }

            // Deterministic audit records: one plain, one with CSV-hostile content.
            using (var scope = factory.Services.CreateScope())
            {
                var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

                await recorder.RecordAsync(new AuditRecord
                {
                    Id = Guid.CreateVersion7(),
                    TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                    ActorUserId = exporterId,
                    ActorName = "Audit One",
                    Action = actionTag,
                    EntityType = "Payment",
                    EntityId = "e-1",
                    Reference = "REF-1",
                    Result = "Success",
                    CorrelationId = "corr-1",
                });

                await recorder.RecordAsync(new AuditRecord
                {
                    Id = Guid.CreateVersion7(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    ActorUserId = exporterId,
                    ActorName = "Doe, \"J\" \n Jr",
                    Action = actionTag,
                    EntityType = "Risk,Review",
                    EntityId = "e\"2",
                    Reference = "REF-2",
                    Result = string.Empty,
                    CorrelationId = "corr-2",
                });

                await recorder.RecordAsync(new AuditRecord
                {
                    Id = Guid.CreateVersion7(),
                    TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                    ActorUserId = "other-actor",
                    ActorName = "Other Guy",
                    Action = otherActionTag,
                    EntityType = "Payment",
                    EntityId = "e-3",
                    Reference = "REF-3",
                    Result = "Success",
                    CorrelationId = "corr-3",
                });
            }

            // ---- Unauthorized: anonymous request must be rejected ----
            var anonymous = await client.GetAsync("/api/v1/audit/export");
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);

            // ---- Authorized login establishes an audited, cookie-based session ----
            var login = await client.PostAsync("/api/v1/auth/login",
                new StringContent(
                    $"{{\"userName\":\"{userName}\",\"password\":\"{password}\"}}",
                    System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);

            // ---- Full export: content type, headers, BOM, records, escaping ----
            var export = await client.GetAsync("/api/v1/audit/export");
            Assert.Equal(System.Net.HttpStatusCode.OK, export.StatusCode);
            Assert.StartsWith("text/csv", export.Content.Headers.ContentType?.ToString());
            var disposition = export.Content.Headers.ContentDisposition;
            Assert.Equal("attachment", disposition?.DispositionType);
            Assert.NotNull(disposition?.FileName);
            Assert.StartsWith("clubstack-audit-", disposition!.FileName);

            var bytes = await export.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length >= 3, "CSV export should carry a UTF-8 BOM.");
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

            var body = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
                .Replace("\r\n", "\n", StringComparison.Ordinal);

            Assert.StartsWith("id,timestamp_utc,actor_user_id,actor_name,action,entity_type,entity_id,reference,result,correlation_id\n", body, StringComparison.Ordinal);
            Assert.Contains("Audit One", body, StringComparison.Ordinal);
            Assert.Contains("REF-1", body, StringComparison.Ordinal);
            Assert.Contains("Other Guy", body, StringComparison.Ordinal);

            // Escaping/quoting: comma + quote + newline cell is quoted with doubled quotes.
            Assert.Contains("\"Doe, \"\"J\"\" ", body, StringComparison.Ordinal);
            // Comma inside a cell is quoted, never emitted raw.
            Assert.Contains("\"Risk,Review\"", body, StringComparison.Ordinal);
            Assert.DoesNotContain(",Risk,Review,", body, StringComparison.Ordinal);
            // Embedded quote in entity id is doubled inside a quoted cell.
            Assert.Contains("\"e\"\"2\"", body, StringComparison.Ordinal);
            // Empty result field yields an empty unquoted cell (double comma).
            Assert.Contains(",REF-2,,corr-2", body, StringComparison.Ordinal);

            // ---- Filtering by action ----
            var byAction = await client.GetAsync($"/api/v1/audit/export?action={Uri.EscapeDataString(actionTag)}");
            Assert.Equal(System.Net.HttpStatusCode.OK, byAction.StatusCode);
            var actionBody = (await byAction.Content.ReadAsStringAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("Audit One", actionBody, StringComparison.Ordinal);
            Assert.Contains("\"Doe, \"\"J\"\" ", actionBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Other Guy", actionBody, StringComparison.Ordinal);
            Assert.DoesNotContain("REF-3", actionBody, StringComparison.Ordinal);

            // ---- Filtering by actor ----
            var byActor = await client.GetAsync($"/api/v1/audit/export?actorUserId={exporterId}");
            Assert.Equal(System.Net.HttpStatusCode.OK, byActor.StatusCode);
            var actorBody = (await byActor.Content.ReadAsStringAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("Audit One", actorBody, StringComparison.Ordinal);
            Assert.Contains("REF-2", actorBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Other Guy", actorBody, StringComparison.Ordinal);

            // ---- Paging parameter is honoured (newest first) ----
            var paged = await client.GetAsync($"/api/v1/audit/export?action={Uri.EscapeDataString(actionTag)}&pageSize=1");
            Assert.Equal(System.Net.HttpStatusCode.OK, paged.StatusCode);
            var pagedBody = (await paged.Content.ReadAsStringAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("\"Doe, \"\"J\"\" ", pagedBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Audit One", pagedBody, StringComparison.Ordinal);

            // ---- Empty result: header row only, no data records ----
            var empty = await client.GetAsync("/api/v1/audit/export?action=Does.Not.Exist.");
            Assert.Equal(System.Net.HttpStatusCode.OK, empty.StatusCode);
            var emptyBody = (await empty.Content.ReadAsStringAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.StartsWith("id,timestamp_utc,actor_user_id,actor_name,action,entity_type,entity_id,reference,result,correlation_id\n", emptyBody, StringComparison.Ordinal);
            Assert.EndsWith("correlation_id\n", emptyBody, StringComparison.Ordinal);
            Assert.Equal(1, emptyBody.Count(c => c == '\n'));
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }
}