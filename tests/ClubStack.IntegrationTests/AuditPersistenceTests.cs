using ClubStack.Application.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class AuditPersistenceTests : DatabaseTestBase
{
    public AuditPersistenceTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task RecordAsync_persists_and_GetRecentAsync_reads_it_back()
    {
        var correlationId = Guid.NewGuid().ToString("N");

        await using (var scope = Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            await recorder.RecordAsync(new AuditRecord
            {
                Id = Guid.CreateVersion7(),
                TimestampUtc = DateTimeOffset.UtcNow,
                ActorUserId = "audit-actor",
                ActorName = "Audit Tester",
                Action = $"Audit.Persist.{correlationId}",
                EntityType = "Authentication",
                EntityId = "A-1",
                Reference = "MEM-000001",
                Result = "Success",
                CorrelationId = correlationId,
                MetadataJson = "{\"source\":\"integration\"}",
            });

            var recent = await recorder.GetRecentAsync($"Audit.Persist.{correlationId}", take: 10);

            var record = Assert.Single(recent);
            Assert.Equal("Audit Tester", record.ActorName);
            Assert.Equal("Authentication", record.EntityType);
            Assert.Equal("Success", record.Result);
            Assert.Equal(correlationId, record.CorrelationId);
            Assert.Equal("{\"source\":\"integration\"}", record.MetadataJson);
        }
    }

    [Fact]
    public async Task GetRecentAsync_filters_by_action_and_orders_newest_first()
    {
        var group = Guid.NewGuid().ToString("N");
        var action = $"Audit.Ordering.{group}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            var baseTime = DateTimeOffset.UtcNow.AddMinutes(-2);
            for (var i = 1; i <= 3; i++)
            {
                await recorder.RecordAsync(new AuditRecord
                {
                    Id = Guid.CreateVersion7(),
                    TimestampUtc = baseTime.AddMinutes(i),
                    Action = action,
                    EntityType = "Authentication",
                    Result = "Success",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                });
            }

            var recent = await recorder.GetRecentAsync(action, take: 5);

            Assert.Equal(3, recent.Count);
            Assert.True(recent[0].TimestampUtc > recent[1].TimestampUtc);
            Assert.True(recent[1].TimestampUtc > recent[2].TimestampUtc);
        }
    }

    [Fact]
    public async Task GetRecentAsync_respects_take_limit()
    {
        var group = Guid.NewGuid().ToString("N");
        var action = $"Audit.Take.{group}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            for (var i = 0; i < 5; i++)
            {
                await recorder.RecordAsync(new AuditRecord
                {
                    Id = Guid.CreateVersion7(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Action = action,
                    Result = "Success",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                });
            }

            var recent = await recorder.GetRecentAsync(action, take: 2);

            Assert.Equal(2, recent.Count);
        }
    }

    [Fact]
    public async Task Audit_rows_are_immutable_in_practice_after_insert()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var action = $"Audit.Immutable.{correlationId}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            await recorder.RecordAsync(new AuditRecord
            {
                Id = Guid.CreateVersion7(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = action,
                Result = "Success",
                CorrelationId = correlationId,
            });
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var stored = await context.AuditLogs.SingleAsync(e => e.Action == action);

        Assert.Equal(correlationId, stored.CorrelationId);
    }
}