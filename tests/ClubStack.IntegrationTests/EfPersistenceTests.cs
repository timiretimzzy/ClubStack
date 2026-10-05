using ClubStack.Application.Outbox;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class EfPersistenceTests : DatabaseTestBase
{
    public EfPersistenceTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Audit_log_row_round_trips_through_ef()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var id = Guid.CreateVersion7();
        var timestamp = DateTimeOffset.UtcNow;

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.AuditLogs.Add(new AuditLogEntity
            {
                Id = id,
                TimestampUtc = timestamp,
                ActorUserId = "user-1",
                ActorName = "Integration Tester",
                Action = "Test.RoundTrip",
                EntityType = "Test",
                EntityId = "T-1",
                Reference = "REF-000001",
                Result = "Success",
                CorrelationId = correlationId,
                MetadataJson = "{\"key\":\"value\"}",
            });
            await context.SaveChangesAsync();
        }

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var row = await context.AuditLogs.AsNoTracking().SingleAsync(e => e.Id == id);

            // Postgres stores timestamptz with microsecond precision, so a
            // DateTimeOffset round trip may differ by sub-microsecond ticks.
            Assert.True(Math.Abs((row.TimestampUtc - timestamp).TotalMilliseconds) < 0.01);
            Assert.Equal("Integration Tester", row.ActorName);
            Assert.Equal("Test.RoundTrip", row.Action);
            Assert.Equal("REF-000001", row.Reference);
            Assert.Equal("{\"key\":\"value\"}", row.MetadataJson);
        }
    }

    [Fact]
    public async Task Reference_sequence_row_round_trips_through_ef()
    {
        var id = Guid.CreateVersion7();

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.ReferenceSequences.Add(new ReferenceSequenceEntity
            {
                Id = id,
                Prefix = "RT",
                LastNumber = 42,
                FormatWidth = 8,
            });
            await context.SaveChangesAsync();
        }

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var row = await context.ReferenceSequences.AsNoTracking().SingleAsync(e => e.Id == id);

            Assert.Equal("RT", row.Prefix);
            Assert.Equal(42, row.LastNumber);
            Assert.Equal(8, row.FormatWidth);
        }
    }

    [Fact]
    public async Task Idempotency_row_round_trips_through_ef()
    {
        var id = Guid.CreateVersion7();

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.IdempotencyRecords.Add(new IdempotencyRecordEntity
            {
                Id = id,
                IdempotencyKey = $"rt-{Guid.NewGuid():N}",
                Status = Application.Idempotency.IdempotencyStatus.InProgress,
                CorrelationId = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var row = await context.IdempotencyRecords.AsNoTracking().SingleAsync(e => e.Id == id);

            Assert.Equal(Application.Idempotency.IdempotencyStatus.InProgress, row.Status);
        }
    }

    [Fact]
    public async Task Outbox_row_round_trips_through_ef_including_enum()
    {
        var id = Guid.CreateVersion7();

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = id,
                EventId = Guid.CreateVersion7(),
                EventType = "Test.RoundTrip",
                PayloadJson = "{\"n\":1}",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Status = OutboxStatus.Pending,
                RetryCount = 3,
                CorrelationId = Guid.NewGuid().ToString("N"),
            });
            await context.SaveChangesAsync();
        }

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var row = await context.OutboxMessages.AsNoTracking().SingleAsync(e => e.Id == id);

            Assert.Equal(OutboxStatus.Pending, row.Status);
            Assert.Equal(3, row.RetryCount);
        }
    }
}