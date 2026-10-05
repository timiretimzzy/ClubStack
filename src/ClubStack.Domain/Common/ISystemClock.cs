namespace ClubStack.Domain.Common;

/// <summary>
/// Abstraction over the current UTC time so domain/application logic can be
/// tested deterministically. Implementations must return UTC timestamps.
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}