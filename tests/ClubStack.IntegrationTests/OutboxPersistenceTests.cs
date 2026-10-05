using ClubStack.Application.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class OutboxPersistenceTests : DatabaseTestBase
{
    public OutboxPersistenceTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task EnqueueAsync_persists_a_pending_message()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Enqueue.{correlationId}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            await store.EnqueueAsync(eventType, "{\"v\":1}", correlationId);
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.OutboxMessages.SingleAsync(e => e.EventType == eventType);

        Assert.Equal(OutboxStatus.Pending, row.Status);
        Assert.Equal(0, row.RetryCount);
        Assert.Equal("{\"v\":1}", row.PayloadJson);
        Assert.Equal(correlationId, row.CorrelationId);
    }

    [Fact]
    public async Task GetPendingAsync_returns_pending_messages_ordered_by_creation()
    {
        var key = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Pending.{key}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            for (var i = 0; i < 3; i++)
            {
                await store.EnqueueAsync($"{eventType}.{i}", $"{{{i}}}", Guid.NewGuid().ToString("N"));
            }

            var pending = await store.GetPendingAsync(100);

            var relevant = pending
                .Where(m => m.EventType.StartsWith(eventType, StringComparison.Ordinal))
                .OrderBy(m => m.CreatedAtUtc)
                .ToArray();

            Assert.Equal(3, relevant.Length);
            Assert.All(relevant, m => Assert.Equal(OutboxStatus.Pending, m.Status));
            Assert.Equal($"{eventType}.0", relevant[0].EventType);
            Assert.Equal($"{eventType}.2", relevant[2].EventType);
        }
    }

    [Fact]
    public async Task GetPendingAsync_honors_take_limit()
    {
        var key = Guid.NewGuid().ToString("N");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            for (var i = 0; i < 5; i++)
            {
                await store.EnqueueAsync($"Test.Take.{key}.{i}", "{}", Guid.NewGuid().ToString("N"));
            }

            var limited = await store.GetPendingAsync(2);

            Assert.Equal(2, limited.Count);
        }
    }

    [Fact]
    public async Task MarkProcessedAsync_marks_the_message_processed()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Processed.{correlationId}";
        var messageId = Guid.Empty;

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            await store.EnqueueAsync(eventType, "{}", correlationId);

            var pending = await store.GetPendingAsync(100);
            var message = pending.Single(m => m.EventType == eventType);
            messageId = message.Id;

            await store.MarkProcessedAsync(message.Id, DateTimeOffset.UtcNow);
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.OutboxMessages.SingleAsync(e => e.Id == messageId);

        Assert.Equal(OutboxStatus.Processed, row.Status);
        Assert.NotNull(row.ProcessedAtUtc);
    }

    [Fact]
    public async Task MarkFailedAsync_marks_the_message_failed()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Failed.{correlationId}";
        var messageId = Guid.Empty;

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            await store.EnqueueAsync(eventType, "{}", correlationId);

            var pending = await store.GetPendingAsync(100);
            var message = pending.Single(m => m.EventType == eventType);
            messageId = message.Id;

            await store.MarkFailedAsync(message.Id, "handler blew up");
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.OutboxMessages.SingleAsync(e => e.Id == messageId);

        Assert.Equal(OutboxStatus.Failed, row.Status);
        Assert.Equal("handler blew up", row.ErrorInfo);
    }

    [Fact]
    public async Task RecordRetryAsync_increments_the_retry_counter()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Retry.{correlationId}";
        var messageId = Guid.Empty;

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            await store.EnqueueAsync(eventType, "{}", correlationId);

            var pending = await store.GetPendingAsync(100);
            var message = pending.Single(m => m.EventType == eventType);
            messageId = message.Id;

            await store.RecordRetryAsync(message.Id);
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.OutboxMessages.SingleAsync(e => e.Id == messageId);

        Assert.Equal(1, row.RetryCount);
    }
}