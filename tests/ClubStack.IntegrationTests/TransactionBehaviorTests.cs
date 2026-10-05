using ClubStack.Application.Auditing;
using ClubStack.Application.Outbox;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class TransactionBehaviorTests : DatabaseTestBase
{
    public TransactionBehaviorTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Commit_persists_all_writes_together()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Tx.Commit.{correlationId}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            await unitOfWork.BeginAsync();

            await outbox.EnqueueAsync(eventType, "{\"phase\":\"commit\"}", correlationId);
            await audit.RecordAsync(new AuditRecord
            {
                Id = Guid.CreateVersion7(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "Tx.Commit",
                Result = "Success",
                CorrelationId = correlationId,
            });

            await unitOfWork.CommitAsync();
        }

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.True(await verify.OutboxMessages.AnyAsync(e => e.EventType == eventType));
        Assert.True(await verify.AuditLogs.AnyAsync(e => e.Action == "Tx.Commit" && e.CorrelationId == correlationId));
    }

    [Fact]
    public async Task Rollback_discards_all_writes()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Tx.Rollback.{correlationId}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            await unitOfWork.BeginAsync();

            await outbox.EnqueueAsync(eventType, "{}", correlationId);
            await audit.RecordAsync(new AuditRecord
            {
                Id = Guid.CreateVersion7(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "Tx.Rollback",
                Result = "Success",
                CorrelationId = correlationId,
            });

            await unitOfWork.RollbackAsync();
        }

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.False(await verify.OutboxMessages.AnyAsync(e => e.EventType == eventType));
        Assert.False(await verify.AuditLogs.AnyAsync(e => e.Action == "Tx.Rollback" && e.CorrelationId == correlationId));
    }

    [Fact]
    public async Task Enqueue_without_unit_of_work_persists_immediately()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Tx.Autocommit.{correlationId}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            await store.EnqueueAsync(eventType, "{}", correlationId);
        }

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.True(await verify.OutboxMessages.AnyAsync(e => e.EventType == eventType));
    }

    [Fact]
    public async Task Begin_twice_without_commit_throws_and_writes_are_discarded()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Tx.DoubleBegin.{correlationId}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            await unitOfWork.BeginAsync();

            await outbox.EnqueueAsync(eventType, "{}", correlationId);

            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.BeginAsync());
        }

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.False(await verify.OutboxMessages.AnyAsync(e => e.EventType == eventType));
    }
}