using ClubStack.Domain.Common;

namespace ClubStack.Domain.Membership;

/// <summary>
/// Authoritative membership-state transition rules (Phase 8 §7.1/§7.2/§7.3,
/// Phase 3 §12). Only the transitions drawn in the approved state machines are
/// valid; anything else is rejected (Phase 8 §7, Phase 2 MEM-032). No additional
/// states are introduced (Phase 9 §L636).
/// </summary>
public static class MembershipStateTransitions
{
    /// <summary>Phase 8 §7.2: Draft → Submitted → UnderReview → Approved → Activated; Submitted/UnderReview → Rejected.</summary>
    public static bool CanTransition(MembershipApplicationStatus from, MembershipApplicationStatus to)
    {
        return (from, to) switch
        {
            (MembershipApplicationStatus.Draft, MembershipApplicationStatus.Submitted) => true,
            (MembershipApplicationStatus.Submitted, MembershipApplicationStatus.UnderReview) => true,
            (MembershipApplicationStatus.Submitted, MembershipApplicationStatus.Rejected) => true,
            (MembershipApplicationStatus.UnderReview, MembershipApplicationStatus.Approved) => true,
            (MembershipApplicationStatus.UnderReview, MembershipApplicationStatus.Rejected) => true,
            (MembershipApplicationStatus.Approved, MembershipApplicationStatus.Activated) => true,
            _ => false,
        };
    }

    /// <summary>Phase 8 §7.1: Prospect → Active → Suspended → Active → Cancelled.</summary>
    public static bool CanTransition(MemberStatus from, MemberStatus to)
    {
        return (from, to) switch
        {
            (MemberStatus.Prospect, MemberStatus.Active) => true,
            (MemberStatus.Active, MemberStatus.Suspended) => true,
            (MemberStatus.Suspended, MemberStatus.Active) => true,
            (MemberStatus.Active, MemberStatus.Cancelled) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Phase 8 §7.3: Pending → Active → Suspended → Active → Expired → Active
    /// (renewed) → Cancelled. Transfer is a controlled historical transition.
    /// </summary>
    public static bool CanTransition(MembershipStatus from, MembershipStatus to)
    {
        return (from, to) switch
        {
            (MembershipStatus.Pending, MembershipStatus.Active) => true,
            (MembershipStatus.Active, MembershipStatus.Suspended) => true,
            (MembershipStatus.Suspended, MembershipStatus.Active) => true,
            (MembershipStatus.Active, MembershipStatus.Expired) => true,
            (MembershipStatus.Expired, MembershipStatus.Active) => true,
            (MembershipStatus.Active, MembershipStatus.Cancelled) => true,
            (MembershipStatus.Pending, MembershipStatus.Transferred) => true,
            (MembershipStatus.Active, MembershipStatus.Transferred) => true,
            _ => false,
        };
    }
}

/// <summary>
/// Member numbering (Phase 4 §17, Phase 2 MEM-002): human-readable reference
/// separate from the UUIDv7 primary key, formatted <c>MEM-000124</c>. Uniqueness
/// is additionally enforced by the database (Phase 4 database integrity rules).
/// </summary>
public static class MembershipNumber
{
    public const string Prefix = "MEM";

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
/// Pure fee proration for assessments created mid-period where the configured fee
/// enables proration (Phase 2 MEM-043). The amount is scaled by the fraction of the
/// period that remains on the assessment date, rounded to the money scale (4).
/// </summary>
public static class FeeProration
{
    public static decimal Apply(decimal amount, int periodDays, int remainingDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount, nameof(amount));
        if (periodDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodDays), "The period length must be positive.");
        }

        if (remainingDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingDays), "The remaining period cannot be negative.");
        }

        if (remainingDays >= periodDays)
        {
            return amount;
        }

        var prorated = amount * remainingDays / periodDays;
        return decimal.Round(prorated, 4, MidpointRounding.ToEven);
    }
}