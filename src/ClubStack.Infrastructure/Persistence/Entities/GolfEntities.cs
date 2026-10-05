using ClubStack.Domain.Golf;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Golf course (Phase 8 §8.9 <c>golf_courses</c>, Phase 3 §40 Course).
/// <para><see cref="NumberOfHoles"/> and <see cref="Holes"/> are the course
/// configuration Phase 3 §40 describes. Courses are inactivated, never deleted,
/// so historic bookings keep a valid reference (Phase 3 §79).</para>
/// </summary>
public sealed class GolfCourseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Human course code, unique across the installation (Phase 3 §40 Code).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Phase 3 §40 NumberOfHoles.</summary>
    public int NumberOfHoles { get; set; }

    public GolfCourseStatus Status { get; set; } = GolfCourseStatus.Active;

    /// <summary>
    /// Currency the course's fees are quoted in. This is club configuration
    /// (Phase 8 §30); D6 B17 owns the base currency and D6 owns the accounting
    /// treatment, so golf never converts or re-denominates an amount.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<GolfHoleEntity> Holes { get; set; } = new();

    public List<GolfTeeEntity> Tees { get; set; } = new();

    public List<GolfTeeSlotEntity> TeeSlots { get; set; } = new();
}

/// <summary>Course hole configuration (Phase 8 §8.9 <c>golf_holes</c>).</summary>
public sealed class GolfHoleEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CourseId { get; set; }

    public GolfCourseEntity Course { get; set; } = null!;

    public int HoleNumber { get; set; }

    public int Par { get; set; }

    public int? Yardage { get; set; }

    public string? Name { get; set; }
}

/// <summary>
/// Tee configuration (Phase 8 §8.9 <c>golf_tees</c>, Phase 3 §41 Tee). The
/// GOLF-012 restriction dimensions and the GOLF-016 fee configuration both live
/// here, because Phase 3 §41 puts <c>Rules</c> on the tee and the tee's capacity
/// is the default for its slots.
/// </summary>
public sealed class GolfTeeEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CourseId { get; set; }

    public GolfCourseEntity Course { get; set; } = null!;

    /// <summary>Phase 3 §41 Code; unique within the installation.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Phase 3 §41 Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Phase 3 §41 Capacity — the default capacity for this tee's slots.</summary>
    public int Capacity { get; set; }

    public GolfTeeStatus Status { get; set; } = GolfTeeStatus.Active;

    // ---- GOLF-012 member restriction ----
    public bool MembersOnly { get; set; }

    // ---- GOLF-012 guest restriction ----
    public bool GuestsAllowed { get; set; } = true;

    // ---- GOLF-012 player-count restriction (the time/format dimension is the slot) ----
    public int? MinimumPlayers { get; set; }

    public int? MaximumPlayers { get; set; }

    /// <summary>
    /// GOLF-016 fee configuration. The green fee for a member player is the price
    /// of this D4 product. Golf therefore reuses the authoritative product and
    /// price catalogue instead of introducing a golf-specific price table, and no
    /// amount is ever hard-coded.
    /// </summary>
    public Guid? GreenFeeProductId { get; set; }

    public ProductEntity? GreenFeeProduct { get; set; }

    /// <summary>GOLF-016 / GOLF-009: the fee product used for a non-member visitor.</summary>
    public Guid? GuestFeeProductId { get; set; }

    public ProductEntity? GuestFeeProduct { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<GolfTeeSlotEntity> TeeSlots { get; set; } = new();
}

/// <summary>
/// A scheduled availability unit (Phase 8 §8.9 <c>tee_slots</c>, Phase 3 §42
/// TeeSlot). Phase 3 §42 states that "availability is derived from slot capacity,
/// bookings, closures, and restrictions", so this row stores only the configured
/// inputs; the derived figures live on the DTO.
/// </summary>
public sealed class GolfTeeSlotEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CourseId { get; set; }

    public GolfCourseEntity Course { get; set; } = null!;

    public Guid TeeId { get; set; }

    public GolfTeeEntity Tee { get; set; } = null!;

    /// <summary>Phase 3 §42 Date. Stored as a date so a tee sheet is queried by business date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Phase 3 §42 StartTime, stored as a time-of-day.</summary>
    public TimeOnly StartTime { get; set; }

    public int DurationMinutes { get; set; } = 10;

    /// <summary>Phase 3 §42 Capacity — player places, defaulted from the tee when not overridden.</summary>
    public int Capacity { get; set; }

    public TeeSlotStatus Status { get; set; } = TeeSlotStatus.Open;

    /// <summary>Phase 3 §42 Restrictions: the per-slot player ceiling for this slot.</summary>
    public int? MaximumPlayers { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<GolfBookingEntity> Bookings { get; set; } = new();
}

/// <summary>
/// Golf guest record (Phase 8 §8.9 <c>golf_guest_records</c>, Phase 3 §45 Guest).
/// Phase 3 §45 is explicit that guest information is "separate from membership"
/// and that a guest participates "without becoming a member", so this is a
/// golf-owned record and never a second member master. <see cref="HostMemberId"/>
/// points at the D2 member who hosts the visit, which is the only member link.
/// </summary>
public sealed class GolfGuestRecordEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>GST-###### business reference (Phase 8 §3.2).</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Phase 3 §45 Name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Phase 3 §45 ContactDetails.</summary>
    public string? ContactDetails { get; set; }

    /// <summary>Phase 3 §45 HostPersonId, expressed as the D2 member who hosts the guest.</summary>
    public Guid? HostMemberId { get; set; }

    public MemberEntity? HostMember { get; set; }

    public GolfGuestStatus Status { get; set; } = GolfGuestStatus.Active;

    /// <summary>Phase 3 §45 History, summarised as a visit count plus last visit.</summary>
    public int VisitCount { get; set; }

    public DateTimeOffset? LastVisitedAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<GolfBookingPlayerEntity> BookingPlayers { get; set; } = new();
}

