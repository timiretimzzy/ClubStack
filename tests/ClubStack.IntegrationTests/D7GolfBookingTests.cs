using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D7 booking behaviour against real PostgreSQL: GOLF-006/007 (create, configure
/// players), GOLF-010 (validate availability before confirming), GOLF-011 (prevent
/// overbooking), GOLF-012 (member/guest/player restrictions), GOLF-013/014 (check-in
/// and completion), GOLF-016/017 (fees and charges), GOLF-020 (cancellation frees the
/// places) and GOLF-021/022 (state machine, no-show).
/// </summary>
[Collection("Database")]
public sealed class D7GolfBookingTests : DatabaseTestBase
{
    public D7GolfBookingTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private GolfTestData Data => GolfTestData.Create(Services, Fixture.ContextFactory);

    // ---------------------------------------------------------------------
    // GOLF-006/007: creating a booking
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_new_booking_is_a_draft_that_holds_no_places()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(2);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(players[0], null),
            new GolfBookingPlayerInput(players[1], null));

        Assert.Equal(GolfBookingStatus.Draft, booking.Status);
        Assert.StartsWith("GBK-", booking.Reference, StringComparison.Ordinal);
        Assert.Equal(10, booking.Reference.Length);
        Assert.Null(booking.ConfirmedAtUtc);
        Assert.Equal(2, booking.PlayerCount);

        // GOLF-010 validates availability at confirmation, so a draft hold must not
        // consume capacity. This is the Phase 9 §12 "draft holds" decision made
        // observable.
        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(0, slot.TakenPlayers);
        Assert.Equal(4, slot.OpenPlayers);
    }

    [Fact]
    public async Task A_booking_must_have_at_least_one_player()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(fixture.TeeSlotId, bookedBy));

        Assert.Contains("at least one player", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_booking_cannot_reference_an_unknown_tee_slot()
    {
        var data = Data;
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(1);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(Guid.CreateVersion7(), bookedBy, new GolfBookingPlayerInput(players[0], null)));

        Assert.Contains("does not exist", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_booking_cannot_reference_an_unknown_booking_member()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var players = await data.CreateMembersAsync(1);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(
                fixture.TeeSlotId,
                Guid.CreateVersion7(),
                new GolfBookingPlayerInput(players[0], null)));

        Assert.Contains("bookedByMemberId", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_blocked_slot_is_not_bookable()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(1);

        // GOLF-002 operational closure.
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .SetTeeSlotStatusAsync(fixture.TeeSlotId, TeeSlotStatus.Blocked, data.Actor, default));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.CreateBookingAsync(fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(players[0], null)));

        Assert.Contains("not bookable", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_inactive_course_is_not_bookable()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(1);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfCourseService>()
            .SetCourseStatusAsync(fixture.CourseId, GolfCourseStatus.Inactive, data.Actor, default));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.CreateBookingAsync(fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(players[0], null)));

        Assert.Contains("not accepting bookings", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // GOLF-007: player composition
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_player_must_be_exactly_one_of_member_or_guest()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        // Neither.
        var neither = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(null, null)));
        Assert.Contains("exactly one", Flatten(neither), StringComparison.OrdinalIgnoreCase);

        // Both.
        var both = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(
                fixture.TeeSlotId,
                bookedBy,
                new GolfBookingPlayerInput(members[0], guest)));
        Assert.Contains("exactly one", Flatten(both), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_inactive_guest_cannot_be_booked()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .SetGuestStatusAsync(guest, GolfGuestStatus.Inactive, data.Actor, default));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(null, guest)));

        Assert.Contains("Inactive", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_draft_booking_can_be_reconfigured_but_a_confirmed_one_cannot()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(3);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(players[0], null));

        var added = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .AddPlayerAsync(booking.Id, new GolfBookingPlayerInput(players[1], null), data.Actor, default));
        Assert.Equal(2, added.PlayerCount);

        var removed = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .RemovePlayerAsync(booking.Id, added.Players[1].Id, data.Actor, default));
        Assert.Single(removed.Players);

        await data.ConfirmAsync(booking.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .AddPlayerAsync(booking.Id, new GolfBookingPlayerInput(players[2], null), data.Actor, default)));

        Assert.Contains("only be changed while it is Draft", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_booking_keeps_at_least_one_player()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(1);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(players[0], null));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .RemovePlayerAsync(booking.Id, booking.Players[0].Id, data.Actor, default)));

        Assert.Contains("at least one player", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // GOLF-011: capacity
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Confirming_consumes_places_and_availability_is_derived()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(3);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(players[0], null),
            new GolfBookingPlayerInput(players[1], null),
            new GolfBookingPlayerInput(players[2], null));

        var confirmed = await data.ConfirmAsync(booking.Id);
        Assert.Equal(GolfBookingStatus.Confirmed, confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAtUtc);
        Assert.All(confirmed.Players, p => Assert.Equal(GolfBookingPlayerStatus.Booked, p.Status));

        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(3, slot.TakenPlayers);
        Assert.Equal(1, slot.OpenPlayers);
    }

    [Fact]
    public async Task Overbooking_is_refused_with_the_live_numbers()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 2);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(4);

        var first = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[0], null),
            new GolfBookingPlayerInput(members[1], null));
        await data.ConfirmAsync(first.Id);

        var second = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[2], null));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => data.ConfirmAsync(second.Id));

        var text = Flatten(ex);
        Assert.Contains("0 open place(s)", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("needs 1", text, StringComparison.OrdinalIgnoreCase);

        // The refusal is a refusal: nothing moved.
        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(2, slot.TakenPlayers);
    }

    [Fact]
    public async Task An_authorised_override_confirms_over_capacity_and_is_recorded()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var first = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        await data.ConfirmAsync(first.Id);

        var second = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));

        var overridden = await data.ConfirmAsync(second.Id, overrideCapacity: true, overrideReason: "Walking event");

        Assert.Equal(GolfBookingStatus.Confirmed, overridden.Status);
        Assert.True(overridden.CapacityOverridden);
        Assert.Equal("Walking event", overridden.OverrideReason);

        // Phase 9 §12: the record names the actor and the timestamp as well as the reason.
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var stored = await context.GolfBookings.AsNoTracking().SingleAsync(b => b.Id == second.Id);
        Assert.Equal(data.Actor, stored.OverrideByUserId);
        Assert.NotNull(stored.OverriddenAtUtc);
    }

    [Fact]
    public async Task An_override_without_a_reason_is_refused()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var first = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        await data.ConfirmAsync(first.Id);

        var second = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.ConfirmAsync(second.Id, overrideCapacity: true));

        Assert.Contains("overrideReason", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_booking_frees_its_places()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 2);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[0], null),
            new GolfBookingPlayerInput(members[1], null));
        await data.ConfirmAsync(booking.Id);

        Assert.Equal(0, (await data.SlotAsync(fixture.TeeSlotId)).OpenPlayers);

        // GOLF-020.
        var cancelled = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CancelBookingAsync(booking.Id, new CancelGolfBookingInput("Weather"), data.Actor, default));

        Assert.Equal(GolfBookingStatus.Cancelled, cancelled.Status);
        Assert.Equal("Weather", cancelled.CancellationReason);
        Assert.All(cancelled.Players, p => Assert.Equal(GolfBookingPlayerStatus.Cancelled, p.Status));

        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(0, slot.TakenPlayers);
        Assert.Equal(2, slot.OpenPlayers);
    }

    [Fact]
    public async Task A_cancellation_reason_is_required()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var players = await data.CreateMembersAsync(1);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(players[0], null));
        await data.ConfirmAsync(booking.Id);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .CancelBookingAsync(booking.Id, new CancelGolfBookingInput("  "), data.Actor, default)));

        Assert.Contains("reason", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_no_show_still_holds_the_places()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 2);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[0], null),
            new GolfBookingPlayerInput(members[1], null));
        await data.ConfirmAsync(booking.Id);

        // GOLF-022: a no-show is a consuming state, because the places were held and the
        // green fees are still chargeable.
        var noShow = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .NoShowBookingAsync(booking.Id, data.Actor, default));

        Assert.Equal(GolfBookingStatus.NoShow, noShow.Status);
        Assert.NotNull(noShow.NoShowAtUtc);
        Assert.All(noShow.Players, p => Assert.Equal(GolfBookingPlayerStatus.NoShow, p.Status));

        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(2, slot.TakenPlayers);
    }

    // ---------------------------------------------------------------------
    // GOLF-021: the state machine
    // ---------------------------------------------------------------------

    [Fact]
    public async Task The_documented_lifecycle_is_walked_end_to_end()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[0], null),
            new GolfBookingPlayerInput(null, guest));
        await data.ConfirmAsync(booking.Id);

        var checkedIn = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CheckInBookingAsync(booking.Id, data.Actor, default));
        Assert.Equal(GolfBookingStatus.CheckedIn, checkedIn.Status);
        Assert.NotNull(checkedIn.CheckedInAtUtc);
        Assert.All(checkedIn.Players, p => Assert.Equal(GolfBookingPlayerStatus.CheckedIn, p.Status));

        var completed = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CompleteBookingAsync(booking.Id, data.Actor, default));
        Assert.Equal(GolfBookingStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAtUtc);

        // Phase 3 §45: a completed visit is recorded on the guest, who is still not a member.
        var guestRecord = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .GetGuestAsync(guest, default));
        Assert.Equal(1, guestRecord.VisitCount);
        Assert.NotNull(guestRecord.LastVisitedAtUtc);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Members.AnyAsync(m => m.Id == guest));
    }

    [Fact]
    public async Task A_draft_cannot_be_cancelled_checked_in_completed_or_marked_no_show()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(1);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));

        await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .CancelBookingAsync(booking.Id, new CancelGolfBookingInput("Changed my mind"), data.Actor, default)));

        await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .CheckInBookingAsync(booking.Id, data.Actor, default)));

        await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .CompleteBookingAsync(booking.Id, data.Actor, default)));

        await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .NoShowBookingAsync(booking.Id, data.Actor, default)));
    }

    [Fact]
    public async Task A_confirmed_booking_cannot_jump_straight_to_completed()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(1);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        await data.ConfirmAsync(booking.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .CompleteBookingAsync(booking.Id, data.Actor, default)));

        // The message names the documented edges, so an operator can see what to do next.
        Assert.Contains("Confirmed", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_cancelled_booking_is_final()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(1);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        await data.ConfirmAsync(booking.Id);
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CancelBookingAsync(booking.Id, new CancelGolfBookingInput("Weather"), data.Actor, default));

        await Assert.ThrowsAsync<ConflictException>(() => data.ConfirmAsync(booking.Id));
        await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
                .CheckInBookingAsync(booking.Id, data.Actor, default)));
    }

    [Fact]
    public async Task A_booking_cannot_be_confirmed_twice()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(1);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));

        await data.ConfirmAsync(booking.Id);
        await Assert.ThrowsAsync<ConflictException>(() => data.ConfirmAsync(booking.Id));
    }

    // ---------------------------------------------------------------------
    // GOLF-012: configured restrictions
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_members_only_tee_refuses_a_guest()
    {
        var data = Data;
        var course = await data.CreateCourseAsync();
        var tee = await data.CreateTeeAsync(course, capacity: 4, membersOnly: true);
        var slot = await CreateSlotAsync(tee, capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(slot, bookedBy, new GolfBookingPlayerInput(null, guest)));

        Assert.Contains("members only", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_tee_configured_not_to_accept_guests_refuses_one()
    {
        var data = Data;
        var course = await data.CreateCourseAsync();
        var tee = await data.CreateTeeAsync(course, capacity: 4, guestsAllowed: false);
        var slot = await CreateSlotAsync(tee, capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(slot, bookedBy, new GolfBookingPlayerInput(null, guest)));

        Assert.Contains("not to accept guests", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_minimum_player_count_is_enforced()
    {
        var data = Data;
        var course = await data.CreateCourseAsync();
        var tee = await data.CreateTeeAsync(course, capacity: 4, minimumPlayers: 2);
        var slot = await CreateSlotAsync(tee, capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(slot, bookedBy, new GolfBookingPlayerInput(members[0], null)));

        Assert.Contains("at least 2", Flatten(ex), StringComparison.OrdinalIgnoreCase);

        // Two players satisfy it, and the tee sheet's own capacity is not the restriction.
        var ok = await data.CreateBookingAsync(
            slot, bookedBy, new GolfBookingPlayerInput(members[0], null), new GolfBookingPlayerInput(members[1], null));
        Assert.Equal(2, ok.PlayerCount);
    }

    [Fact]
    public async Task A_slot_may_lower_the_tee_player_ceiling()
    {
        var data = Data;
        var course = await data.CreateCourseAsync();
        var tee = await data.CreateTeeAsync(course, capacity: 4, maximumPlayers: 4);
        var slot = await CreateSlotAsync(tee, capacity: 4, maximumPlayers: 2);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(3);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(
                slot,
                bookedBy,
                new GolfBookingPlayerInput(members[0], null),
                new GolfBookingPlayerInput(members[1], null),
                new GolfBookingPlayerInput(members[2], null)));

        Assert.Contains("at most 2", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // GOLF-016: fees come from the D4 catalogue
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Member_fees_use_the_member_price_and_guests_the_standard_price()
    {
        var data = Data;
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 500m, memberAmount: 400m);
        var guestFee = await data.CreateFeeProductAsync(standardAmount: 600m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee, guestFeeProductId: guestFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(member, null),
            new GolfBookingPlayerInput(null, guest));
        await data.ConfirmAsync(booking.Id);

        var confirmed = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .GetBookingAsync(booking.Id, default));

        var memberPlayer = Assert.Single(confirmed.Players, p => p.PlayerType == GolfPlayerType.Member);
        var guestPlayer = Assert.Single(confirmed.Players, p => p.PlayerType == GolfPlayerType.Guest);

        Assert.Equal(400m, memberPlayer.Fee);
        Assert.Equal(600m, guestPlayer.Fee);
        Assert.Equal(1000m, confirmed.TotalFee);
        Assert.Equal(data.MoneyCurrency, memberPlayer.CurrencyCode);

        // GOLF-017: one green-fee charge per player, raised at confirmation.
        var greenFees = confirmed.Charges.Where(c => c.ChargeType == GolfChargeType.GreenFee).ToList();
        Assert.Equal(2, greenFees.Count);
        Assert.All(greenFees, c => Assert.Equal(GolfChargeStatus.Pending, c.Status));
        Assert.Equal(1000m, greenFees.Sum(c => c.Amount));
    }

    [Fact]
    public async Task A_confirmed_fee_is_a_snapshot_that_a_later_price_change_does_not_rewrite()
    {
        var data = Data;
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 500m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        // The club raises the green fee.
        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var price = await context.ProductPrices.SingleAsync(p => p.ProductId == greenFee);
            price.Amount = 750m;
            await context.SaveChangesAsync();
        }

        var reloaded = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .GetBookingAsync(booking.Id, default));

        Assert.Equal(500m, Assert.Single(reloaded.Players).Fee);
        Assert.Equal(500m, reloaded.TotalFee);
    }

    [Fact]
    public async Task A_tee_with_no_configured_fee_confirms_at_zero()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var confirmed = await data.ConfirmAsync(booking.Id);

        // No amount is invented: an unconfigured fee is a zero fee, and the charge line
        // still exists so the booking's charge set is complete.
        Assert.Equal(0m, confirmed.TotalFee);
        Assert.Single(confirmed.Charges);
        Assert.Equal(0m, Assert.Single(confirmed.Charges).Amount);
    }

    // ---------------------------------------------------------------------
    // Booking search
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Bookings_can_be_searched_by_status_course_and_member()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var confirmed = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(confirmed.Id);

        var draft = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));

        var byStatus = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .SearchBookingsAsync(new GolfBookingSearchInput(null, null, null, null, null, GolfBookingStatus.Draft), default));
        Assert.Contains(byStatus.Items, b => b.Id == draft.Id);
        Assert.DoesNotContain(byStatus.Items, b => b.Id == confirmed.Id);

        var byMember = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .SearchBookingsAsync(new GolfBookingSearchInput(null, null, null, null, bookedBy, null), default));
        Assert.Contains(byMember.Items, b => b.Id == confirmed.Id);
        Assert.Contains(byMember.Items, b => b.Id == draft.Id);

        var byCourse = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .SearchBookingsAsync(new GolfBookingSearchInput(null, null, fixture.CourseId, null, null, null), default));
        Assert.All(byCourse.Items, b => Assert.Equal(fixture.CourseId, b.CourseId));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<Guid> CreateSlotAsync(Guid teeId, int capacity, int? maximumPlayers = null)
    {
        var data = Data;
        var slot = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .CreateTeeSlotAsync(
                new CreateGolfTeeSlotInput(teeId, GolfTestData.DefaultDate, new TimeOnly(9, 30), 10, capacity, maximumPlayers),
                data.Actor,
                default));

        return slot.Id;
    }

    /// <summary>
    /// Flattens a failure into searchable text. A <see cref="ValidationException"/> keeps
    /// the field-level detail in <c>Errors</c> rather than in the message, so the detail
    /// is appended here; otherwise the assertions would only ever see the summary line.
    /// </summary>
    private static string Flatten(Exception exception)
    {
        var text = exception.ToString();

        if (exception is ValidationException validation)
        {
            foreach (var (field, messages) in validation.Errors)
            {
                text += " | " + field + ": " + string.Join("; ", messages);
            }
        }

        var inner = exception.InnerException;
        while (inner is not null)
        {
            text += " | " + inner.Message;
            inner = inner.InnerException;
        }

        return text;
    }
}
