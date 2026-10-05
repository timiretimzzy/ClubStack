namespace ClubStack.Domain.Common;

/// <summary>Marker interface applied to aggregates so repositories can enforce boundaries.</summary>
public interface IAggregateRoot
{
}

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}