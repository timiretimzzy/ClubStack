using ClubStack.Application.Outbox;
using ClubStack.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class OutboxProcessingTests : DatabaseTestBase
{
    public OutboxProcessingTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Job_marks_unhandled_messages_as_processed()
    {
        var key = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");
        var eventTypes = new[] { $"Test.NoHandler.{key}.1", $"Test.NoHandler.{key}.2", $"Test.NoHandler.{key}.3" };

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            foreach (var eventType in eventTypes)
            {
                await store.EnqueueAsync(eventType, "{}", correlationId);
            }
        }

        await RunOutboxJobAsync(Services);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var rows = await context.OutboxMessages
            .AsNoTracking()
            .Where(e => e.EventType.StartsWith($"Test.NoHandler.{key}"))
            .ToListAsync();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(OutboxStatus.Processed, r.Status));
        Assert.All(rows, r => Assert.NotNull(r.ProcessedAtUtc));
        Assert.All(rows, r => Assert.Equal(0, r.RetryCount));
    }

    [Fact]
    public async Task Job_delivers_messages_to_a_registered_handler()
    {
        var key = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Capture.{key}";

        var handler = new CapturingOutboxHandler(eventType);
        await using var customProvider = TestServiceProvider.Build(
            Fixture.ConnectionString,
            services => services.AddSingleton<IOutboxMessageHandler>(handler));

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            await store.EnqueueAsync(eventType, $"{{\"key\":\"{key}\"}}", correlationId);
        }

        await RunOutboxJobAsync(customProvider);

        var captured = Assert.Single(handler.Received);
        Assert.Equal(eventType, captured.EventType);
        Assert.Equal(correlationId, captured.CorrelationId);
        Assert.Equal($"{{\"key\":\"{key}\"}}", captured.PayloadJson);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.OutboxMessages.SingleAsync(e => e.EventType == eventType);
        Assert.Equal(OutboxStatus.Processed, row.Status);
    }

    [Fact]
    public async Task Job_retries_a_failing_handler_and_fails_after_max_retries()
    {
        var key = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");
        var eventType = $"Test.Failure.{key}";

        var handler = new ThrowingOutboxHandler(eventType, "handler exploded");
        await using var customProvider = TestServiceProvider.Build(
            Fixture.ConnectionString,
            services => services.AddSingleton<IOutboxMessageHandler>(handler));

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            await store.EnqueueAsync(eventType, "{}", correlationId);
        }

        await RunOutboxJobAsync(customProvider, executions: 1);

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var afterFirst = await context.OutboxMessages.SingleAsync(e => e.EventType == eventType);
            Assert.Equal(OutboxStatus.Pending, afterFirst.Status);
            Assert.Equal(1, afterFirst.RetryCount);
        }

        // MaxRetryCount = 10; the first run already produced retry 1, so
        // nine more runs walk RetryCount to 9 and the tenth marks Failed.
        await RunOutboxJobAsync(customProvider, executions: 9);

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var final = await context.OutboxMessages.SingleAsync(e => e.EventType == eventType);
            Assert.Equal(OutboxStatus.Failed, final.Status);
            Assert.Equal(9, final.RetryCount);
            Assert.Equal("handler exploded", final.ErrorInfo);
        }
    }

private sealed class CapturingOutboxHandler : IOutboxMessageHandler
    {
        private readonly List<OutboxMessageData> _received = new();

        public CapturingOutboxHandler(string handledEventType)
        {
            HandledEventType = handledEventType;
        }

        public string HandledEventType { get; }

        public IReadOnlyList<OutboxMessageData> Received
        {
            get
            {
                lock (_received)
                {
                    return _received.ToArray();
                }
            }
        }

        public Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(message.EventType, HandledEventType, StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            lock (_received)
            {
                _received.Add(message);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingOutboxHandler : IOutboxMessageHandler
    {
        private readonly string _message;

        public ThrowingOutboxHandler(string handledEventType, string message)
        {
            HandledEventType = handledEventType;
            _message = message;
        }

        public string HandledEventType { get; }

        public Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
        {
            if (string.Equals(message.EventType, HandledEventType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(_message);
            }

            return Task.CompletedTask;
        }
    }
}