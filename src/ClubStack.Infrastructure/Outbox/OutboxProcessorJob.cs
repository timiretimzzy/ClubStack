using ClubStack.Application.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ClubStack.Infrastructure.Outbox;

/// <summary>
/// Quartz job that drains the outbox. Handlers are resolved from a scope so the
/// job can be hosted by the Windows service or any background runtime.
/// </summary>
[DisallowConcurrentExecution]
public sealed class OutboxProcessorJob : IJob
{
    private const int MaxRetryCount = 10;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorJob> _logger;

    public OutboxProcessorJob(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var scope = _scopeFactory.CreateScope();

        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        // Bounded batch per tick; the scheduler repeatedly invokes the job.
        var messages = await outbox.GetPendingAsync(100, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await dispatcher.DispatchAsync(message, cancellationToken);
                await outbox.MarkProcessedAsync(message.Id, DateTimeOffset.UtcNow, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Outbox message {MessageId} ({EventType}) failed to dispatch on attempt {Attempt}.",
                    message.Id,
                    message.EventType,
                    message.RetryCount + 1);

                if (message.RetryCount + 1 >= MaxRetryCount)
                {
                    await outbox.MarkFailedAsync(message.Id, exception.Message, cancellationToken);
                }
                else
                {
                    await outbox.RecordRetryAsync(message.Id, cancellationToken);
                }
            }
        }
    }
}