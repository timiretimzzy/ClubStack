using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;

namespace ClubStack.Api.Tests;

/// <summary>
/// Stub golf services for <see cref="D7GolfAuthorizationTests"/>.
///
/// <para>Authorization is what those tests assert, so these answer every call with a
/// minimal valid DTO. Nothing here encodes golf behaviour: the state machine,
/// capacity guard, restrictions and financial posting are proved by the PostgreSQL
/// integration suite, and a stub that threw would abort the TestServer request
/// before the test could observe the authorization outcome.</para>
/// </summary>
internal abstract class GolfStub
{
    protected static readonly Guid Id = new("11111111-1111-1111-1111-111111111111");

    protected static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    protected static readonly DateOnly BusinessDate = new(2026, 1, 15);

    protected static readonly TimeOnly StartOfDay = new(7, 0);
}

internal sealed class StubGolfCourseService : GolfStub, IGolfCourseService
{
    private static readonly GolfHoleDto Hole = new(GolfStub.Id, GolfStub.Id, 1, 4, 380, null);

    private static readonly GolfTeeDto Tee = new(
        GolfStub.Id, GolfStub.Id, "CHAMP", "Championship", 4, GolfTeeStatus.Active, false, true, null, 4,
        null, null, null, GolfStub.Now, GolfStub.Now);

    private static readonly GolfCourseDto Course = new(
        GolfStub.Id, "MAIN", "Main Course", 18, GolfCourseStatus.Active, "EUR", "Stub course", GolfStub.Now,
        GolfStub.Now, [Hole], [Tee]);

    public Task<GolfCourseSearchResult> SearchCoursesAsync(
        GolfCourseSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new GolfCourseSearchResult([Course], 1, input.Page, input.PageSize));

    public Task<GolfCourseDto> GetCourseAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Course);

    public Task<GolfCourseDto> CreateCourseAsync(
        CreateGolfCourseInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Course);

    public Task<GolfCourseDto> SetCourseStatusAsync(
        Guid id, GolfCourseStatus status, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Course with { Status = status });

    public Task<GolfTeeDto> GetTeeAsync(Guid teeId, CancellationToken cancellationToken = default)
        => Task.FromResult(Tee);

    public Task<GolfTeeDto> CreateTeeAsync(
        Guid courseId, CreateGolfTeeInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Tee);

    public Task<GolfTeeDto> UpdateTeeAsync(
        Guid teeId, CreateGolfTeeInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Tee);

    public Task SetTeeStatusAsync(
        Guid teeId, GolfTeeStatus status, string actorUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class StubGolfTeeSlotService : GolfStub, IGolfTeeSlotService
{
    private static readonly GolfTeeSlotDto Slot = new(
        GolfStub.Id, GolfStub.Id, GolfStub.Id, "MAIN", "Main Course", "CHAMP", "Championship",
        GolfStub.BusinessDate, GolfStub.StartOfDay, 10, 4, 1, 3, TeeSlotStatus.Open, 4, false, true, "EUR",
        GolfStub.Now, GolfStub.Now);

    public Task<GolfTeeSlotSearchResult> GetTeeSheetAsync(
        GolfTeeSheetInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new GolfTeeSlotSearchResult([Slot], 1, input.Page, input.PageSize));

    public Task<GolfTeeSlotDto> GetTeeSlotAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Slot);

    public Task<GolfTeeSlotDto> CreateTeeSlotAsync(
        CreateGolfTeeSlotInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Slot);

    public Task<GolfTeeSlotSearchResult> GenerateTeeSlotsAsync(
        GenerateGolfTeeSlotsInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(new GolfTeeSlotSearchResult([Slot], 1, 1, 100));

    public Task<GolfTeeSlotDto> SetTeeSlotStatusAsync(
        Guid id, TeeSlotStatus status, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Slot with { Status = status });
}

internal sealed class StubGolfGuestService : GolfStub, IGolfGuestService
{
    private static readonly GolfGuestDto Guest = new(
        GolfStub.Id, "GST-000001", "Visitor Example", null, GolfStub.Id, GolfGuestStatus.Active, 1,
        GolfStub.Now, null, GolfStub.Now, GolfStub.Now);

    public Task<GolfGuestSearchResult> SearchGuestsAsync(
        GolfGuestSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new GolfGuestSearchResult([Guest], 1, input.Page, input.PageSize));

    public Task<GolfGuestDto> GetGuestAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Guest);

    public Task<GolfGuestDto> CreateGuestAsync(
        CreateGolfGuestInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Guest);

    public Task<GolfGuestDto> SetGuestStatusAsync(
        Guid id, GolfGuestStatus status, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Guest with { Status = status });
}

