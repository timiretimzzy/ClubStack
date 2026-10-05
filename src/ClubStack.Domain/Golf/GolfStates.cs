namespace ClubStack.Domain.Golf;

/// <summary>
/// Golf course lifecycle (Phase 3 §40 Course.Status). A course is inactivated
/// rather than deleted so historic bookings keep a valid course reference
/// (Phase 3 §79 reference integrity).
/// </summary>
public enum GolfCourseStatus
{
    Active = 0,
    Inactive = 1,
}

/// <summary>
/// Tee configuration lifecycle (Phase 3 §41 Tee.Status). Phase 3 §41 defines a
/// tee as "a playable tee/resource configuration" carrying its own capacity and
/// rules.
/// </summary>
public enum GolfTeeStatus
{
    Active = 0,
    Inactive = 1,
}

/// <summary>
/// Tee slot lifecycle (Phase 3 §42 TeeSlot.Status).
///
/// <para><see cref="Open"/> is bookable. <see cref="Blocked"/> is the operational
/// closure/blocking that Phase 9 §12 lists as an availability input and that
/// GOLF-002 ("courses MUST support closures/unavailability periods") is
/// expressed through: a course is unavailable for a period because its slots in
/// that period are blocked. Blocking is an operational action gated on the
/// sensitive <c>Golf.TeeSlot.Manage</c> permission, which is why closure is a
/// slot state rather than a separate entity.</para>
/// </summary>
public enum TeeSlotStatus
{
    Open = 0,
    Blocked = 1,
}

/// <summary>
/// Golf booking lifecycle. The values are exactly the Phase 8 §7.7 state
/// machine, which GOLF-020 restates and GOLF-021 makes enforceable:
/// Draft → Confirmed → CheckedIn → Completed, plus the alternative
/// Confirmed → Cancelled and Confirmed → NoShow.
/// </summary>
public enum GolfBookingStatus
{
    Draft = 0,
    Confirmed = 1,
    CheckedIn = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5,
}

/// <summary>
/// A player on a booking. Phase 3 §44 stores <c>PersonId</c> nullable "for
/// visitor" and a nullable <c>GuestId</c>, and states that "a booking may
/// contain both members and guests"; this enum is the discriminator between the
/// two cases so a guest can never be silently recorded as a member.
/// </summary>
public enum GolfPlayerType
{
    /// <summary>An existing ClubStack member (D2 <c>Member</c>).</summary>
    Member = 0,

    /// <summary>A non-member visitor recorded in <c>golf_guest_records</c> (GOLF-009).</summary>
    Guest = 1,
}

/// <summary>
/// Per-player booking status (Phase 3 §44 GolfBookingPlayer.Status). It mirrors the
/// parent booking's outcome for the player concerned, so a no-show or a
/// cancellation is attributable to an individual player rather than only to the
/// group.
/// </summary>
public enum GolfBookingPlayerStatus
{
    Booked = 0,
    CheckedIn = 1,
    NoShow = 2,
    Cancelled = 3,
}

/// <summary>
/// Golf guest record lifecycle (Phase 3 §45 Guest.Status). Guests are explicitly
/// "separate from membership" and "can participate in golf without becoming a
/// member" (GOLF-008/GOLF-009), so this is a golf-owned record, not a second
/// member master.
/// </summary>
public enum GolfGuestStatus
{
    Active = 0,
    Inactive = 1,
}

/// <summary>
/// What a <c>golf_charges</c> row represents (Phase 8 §8.9, Phase 3 §75).
/// </summary>
public enum GolfChargeType
{
    /// <summary>
    /// The per-player green fee resolved from the tee's configured catalogue
    /// product (GOLF-016).
    /// </summary>
    GreenFee = 0,

    /// <summary>
    /// An optional additional charge for one booking, priced from the D4 product
    /// catalogue. This is how Phase 6 §14's carts, caddies and rentals are
    /// expressed: as configurable charge lines, because the specification set
    /// defines no cart fleet, caddie roster or rental lifecycle of its own.
    /// </summary>
    Additional = 1,
}

/// <summary>
/// Golf charge lifecycle. A charge moves from <see cref="Pending"/> to
/// <see cref="Charged"/> when it is posted to the member account and to finance,
/// and may be waived or cancelled before that. GOLF-017 requires every charge to
/// stay linked to the player and the financial transaction, so a charge is never
/// deleted: cancellation and waiver are recorded states.
/// </summary>
public enum GolfChargeStatus
{
    Pending = 0,
    Charged = 1,
    Waived = 2,
    Cancelled = 3,
}
