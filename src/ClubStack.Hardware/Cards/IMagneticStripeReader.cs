namespace ClubStack.Hardware.Cards;

/// <summary>
/// Normalized result produced by a card reader (Phase 4 §13, Phase 9 magnetic
/// stripe). The reader is an input device, never a source of business logic;
/// it returns an identifier that the application then resolves.
/// </summary>
public sealed record CardReadResult
{
    public bool IsSuccess { get; init; }

    /// <summary>Raw data as read from the device. Must not be persisted (Phase 9).</summary>
    public string? RawData { get; init; }

    /// <summary>The minimum identifier required for credential resolution.</summary>
    public string? Identifier { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Abstraction over membership-card hardware (CRED-002, Phase 4 §13). D3 wires
/// against this seam; business modules must never own device-driver code
/// (D2 Market Reconciliation §13).
/// </summary>
public interface IMagneticStripeReader
{
    Task<CardReadResult> ReadAsync(CancellationToken cancellationToken = default);
}