internal sealed class StubGolfBookingService : GolfStub, IGolfBookingService
{
    internal static readonly GolfBookingDto Booking = new(
        GolfStub.Id, "GBK-000001", GolfStub.Id, GolfStub.Id, GolfStub.Id, "Main Course", "Championship",
        GolfStub.BusinessDate, GolfStub.StartOfDay, GolfBookingStatus.Confirmed, GolfStub.Id, 1, 50m, "EUR",
        null, null, false, GolfStub.Now, GolfStub.Now, null, null, null, null, "test-user", "corr",
        [new GolfBookingPlayerDto(
            GolfStub.Id, GolfStub.Id, GolfPlayerType.Member, GolfStub.Id, "M-0001", "Test Member", null, null,
            50m, "EUR", null, null, GolfBookingPlayerStatus.Booked, 1)],
        [new GolfBookingChargeDto(
            GolfStub.Id, GolfStub.Id, GolfStub.Id, GolfChargeType.GreenFee, "Green fee", 50m, "EUR",
            GolfChargeStatus.Pending, GolfStub.Id, null, null, null, GolfStub.Now)]);

    public Task<GolfBookingSearchResult> SearchBookingsAsync(
        GolfBookingSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new GolfBookingSearchResult([Booking], 1, input.Page, input.PageSize));

    public Task<GolfBookingDto> GetBookingAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking);

    public Task<GolfBookingDto> CreateBookingAsync(
        CreateGolfBookingInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking with { Status = GolfBookingStatus.Draft, ConfirmedAtUtc = null });

    public Task<GolfBookingDto> AddPlayerAsync(
        Guid bookingId, GolfBookingPlayerInput player, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking);

    public Task<GolfBookingDto> RemovePlayerAsync(
        Guid bookingId, Guid playerId, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking);

    public Task<GolfBookingDto> ConfirmBookingAsync(
        Guid bookingId, ConfirmGolfBookingInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking);

    public Task<GolfBookingDto> CheckInBookingAsync(
        Guid bookingId, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking with { Status = GolfBookingStatus.CheckedIn, CheckedInAtUtc = GolfStub.Now });

    public Task<GolfBookingDto> CompleteBookingAsync(
        Guid bookingId, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking with { Status = GolfBookingStatus.Completed, CompletedAtUtc = GolfStub.Now });

    public Task<GolfBookingDto> CancelBookingAsync(
        Guid bookingId, CancelGolfBookingInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking with
        {
            Status = GolfBookingStatus.Cancelled,
            CancellationReason = input.Reason,
            CancelledAtUtc = GolfStub.Now,
        });

    public Task<GolfBookingDto> NoShowBookingAsync(
        Guid bookingId, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Booking with { Status = GolfBookingStatus.NoShow, NoShowAtUtc = GolfStub.Now });
}

internal sealed class StubGolfChargeService : GolfStub, IGolfChargeService
{
    private static readonly GolfBookingChargeDto Charge = new(
        GolfStub.Id, GolfStub.Id, GolfStub.Id, GolfChargeType.Additional, "Cart hire", 100m, "EUR",
        GolfChargeStatus.Pending, GolfStub.Id, null, null, null, GolfStub.Now);

    public Task<GolfChargeSearchResult> SearchChargesAsync(
        GolfChargeSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new GolfChargeSearchResult([Charge], 1, 100m, input.Page, input.PageSize));

    public Task<GolfBookingDto> PostBookingChargesAsync(
        Guid bookingId, PostGolfBookingChargesInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default)
        => Task.FromResult(StubGolfBookingService.Booking);

    public Task<GolfBookingDto> AddChargeAsync(
        Guid bookingId, AddGolfChargeInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(StubGolfBookingService.Booking);

    public Task<GolfBookingDto> WaiveChargeAsync(
        Guid bookingId, Guid chargeId, string reason, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(StubGolfBookingService.Booking);
}
