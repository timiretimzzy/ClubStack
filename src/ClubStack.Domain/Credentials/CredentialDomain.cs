using ClubStack.Domain.Common;

namespace ClubStack.Domain.Credentials;

/// <summary>
/// Authoritative credential state-transition rules (Phase 8 §7.4). Only the edges
/// drawn in the approved state machine are valid; anything else is rejected.
/// Lost/stolen credentials can only exit through replacement; nothing may
/// silently reactivate a lost or stolen credential.
/// </summary>
public static class CredentialStateTransitions
{
    public static bool CanTransition(CredentialStatus from, CredentialStatus to)
    {
        return (from, to) switch
        {
            (CredentialStatus.Issued, CredentialStatus.Active) => true,
            (CredentialStatus.Active, CredentialStatus.Suspended) => true,
            (CredentialStatus.Suspended, CredentialStatus.Active) => true,
            (CredentialStatus.Active, CredentialStatus.Lost) => true,
            (CredentialStatus.Active, CredentialStatus.Stolen) => true,
            (CredentialStatus.Active, CredentialStatus.Expired) => true,
            (CredentialStatus.Lost, CredentialStatus.Replaced) => true,
            (CredentialStatus.Stolen, CredentialStatus.Replaced) => true,
            _ => false,
        };
    }

    /// <summary>
    /// An Active credential whose expiry timestamp has passed cannot continue to
    /// be accepted (CRED-010); it must be moved to <see cref="CredentialStatus.Expired"/>.
    /// </summary>
    public static bool IsExpiredDue(CredentialStatus status, DateTimeOffset? expiresAtUtc, DateTimeOffset utcNow)
    {
        return status == CredentialStatus.Active && expiresAtUtc is not null && expiresAtUtc <= utcNow;
    }
}

/// <summary>
/// Credential numbering (Phase 4 §17): human-readable reference <c>CRD-000124</c>,
/// separate from the UUIDv7 primary key, produced by the reference generator and
/// formatted through <see cref="BusinessReference"/>.
/// </summary>
public static class CredentialNumber
{
    public const string Prefix = "CRD";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separatorIndex = value.IndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        if (!string.Equals(value[..separatorIndex], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = value[(separatorIndex + 1)..];
        return digits.Length == Width && digits.All(char.IsAsciiDigit);
    }
}

/// <summary>
/// Minimum card identifier stored for credential resolution (Phase 9 magnetic
/// stripe, CRED-003). Raw track data must not be persisted; only the minimum
/// identifier required to look up the credential is retained.
/// </summary>
public static class CardIdentifier
{
    public const int MinLength = 6;

    public const int MaxLength = 64;

    /// <summary>
    /// Normalizes a raw read to the stored minimum identifier. Trims surrounding
    /// whitespace and uppercases so the same card always resolves to one value.
    /// </summary>
    public static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length == 0 ? normalized : normalized.ToUpperInvariant();
    }

    /// <summary>
    /// Format validation performed before a read value is used for lookup or a
    /// credential is issued (Phase 4 §13: receives identifier, validates format).
    /// Alphanumeric identifiers with optional '-'/'_' separators are accepted.
    /// </summary>
    public static bool IsValidFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Normalize(value);
        if (normalized.Length is < MinLength or > MaxLength)
        {
            return false;
        }

        return normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }
}