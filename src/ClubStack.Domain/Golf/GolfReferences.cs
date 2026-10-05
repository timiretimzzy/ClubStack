using ClubStack.Domain.Common;

namespace ClubStack.Domain.Golf;

/// <summary>
/// Golf booking business reference. Phase 3 §43 requires a
/// <c>BookingReference</c> distinct from the primary key, and Phase 8 §3.2 makes
/// human-readable references a first-class, human-readable form. The
/// <c>GBK-######</c> shape follows the existing six-digit module convention used
/// by the D2/D3/D4/D5/D6 references already in the platform.
/// </summary>
public static class GolfBookingReference
{
    public const string Prefix = "GBK";

    public const int Width = 6;
}

/// <summary>
/// Golf guest record business reference (Phase 3 §45 <c>GuestId</c> is the key;
/// the reference keeps the guest list human-readable and matches the Phase 5 §47
/// "Guests" screen).
/// </summary>
public static class GolfGuestReference
{
    public const string Prefix = "GST";

    public const int Width = 6;
}

/// <summary>Shared golf reference helpers.</summary>
public static class GolfReferenceHelpers
{
    public static string Format(string prefix, long sequence)
        => new BusinessReference(prefix, sequence).ToFormattedString(width: 6);

    /// <summary>
    /// True when a value matches <c>PREFIX-000000</c> for one of the golf prefixes.
    /// Enforced by a database CHECK constraint as well as here, so a reference
    /// cannot be written in an unexpected shape by any other writer.
    /// </summary>
    public static bool IsValidFormat(string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var expected = $"{prefix}-";
        if (!value.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = value[expected.Length..];
        return digits.Length == 6 && digits.All(char.IsAsciiDigit);
    }
}
