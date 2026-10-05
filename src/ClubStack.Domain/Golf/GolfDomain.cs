using ClubStack.Domain.Common;

namespace ClubStack.Domain.Golf;

/// <summary>
/// Golf business rules. Everything here is derived from the authoritative
/// specification set; nothing in this file invents club policy.
/// </summary>
public static class GolfDomain
{
    /// <summary>
    /// GOLF-004: "Tee slots MUST support configured capacity." Phase 3 §42 keeps
    /// capacity on the slot, and the Phase 5 §48 tee-sheet example expresses
    /// availability in player places ("07:00 … 2 open"), so capacity counts
    /// players rather than bookings.
    /// </summary>
    public const int MaxSlotCapacity = 24;

    public const int MinSlotCapacity = 1;

    public const int MaxPlayersPerBooking = 12;

    /// <summary>
    /// Normalizes an amount to the platform's fixed (19,4) money scale, matching
    /// <see cref="Money"/>'s rounding so a fee snapshotted onto a booking player
    /// re-adds exactly to the booking total.
    /// </summary>
    public static decimal NormalizeAmount(decimal amount)
        => decimal.Round(amount, 4, MidpointRounding.ToEven);

    /// <summary>
    /// The complete set of legal booking transitions, exactly as Phase 8 §7.7
    /// draws them. GOLF-021 requires invalid transitions to be rejected, so this
    /// table is the single source of truth for the booking lifecycle rather than
    /// a set of scattered <c>if</c> checks.
    ///
    /// Note what is deliberately absent: there is no Draft → Cancelled edge,
    /// because §7.7 does not define one. Adding it would be inventing policy.
    /// </summary>
    public static bool IsValidTransition(GolfBookingStatus from, GolfBookingStatus to) => (from, to) switch
    {
        (GolfBookingStatus.Draft, GolfBookingStatus.Confirmed) => true,
        (GolfBookingStatus.Confirmed, GolfBookingStatus.CheckedIn) => true,
        (GolfBookingStatus.CheckedIn, GolfBookingStatus.Completed) => true,
        (GolfBookingStatus.Confirmed, GolfBookingStatus.Cancelled) => true,
        (GolfBookingStatus.Confirmed, GolfBookingStatus.NoShow) => true,
        _ => false,
    };

    /// <summary>
    /// True when a booking in this state still holds tee-slot capacity.
    ///
    /// <para>GOLF-010 requires availability to be validated "before confirming a
    /// booking" and GOLF-011 requires overbooking to be prevented, so only a
    /// confirmed (or later) booking consumes capacity: a Draft hold does not
    /// block a slot, and confirming re-validates against the live count.</para>
    /// </summary>
    public static bool ConsumesCapacity(GolfBookingStatus status)
        => status is GolfBookingStatus.Confirmed
            or GolfBookingStatus.CheckedIn
            or GolfBookingStatus.Completed
            or GolfBookingStatus.NoShow;

    /// <summary>
    /// True when a booking in this state can still be cancelled. Phase 8 §7.7
    /// allows Confirmed → Cancelled only.
    /// </summary>
    public static bool IsCancellable(GolfBookingStatus status) => status == GolfBookingStatus.Confirmed;

    /// <summary>
    /// The per-player status a booking transition imposes on its players. A
    /// cancellation marks every player cancelled; the other transitions mark the
    /// booked players so a no-show is attributable per player (Phase 3 §44).
    /// </summary>
    public static GolfBookingPlayerStatus PlayerStatusFor(GolfBookingStatus bookingStatus) => bookingStatus switch
    {
        GolfBookingStatus.Confirmed => GolfBookingPlayerStatus.Booked,
        GolfBookingStatus.CheckedIn => GolfBookingPlayerStatus.CheckedIn,
        GolfBookingStatus.Completed => GolfBookingPlayerStatus.CheckedIn,
        GolfBookingStatus.NoShow => GolfBookingPlayerStatus.NoShow,
        GolfBookingStatus.Cancelled => GolfBookingPlayerStatus.Cancelled,
        _ => GolfBookingPlayerStatus.Booked,
    };

    /// <summary>
    /// GOLF-012: "Booking rules MUST support configured member/guest/time
    /// restrictions." The three named dimensions are evaluated here against the
    /// configuration carried on the tee (Phase 3 §41 <c>Rules</c>) and the slot
    /// (Phase 3 §42 <c>Restrictions</c>).
    ///
    /// <paramref name="memberCount"/> and <paramref name="guestCount"/> are the
    /// player composition being validated; a zero count for a dimension the club
    /// has restricted is the violation.
    /// </summary>
    public static string? ValidateRestrictions(
        bool membersOnly,
        bool guestsAllowed,
        int? minimumPlayers,
        int? maximumPlayers,
        int playerCount,
        int guestCount)
    {
        if (playerCount < 1)
        {
            return "A booking must contain at least one player.";
        }

        if (playerCount > MaxPlayersPerBooking)
        {
            return $"A booking cannot contain more than {MaxPlayersPerBooking} players.";
        }

        if (membersOnly && guestCount > 0)
        {
            return "This tee is configured for members only, so the booking cannot contain guests.";
        }

        if (!guestsAllowed && guestCount > 0)
        {
            return "This tee is configured not to accept guests.";
        }

        if (minimumPlayers is { } minimum && playerCount < minimum)
        {
            return $"This tee requires at least {minimum} player(s); {playerCount} were supplied.";
        }

        if (maximumPlayers is { } maximum && playerCount > maximum)
        {
            return $"This tee allows at most {maximum} player(s); {playerCount} were supplied.";
        }

        return null;
    }

    /// <summary>
    /// GOLF-011 / Phase 9 §12: the system must prevent normal overbooking. Returns
    /// the number of player places still bookable, and the overload amount when
    /// the request exceeds the remaining capacity (zero when it fits).
    /// </summary>
    public static (int Open, int Overload) RemainingCapacity(int configuredCapacity, int takenPlayers, int requestedPlayers)
    {
        var open = Math.Max(0, configuredCapacity - takenPlayers);
        var overload = Math.Max(0, requestedPlayers - open);
        return (open, overload);
    }

    /// <summary>
    /// The operational closure/blocking window Phase 9 §12 lists as an
    /// availability input. A slot whose status is <see cref="TeeSlotStatus.Blocked"/>
    /// is never bookable regardless of remaining capacity.
    /// </summary>
    public static bool IsSlotBookable(TeeSlotStatus status) => status == TeeSlotStatus.Open;
}
