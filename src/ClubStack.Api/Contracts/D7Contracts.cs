using System.ComponentModel.DataAnnotations;
using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;

namespace ClubStack.Api.Contracts;

// ===========================================================================
// Golf Courses (GOLF-001) — GOLF-001 permission, Phase 3 §40/§41
// ===========================================================================

public sealed class GolfCourseSearchRequest
{
    public string? Code { get; set; }

    public GolfCourseStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class CreateGolfCourseRequest
{
    [Required, StringLength(32, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 36)]
    public int NumberOfHoles { get; set; }

    [Required, StringLength(8, MinimumLength = 1)]
    public string CurrencyCode { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; set; }

    /// <summary>Optional hole configuration. When supplied it must cover the declared hole count exactly.</summary>
    public List<CreateGolfHoleRequest> Holes { get; set; } = new();
}

public sealed class CreateGolfHoleRequest
{
    [Range(1, 36)]
    public int HoleNumber { get; set; }

    [Range(2, 6)]
    public int Par { get; set; }

    [Range(0, 1000)]
    public int? Yardage { get; set; }

    [StringLength(80)]
    public string? Name { get; set; }
}

public sealed class CreateGolfTeeRequest
{
    [Required, StringLength(32, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 24)]
    public int Capacity { get; set; }

    /// <summary>GOLF-012 member restriction.</summary>
    public bool MembersOnly { get; set; }

    /// <summary>GOLF-012 guest restriction.</summary>
    public bool GuestsAllowed { get; set; } = true;

    /// <summary>GOLF-012 player-count restriction.</summary>
    [Range(1, 12)]
    public int? MinimumPlayers { get; set; }

    [Range(1, 12)]
    public int? MaximumPlayers { get; set; }

    /// <summary>GOLF-016: the D4 product whose member price is the green fee.</summary>
    public Guid? GreenFeeProductId { get; set; }

    /// <summary>GOLF-016/GOLF-009: the D4 product whose standard price is the visitor fee.</summary>
    public Guid? GuestFeeProductId { get; set; }

    [StringLength(512)]
    public string? Notes { get; set; }
}

public sealed class GolfHoleResponse
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public int HoleNumber { get; set; }

    public int Par { get; set; }

    public int? Yardage { get; set; }

    public string? Name { get; set; }
}

public sealed class GolfTeeResponse
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public GolfTeeStatus Status { get; set; }

    public bool MembersOnly { get; set; }

    public bool GuestsAllowed { get; set; }

    public int? MinimumPlayers { get; set; }

    public int? MaximumPlayers { get; set; }

    public Guid? GreenFeeProductId { get; set; }

    public Guid? GuestFeeProductId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class GolfCourseResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int NumberOfHoles { get; set; }

    public GolfCourseStatus Status { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<GolfHoleResponse> Holes { get; set; } = new();

    public List<GolfTeeResponse> Tees { get; set; } = new();
}

public sealed class GolfCourseSearchResponse
{
    public List<GolfCourseResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public sealed class SetGolfCourseStatusRequest
{
    public GolfCourseStatus Status { get; set; }
}

public sealed class SetGolfTeeStatusRequest
{
    public GolfTeeStatus Status { get; set; }
}

// ===========================================================================
// Tee sheet and tee slots (GOLF-002/003/004/005) — Phase 9 §12
// ===========================================================================

public sealed class GolfTeeSheetRequest
{
    [Required]
    public DateOnly Date { get; set; }

    public Guid? CourseId { get; set; }

    public Guid? TeeId { get; set; }

    /// <summary>Restricts the result to slots that can currently accept a booking.</summary>
    public bool BookableOnly { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 100;
}

public sealed class CreateGolfTeeSlotRequest
{
    [Required]
    public Guid TeeId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Range(5, 240)]
    public int DurationMinutes { get; set; } = 10;

    /// <summary>Defaults to the tee's configured capacity when omitted (Phase 3 §42).</summary>
    [Range(1, 24)]
    public int? Capacity { get; set; }

    [Range(1, 12)]
    public int? MaximumPlayers { get; set; }

    public TeeSlotStatus Status { get; set; } = TeeSlotStatus.Open;
}

public sealed class GenerateGolfTeeSlotsRequest
{
    [Required]
    public Guid TeeId { get; set; }

    [Required]
    public DateOnly FromDate { get; set; }

    [Required]
    public DateOnly ToDate { get; set; }

    [Required]
    public TimeOnly FirstStartTime { get; set; }

    [Required]
    public TimeOnly LastStartTime { get; set; }

    [Range(5, 240)]
    public int IntervalMinutes { get; set; } = 10;

    [Range(1, 24)]
    public int? Capacity { get; set; }

    [Range(5, 240)]
    public int DurationMinutes { get; set; } = 10;
}

public sealed class GolfTeeSlotResponse
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid TeeId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string TeeCode { get; set; } = string.Empty;

    public string TeeName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public int DurationMinutes { get; set; }

    public int Capacity { get; set; }

    /// <summary>Players already held by capacity-consuming bookings.</summary>
    public int TakenPlayers { get; set; }

    /// <summary>Player places still bookable (Phase 5 §48 shows this as "open").</summary>
    public int OpenPlayers { get; set; }

    public TeeSlotStatus Status { get; set; }

    public int? MaximumPlayers { get; set; }

    public bool MembersOnly { get; set; }

