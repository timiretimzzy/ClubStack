using ClubStack.Application.Outbox;
using Microsoft.Extensions.Logging;

namespace ClubStack.Infrastructure.Outbox;

/// <summary>Handles a specific outbox event type when one is registered.</summary>
public interface IOutboxMessageHandler
{
    string HandledEventType { get; }

    Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches outbox messages to registered handlers. D0 registers only a
/// logging handler; later slices register domain-specific handlers.
/// </summary>
public sealed class OutboxEventDispatcher : IEventDispatcher
{
    private readonly IEnumerable<IOutboxMessageHandler> _handlers;
    private readonly ILogger<OutboxEventDispatcher> _logger;

    public OutboxEventDispatcher(IEnumerable<IOutboxMessageHandler> handlers, ILogger<OutboxEventDispatcher> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
    {
        var handlers = _handlers.Where(h => string.Equals(h.HandledEventType, message.EventType, StringComparison.Ordinal)).ToList();

        if (handlers.Count == 0)
        {
            // Explicit no-op for unhandled events; delivery is still recorded as processed.
            _logger.LogInformation(
                "Outbox event {EventType} ({MessageId}) has no registered handler; delivered as no-op.",
                message.EventType,
                message.Id);
            return;
        }

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(message, cancellationToken);
        }
    }
}