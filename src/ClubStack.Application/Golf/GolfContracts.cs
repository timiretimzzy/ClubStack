using ClubStack.Domain.Golf;

namespace ClubStack.Application.Golf;

// ===========================================================================
// Courses (GOLF-001), holes and tees (Phase 3 §40/§41)
// ===========================================================================

public sealed record GolfHoleDto(
    Guid Id,
    Guid CourseId,
    int HoleNumber,
    int Par,
    int? Yardage,
    string? Name);

public sealed record GolfCourseDto(
    Guid Id,
    string Code,
    string Name,
    int NumberOfHoles,
    GolfCourseStatus Status,
    string CurrencyCode,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<GolfHoleDto> Holes,
    IReadOnlyList<GolfTeeDto> Tees);

public sealed record GolfCourseSearchResult(
    IReadOnlyList<GolfCourseDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GolfCourseSearchInput(
    string? Code,
    GolfCourseStatus? Status,
    int Page = 1,
    int PageSize = 50);

public sealed record CreateGolfCourseInput(
    string Code,
    string Name,
    int NumberOfHoles,
    string CurrencyCode,
    string? Description,
    IReadOnlyList<CreateGolfHoleInput> Holes);

public sealed record CreateGolfHoleInput(int HoleNumber, int Par, int? Yardage, string? Name);

public sealed record GolfTeeDto(
    Guid Id,
    Guid CourseId,
    string Code,
    string Name,
    int Capacity,
    GolfTeeStatus Status,
    bool MembersOnly,
    bool GuestsAllowed,
    int? MinimumPlayers,
    int? MaximumPlayers,
    Guid? GreenFeeProductId,
    Guid? GuestFeeProductId,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateGolfTeeInput(
    string Code,
    string Name,
    int Capacity,
    bool MembersOnly,
    bool GuestsAllowed,
    int? MinimumPlayers,
    int? MaximumPlayers,
    Guid? GreenFeeProductId,
    Guid? GuestFeeProductId,
    string? Notes);

public interface IGolfCourseService
{
    Task<GolfCourseSearchResult> SearchCoursesAsync(GolfCourseSearchInput input, CancellationToken cancellationToken = default);

    Task<GolfCourseDto> GetCourseAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GolfCourseDto> CreateCourseAsync(CreateGolfCourseInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfCourseDto> SetCourseStatusAsync(Guid id, GolfCourseStatus status, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfTeeDto> GetTeeAsync(Guid teeId, CancellationToken cancellationToken = default);

    Task<GolfTeeDto> CreateTeeAsync(Guid courseId, CreateGolfTeeInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfTeeDto> UpdateTeeAsync(Guid teeId, CreateGolfTeeInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task SetTeeStatusAsync(Guid teeId, GolfTeeStatus status, string actorUserId, CancellationToken cancellationToken = default);
}

// ===========================================================================
// Tee slots and availability (GOLF-002/003/004/005, Phase 9 §12)
// ===========================================================================

public sealed record GolfTeeSlotDto(
    Guid Id,
    Guid CourseId,
    Guid TeeId,
    string CourseCode,
    string CourseName,
    string TeeCode,
    string TeeName,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    int Capacity,
    int TakenPlayers,
    int OpenPlayers,
    TeeSlotStatus Status,
    int? MaximumPlayers,
    bool MembersOnly,
    bool GuestsAllowed,
    string CurrencyCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GolfTeeSlotSearchResult(
    IReadOnlyList<GolfTeeSlotDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// The tee-sheet query. Phase 9 §12 derives availability from course, tee, date,
/// time slot, configured capacity, existing bookings, applicable restrictions and
/// operational closure/blocking, so each of those is an explicit input.
/// </summary>
public sealed record GolfTeeSheetInput(
    DateOnly Date,
    Guid? CourseId,
    Guid? TeeId,
    bool BookableOnly = false,
    int Page = 1,
    int PageSize = 100);

public sealed record CreateGolfTeeSlotInput(
    Guid TeeId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    int? Capacity,
    int? MaximumPlayers,
    TeeSlotStatus Status = TeeSlotStatus.Open);

public sealed record GenerateGolfTeeSlotsInput(
    Guid TeeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly FirstStartTime,
    TimeOnly LastStartTime,
    int IntervalMinutes,
    int? Capacity,
    int DurationMinutes = 10);

public interface IGolfTeeSlotService
{
    Task<GolfTeeSlotSearchResult> GetTeeSheetAsync(GolfTeeSheetInput input, CancellationToken cancellationToken = default);

    Task<GolfTeeSlotDto> GetTeeSlotAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GolfTeeSlotDto> CreateTeeSlotAsync(CreateGolfTeeSlotInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfTeeSlotSearchResult> GenerateTeeSlotsAsync(GenerateGolfTeeSlotsInput input, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>GOLF-002 / Phase 9 §12: operational closure or reopening of a slot.</summary>
    Task<GolfTeeSlotDto> SetTeeSlotStatusAsync(Guid id, TeeSlotStatus status, string actorUserId, CancellationToken cancellationToken = default);
}

// ===========================================================================
// Guests (GOLF-008, GOLF-009, Phase 3 §45)
// ===========================================================================

public sealed record GolfGuestDto(
    Guid Id,
    string Reference,
    string FullName,
    string? ContactDetails,
    Guid? HostMemberId,
    GolfGuestStatus Status,
    int VisitCount,
    DateTimeOffset? LastVisitedAtUtc,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GolfGuestSearchResult(
    IReadOnlyList<GolfGuestDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GolfGuestSearchInput(
    string? Search,
    GolfGuestStatus? Status,
    int Page = 1,
    int PageSize = 50);

public sealed record CreateGolfGuestInput(
    string FullName,
    string? ContactDetails,
    Guid? HostMemberId,
    string? Notes);

public interface IGolfGuestService
{
    Task<GolfGuestSearchResult> SearchGuestsAsync(GolfGuestSearchInput input, CancellationToken cancellationToken = default);

    Task<GolfGuestDto> GetGuestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GolfGuestDto> CreateGuestAsync(CreateGolfGuestInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfGuestDto> SetGuestStatusAsync(Guid id, GolfGuestStatus status, string actorUserId, CancellationToken cancellationToken = default);
}

// ===========================================================================
// Bookings (GOLF-006/007/010/011/013/014/015/020/021/022)
// ===========================================================================

public sealed record GolfBookingPlayerDto(
    Guid Id,
    Guid BookingId,
    GolfPlayerType PlayerType,
    Guid? MemberId,
    string? MemberNumber,
    string? MemberName,
    Guid? GolfGuestId,
    string? GuestName,
    decimal Fee,
    string CurrencyCode,
    Guid? AccountTransactionId,
    Guid? ReceivableId,
    GolfBookingPlayerStatus Status,
    int Position);

/// <summary>
/// One player on a booking (Phase 3 §44). Exactly one of
/// <paramref name="MemberId"/> / <paramref name="GolfGuestId"/> is supplied, and
/// the service rejects any other combination so a guest can never be recorded as
/// a member and a member can never be recorded as a guest.
/// </summary>
public sealed record GolfBookingPlayerInput(
    Guid? MemberId,
    Guid? GolfGuestId,
    int? Position = null);

public sealed record GolfBookingChargeDto(
    Guid Id,
    Guid BookingId,
    Guid? BookingPlayerId,
    GolfChargeType ChargeType,
    string Description,
    decimal Amount,
    string CurrencyCode,
    GolfChargeStatus Status,
    Guid? ProductId,
    Guid? AccountTransactionId,
    Guid? ReceivableId,
    Guid? JournalId,
    DateTimeOffset CreatedAtUtc);

public sealed record GolfBookingDto(
    Guid Id,
    string Reference,
    Guid TeeSlotId,
    Guid CourseId,
    Guid TeeId,
    string CourseName,
    string TeeName,
    DateOnly TeeDate,
    TimeOnly StartTime,
    GolfBookingStatus Status,
    Guid BookedByMemberId,
    int PlayerCount,
    decimal TotalFee,
    string CurrencyCode,
    string? CancellationReason,
    string? OverrideReason,
    bool CapacityOverridden,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset? CheckedInAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? NoShowAtUtc,
    string CreatedByUserId,
    string CorrelationId,
    IReadOnlyList<GolfBookingPlayerDto> Players,
    IReadOnlyList<GolfBookingChargeDto> Charges);

public sealed record GolfBookingSearchResult(
    IReadOnlyList<GolfBookingDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GolfBookingSearchInput(
    DateOnly? FromDate,
    DateOnly? ToDate,
    Guid? CourseId,
    Guid? TeeId,
    Guid? MemberId,
    GolfBookingStatus? Status,
    int Page = 1,
    int PageSize = 50);

/// <summary>
/// GOLF-006/GOLF-007: create a booking for a tee slot. The booking is created in
/// Draft and availability is validated when it is confirmed (GOLF-010), which is
/// why this input carries no capacity decision.
/// </summary>
public sealed record CreateGolfBookingInput(
    Guid TeeSlotId,
    Guid BookedByMemberId,
    IReadOnlyList<GolfBookingPlayerInput> Players,
    DateTimeOffset? BusinessDateUtc = null,
    string? IdempotencyKey = null);

public sealed record ConfirmGolfBookingInput(
    string? Note = null,
    bool OverrideCapacity = false,
    string? OverrideReason = null,
    string? IdempotencyKey = null);

public sealed record CancelGolfBookingInput(string Reason);

public sealed record GolfBookingOverrideInput(string Reason);

public interface IGolfBookingService
{
    Task<GolfBookingSearchResult> SearchBookingsAsync(GolfBookingSearchInput input, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> GetBookingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> CreateBookingAsync(CreateGolfBookingInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> AddPlayerAsync(Guid bookingId, GolfBookingPlayerInput player, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> RemovePlayerAsync(Guid bookingId, Guid playerId, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> ConfirmBookingAsync(Guid bookingId, ConfirmGolfBookingInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> CheckInBookingAsync(Guid bookingId, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> CompleteBookingAsync(Guid bookingId, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> CancelBookingAsync(Guid bookingId, CancelGolfBookingInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> NoShowBookingAsync(Guid bookingId, string actorUserId, CancellationToken cancellationToken = default);
}

// ===========================================================================
// Golf charges (GOLF-016, GOLF-017, XMOD-004, XMOD-005)
// ===========================================================================

/// <summary>A configured additional charge for a booking, priced from the D4 catalogue.</summary>
public sealed record AddGolfChargeInput(
    Guid ProductId,
    int? Quantity = null,
    Guid? BookingPlayerId = null,
    string? Description = null,
    string? IdempotencyKey = null);

/// <summary>
/// GOLF-019/020: post the pending charges of a booking. Posting is a money movement, so
/// the command carries an idempotency key: a retried POST must return the first result
/// rather than raise a second set of receivables.
/// </summary>
public sealed record PostGolfBookingChargesInput(string? IdempotencyKey = null);

public sealed record GolfChargeSearchResult(
    IReadOnlyList<GolfBookingChargeDto> Items,
    int TotalCount,
    decimal TotalAmount,
    int Page,
    int PageSize);

public sealed record GolfChargeSearchInput(
    Guid? BookingId,
    GolfChargeType? ChargeType,
    GolfChargeStatus? Status,
    Guid? MemberId,
    int Page = 1,
    int PageSize = 50);

public interface IGolfChargeService
{
    Task<GolfChargeSearchResult> SearchChargesAsync(GolfChargeSearchInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// GOLF-017 + XMOD-004: charges a booking's pending charges to the booking
    /// member's account, creating the D3 account transaction and the D6 receivable
    /// in the same transaction.
    /// </summary>
    Task<GolfBookingDto> PostBookingChargesAsync(
        Guid bookingId,
        PostGolfBookingChargesInput input,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<GolfBookingDto> AddChargeAsync(Guid bookingId, AddGolfChargeInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<GolfBookingDto> WaiveChargeAsync(Guid bookingId, Guid chargeId, string reason, string actorUserId, CancellationToken cancellationToken = default);
}