    public bool GuestsAllowed { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class GolfTeeSlotSearchResponse
{
    public List<GolfTeeSlotResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public sealed class SetGolfTeeSlotStatusRequest
{
    /// <summary>Blocked is the GOLF-002 closure/unavailability state.</summary>
    public TeeSlotStatus Status { get; set; }
}

// ===========================================================================
// Guests (GOLF-008/GOLF-009)
// ===========================================================================

public sealed class GolfGuestSearchRequest
{
    public string? Search { get; set; }

    public GolfGuestStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class CreateGolfGuestRequest
{
    [Required, StringLength(160, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(256)]
    public string? ContactDetails { get; set; }

    /// <summary>The D2 member hosting the visit. Golf never creates a member.</summary>
    public Guid? HostMemberId { get; set; }

    [StringLength(512)]
    public string? Notes { get; set; }
}

public sealed class GolfGuestResponse
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? ContactDetails { get; set; }

    public Guid? HostMemberId { get; set; }

    public GolfGuestStatus Status { get; set; }

    public int VisitCount { get; set; }

    public DateTimeOffset? LastVisitedAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class GolfGuestSearchResponse
{
    public List<GolfGuestResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public sealed class SetGolfGuestStatusRequest
{
    public GolfGuestStatus Status { get; set; }
}

// ===========================================================================
// Bookings (GOLF-006/007/010/011/013/014/020/021/022)
// ===========================================================================

public sealed class GolfBookingSearchRequest
{
    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public Guid? CourseId { get; set; }

    public Guid? TeeId { get; set; }

    public Guid? MemberId { get; set; }

    public GolfBookingStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class CreateGolfBookingRequest
{
    [Required]
    public Guid TeeSlotId { get; set; }

    /// <summary>The D2 member making the booking.</summary>
    [Required]
    public Guid BookedByMemberId { get; set; }

    /// <summary>Each entry must be exactly one of a member or a golf guest record.</summary>
    [Required, MinLength(1)]
    public List<GolfBookingPlayerRequest> Players { get; set; } = new();

    public DateTimeOffset? BusinessDateUtc { get; set; }

    /// <summary>
    /// Optional retry key. A retried create with the same key returns the first booking
    /// instead of booking the slot twice.
    /// </summary>
    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public sealed class GolfBookingPlayerRequest
{
    public Guid? MemberId { get; set; }

    public Guid? GolfGuestId { get; set; }

    [Range(1, 12)]
    public int? Position { get; set; }
}

public sealed class ConfirmGolfBookingRequest
{
    [StringLength(256)]
    public string? Note { get; set; }

    /// <summary>GOLF-011: requires Golf.Booking.Override and a reason.</summary>
    public bool OverrideCapacity { get; set; }

    [StringLength(512)]
    public string? OverrideReason { get; set; }

    /// <summary>
    /// Optional retry key. Confirming consumes capacity, prices fees and raises green-fee
    /// charges, so a retried confirm must return the first result rather than act twice.
    /// </summary>
    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public sealed class PostGolfBookingChargesRequest
{
    /// <summary>Optional retry key for the money movement (XMOD-004).</summary>
    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public sealed class CancelGolfBookingRequest
{
    [Required, StringLength(512, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AddGolfChargeRequest
{
    /// <summary>The D4 catalogue product that prices the charge (cart, caddie, rental, other).</summary>
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 100)]
    public int? Quantity { get; set; }

    /// <summary>Optional player link. Omitted for a booking-level charge.</summary>
    public Guid? BookingPlayerId { get; set; }

    [StringLength(256)]
    public string? Description { get; set; }

    /// <summary>Optional retry key. A repeated request returns the first charge.</summary>
    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public sealed class WaiveGolfChargeRequest
{
    [Required, StringLength(512, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class GolfBookingPlayerResponse
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public GolfPlayerType PlayerType { get; set; }

    public Guid? MemberId { get; set; }

    public string? MemberNumber { get; set; }

    public string? MemberName { get; set; }

    public Guid? GolfGuestId { get; set; }

    public string? GuestName { get; set; }

    public decimal Fee { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public Guid? AccountTransactionId { get; set; }

    public Guid? ReceivableId { get; set; }

    public GolfBookingPlayerStatus Status { get; set; }

    public int Position { get; set; }
}

public sealed class GolfBookingChargeResponse
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid? BookingPlayerId { get; set; }

    public GolfChargeType ChargeType { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public GolfChargeStatus Status { get; set; }

    public Guid? ProductId { get; set; }

    public Guid? AccountTransactionId { get; set; }

    public Guid? ReceivableId { get; set; }

    public Guid? JournalId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class GolfBookingResponse
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public Guid TeeSlotId { get; set; }

    public Guid CourseId { get; set; }

    public Guid TeeId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string TeeName { get; set; } = string.Empty;

    public DateOnly TeeDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public GolfBookingStatus Status { get; set; }

    public Guid BookedByMemberId { get; set; }

    public int PlayerCount { get; set; }

    public decimal TotalFee { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string? CancellationReason { get; set; }

    public string? OverrideReason { get; set; }

    /// <summary>GOLF-011: true when confirmed over capacity under an authorized override.</summary>
    public bool CapacityOverridden { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public DateTimeOffset? CheckedInAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public DateTimeOffset? NoShowAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public List<GolfBookingPlayerResponse> Players { get; set; } = new();

    public List<GolfBookingChargeResponse> Charges { get; set; } = new();
}

public sealed class GolfBookingSearchResponse
{
    public List<GolfBookingResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

// ===========================================================================
// Golf charges (GOLF-016/GOLF-017, XMOD-004, XMOD-005)
// ===========================================================================

public sealed class GolfChargeSearchRequest
{
    public Guid? BookingId { get; set; }

    public GolfChargeType? ChargeType { get; set; }

    public GolfChargeStatus? Status { get; set; }

    public Guid? MemberId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class GolfChargeSearchResponse
{
    public List<GolfBookingChargeResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    /// <summary>Total of the charges already posted to the member account.</summary>
    public decimal TotalAmount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