/// <summary>
/// Golf booking (Phase 8 §8.9 <c>golf_bookings</c>, Phase 3 §43 GolfBooking). The
/// field set is Phase 3 §43 plus the GOLF-011 override record that Phase 9 §12
/// requires ("actor, reason, timestamp, affected booking/slot").
/// </summary>
public sealed class GolfBookingEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>GBK-###### business reference (Phase 3 §43 BookingReference).</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Phase 3 §79: "Booking cannot reference a nonexistent tee slot."</summary>
    public Guid TeeSlotId { get; set; }

    public GolfTeeSlotEntity TeeSlot { get; set; } = null!;

    /// <summary>Phase 3 §43 BookedByPersonId, expressed as the D2 member who made the booking.</summary>
    public Guid BookedByMemberId { get; set; }

    public MemberEntity BookedByMember { get; set; } = null!;

    public GolfBookingStatus Status { get; set; } = GolfBookingStatus.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    // ---- Phase 8 §7.7 / Phase 3 §43 lifecycle timestamps ----
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public DateTimeOffset? CheckedInAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public DateTimeOffset? NoShowAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    // ---- GOLF-011 / Phase 9 §12 override record ----
    /// <summary>True when the booking was confirmed over capacity under Golf.Booking.Override.</summary>
    public bool CapacityOverridden { get; set; }

    public string? OverrideReason { get; set; }

    public string? OverrideByUserId { get; set; }

    public DateTimeOffset? OverriddenAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public List<GolfBookingPlayerEntity> Players { get; set; } = new();

    public List<GolfChargeEntity> Charges { get; set; } = new();
}

/// <summary>
/// A player on a booking (Phase 8 §8.9 <c>golf_booking_players</c>, Phase 3 §44).
/// <para>Phase 3 §44 stores <c>PersonId</c> nullable "for visitor" and a nullable
/// <c>GuestId</c> together with a <c>Fee</c> and a
/// <c>Payment/AccountReference</c>. The account/finance references are recorded
/// here as well as on the charge row so a player's charge is traceable from the
/// player itself (GOLF-017).</para>
/// </summary>
public sealed class GolfBookingPlayerEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid BookingId { get; set; }

    public GolfBookingEntity Booking { get; set; } = null!;

    /// <summary>Discriminator between a member and a guest (Phase 3 §44 PlayerType).</summary>
    public GolfPlayerType PlayerType { get; set; }

    /// <summary>D2 member, set when <see cref="PlayerType"/> is <see cref="GolfPlayerType.Member"/>.</summary>
    public Guid? MemberId { get; set; }

    public MemberEntity? Member { get; set; }

    /// <summary>Golf guest record, set when <see cref="PlayerType"/> is <see cref="GolfPlayerType.Guest"/>.</summary>
    public Guid? GolfGuestId { get; set; }

    public GolfGuestRecordEntity? GolfGuest { get; set; }

    /// <summary>
    /// Phase 3 §44 Fee. The amount is resolved once from the tee's configured
    /// catalogue price and snapshotted here, so a later price change never
    /// rewrites a confirmed booking's fee.
    /// </summary>
    public decimal Fee { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Phase 3 §44 Payment/AccountReference: the D3 account transaction raised for this player's fee.</summary>
    public Guid? AccountTransactionId { get; set; }

    /// <summary>The D6 receivable raised for this player's fee (XMOD-005).</summary>
    public Guid? ReceivableId { get; set; }

    public GolfBookingPlayerStatus Status { get; set; } = GolfBookingPlayerStatus.Booked;

    /// <summary>Order within the booking, used by the Phase 5 §48 tee-sheet player list.</summary>
    public int Position { get; set; }
}

/// <summary>
/// Golf charge (Phase 8 §8.9 <c>golf_charges</c>, Phase 3 §75 Golf Charge).
/// <para>Phase 3 §75 defines a golf charge as "Golf Charge + Account/Payment
/// relationship + Financial Source Record", and GOLF-017 requires the charge to be
/// linked to the player and the financial transaction. That is exactly the three
/// link columns here: the player, the D3 account transaction, and the D6 financial
/// source records.</para>
/// </summary>
public sealed class GolfChargeEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid BookingId { get; set; }

    public GolfBookingEntity Booking { get; set; } = null!;

    /// <summary>Null for a booking-level additional charge such as a cart or caddie hire.</summary>
    public Guid? BookingPlayerId { get; set; }

    public GolfBookingPlayerEntity? BookingPlayer { get; set; }

    public GolfChargeType ChargeType { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public GolfChargeStatus Status { get; set; } = GolfChargeStatus.Pending;

    /// <summary>The D4 product that priced this charge, when it was priced from the catalogue.</summary>
    public Guid? ProductId { get; set; }

    public ProductEntity? Product { get; set; }

    /// <summary>Phase 3 §75 Account/Payment relationship: the D3 account transaction.</summary>
    public Guid? AccountTransactionId { get; set; }

    /// <summary>Phase 3 §75 Financial Source Record: the D6 receivable.</summary>
    public Guid? ReceivableId { get; set; }

    /// <summary>Phase 3 §75 Financial Source Record: the D6 journal.</summary>
    public Guid? JournalId { get; set; }

    public string? WaivedReason { get; set; }

    /// <summary>
    /// Why the charge was cancelled, distinct from <see cref="WaivedReason"/>. A
    /// cancellation is a consequence of the booking (GOLF-020) while a waiver is a
    /// decision about the charge itself, so the two are recorded separately.
    /// </summary>
    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
