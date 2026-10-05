namespace ClubStack.Domain.Common;

/// <summary>
/// A human-readable business reference (e.g. MEM-000001, SALE-2026-000001).
/// References are separate from primary (UUIDv7) identifiers and are produced by
/// the reference-generation foundation. Equals is value-based.
/// </summary>
public readonly record struct BusinessReference
{
    public BusinessReference(string prefix, long sequence)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("A business reference requires a prefix.", nameof(prefix));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A business reference sequence must be positive.");
        }

        Prefix = prefix.Trim().ToUpperInvariant();
        Sequence = sequence;
    }

    public string Prefix { get; }

    public long Sequence { get; }

    /// <summary>Formats the reference using the specification's zero-padded width.</summary>
    public string ToFormattedString(int width = 6)
    {
        if (width < 1)
        {
            width = 1;
        }

        return $"{Prefix}-{Sequence.ToString().PadLeft(width, '0')}";
    }

    public override string ToString() => ToFormattedString();

    public static BusinessReference Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Reference cannot be null or empty.", nameof(value));
        }

        var separatorIndex = value.IndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            throw new FormatException($"'{value}' is not a valid business reference.");
        }

        var prefix = value[..separatorIndex];
        if (!long.TryParse(value[(separatorIndex + 1)..], out var sequence) || sequence < 1)
        {
            throw new FormatException($"'{value}' is not a valid business reference.");
        }

        return new BusinessReference(prefix, sequence);
    }
}