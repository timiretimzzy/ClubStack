namespace ClubStack.Application.Correlation;

/// <summary>
/// Ambient access to the current request's correlation ID. Implementations are
/// scoped per request; background processing must propagate the ID explicitly.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
}

/// <summary>
/// Internal writer used by the API correlation middleware so ambient state is
/// only ever set by trusted infrastructure.
/// </summary>
public interface ICorrelationContextWriter
{
    void Set(string correlationId, bool isGenerated);
}

public sealed class CorrelationContextV1 : ICorrelationContext, ICorrelationContextWriter
{
    public string CorrelationId { get; private set; } = string.Empty;

    public bool IsGenerated { get; private set; }

    public void Set(string correlationId, bool isGenerated)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID must not be empty.", nameof(correlationId));
        }

        CorrelationId = correlationId;
        IsGenerated = isGenerated;
    }
}

/// <summary>Validates/normalizes incoming or newly generated correlation IDs.</summary>
public static class CorrelationIdPolicy
{
    private const int MaxLength = 128;

    public static bool TryNormalize(string? candidate, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        if (trimmed.Length > MaxLength || trimmed.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }
}