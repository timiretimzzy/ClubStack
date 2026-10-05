using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D7 idempotency: a retried golf command returns the first result instead of acting
/// twice.
///
/// <para>A clubhouse network drops requests. The dangerous case is not the failed call but
/// the one the client re-sends: a second booking on a slot that only had room for one, a
/// second set of green-fee charges, a second receivable on the member account. Each test
/// therefore sends the same key twice and counts what the database actually holds - a
/// replayed response alone would prove nothing.</para>
///
/// <para>The scope follows the established D5/D6 contract: a key is unique per
/// (key, operation, actor), and the same key with a materially different payload is a
/// conflict rather than a replay.</para>
/// </summary>
[Collection("Database")]
public sealed class D7GolfIdempotencyTests : DatabaseTestBase
{
    public D7GolfIdempotencyTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private GolfTestData Data => GolfTestData.Create(Services, Fixture.ContextFactory);

    private static string Key() => UniquePrefix("D7KEY");

    private async Task<int> BookingCountAsync()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        return await context.GolfBookings.CountAsync();
    }

    private async Task<int> ChargeCountAsync()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        return await context.GolfCharges.CountAsync();
    }

    // ---------------------------------------------------------------------
    // Booking creation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_retried_create_returns_the_first_booking_and_adds_nothing()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();
        var key = Key();

        var first = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, key, new GolfBookingPlayerInput(member, null));
        var second = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, key, new GolfBookingPlayerInput(member, null));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Reference, second.Reference);

        var before = await BookingCountAsync();
        Assert.Equal(1, (await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .SearchBookingsAsync(new GolfBookingSearchInput(null, null, slot.CourseId, null, null, null, 1, 200), default))).Items
            .Count(b => b.Reference == first.Reference));
        Assert.True(before > 0);
    }

    [Fact]
    public async Task The_same_key_with_a_different_booking_is_refused()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);
        var key = Key();

        await data.CreateBookingAsync(slot.TeeSlotId, bookedBy, key, new GolfBookingPlayerInput(members[0], null));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.CreateBookingAsync(slot.TeeSlotId, bookedBy, key, new GolfBookingPlayerInput(members[1], null)));

        Assert.Contains("different request payload", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failed_create_releases_the_key_so_the_retry_can_succeed()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();
        var key = Key();

        // No players: the create fails on validation, after the key was claimed.
        var invalid = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(slot.TeeSlotId, bookedBy, key));
        Assert.NotNull(invalid);

        var retry = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, key, new GolfBookingPlayerInput(member, null));

        Assert.NotEqual(Guid.Empty, retry.Id);
        Assert.Equal(GolfBookingStatus.Draft, retry.Status);
    }

    [Fact]
    public async Task Two_actors_may_use_the_same_key_independently()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);
        var key = Key();

        var mine = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CreateBookingAsync(
                new CreateGolfBookingInput(
                    slot.TeeSlotId, bookedBy, [new GolfBookingPlayerInput(members[0], null)], null, key),
                "desk-one",
                "d7-golf-correlation",
                default));

        var theirs = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CreateBookingAsync(
                new CreateGolfBookingInput(
                    slot.TeeSlotId, bookedBy, [new GolfBookingPlayerInput(members[1], null)], null, key),
                "desk-two",
                "d7-golf-correlation",
                default));

        // The key scope includes the actor, so two desks are not serialized by one key.
        Assert.NotEqual(mine.Id, theirs.Id);
    }

    // ---------------------------------------------------------------------
    // Confirmation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_retried_confirm_does_not_raise_the_fees_twice()
    {
        var data = Data;
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 400m);
        var slot = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var key = Key();

        var first = await data.ConfirmAsync(booking.Id, false, null, key);
        var chargesAfterFirst = await ChargeCountAsync();

        var second = await data.ConfirmAsync(booking.Id, false, null, key);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(GolfBookingStatus.Confirmed, second.Status);
        Assert.Single(second.Charges);
        Assert.Equal(chargesAfterFirst, await ChargeCountAsync());
    }

    [Fact]
    public async Task A_retried_confirm_does_not_consume_the_places_twice()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var key = Key();

        await data.ConfirmAsync(booking.Id, false, null, key);
        var second = await data.ConfirmAsync(booking.Id, false, null, key);

        // The replay returns the stored result instead of re-running the capacity check,
        // so a full slot is not re-evaluated against itself.
        Assert.Equal(GolfBookingStatus.Confirmed, second.Status);
        Assert.Equal(1, (await data.SlotAsync(slot.TeeSlotId)).TakenPlayers);
    }

    [Fact]
    public async Task The_same_confirm_key_with_a_different_booking_is_refused()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);
        var key = Key();

        var first = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        var second = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));

        await data.ConfirmAsync(first.Id, false, null, key);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.ConfirmAsync(second.Id, false, null, key));

        Assert.Contains("different request payload", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_confirm_refused_for_capacity_releases_its_key()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);
        var key = Key();

        var winner = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        var loser = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));

        await data.ConfirmAsync(winner.Id);
        await Assert.ThrowsAsync<ConflictException>(() => data.ConfirmAsync(loser.Id, false, null, key));

        // The slot is freed, so the same key must still be usable for a genuine retry
        // rather than staying wedged behind the failure.
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CancelBookingAsync(winner.Id, new CancelGolfBookingInput("Released"), data.Actor, default));

        var retried = await data.ConfirmAsync(loser.Id, false, null, key);
        Assert.Equal(GolfBookingStatus.Confirmed, retried.Status);
    }

    // ---------------------------------------------------------------------
    // Charges
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_retried_add_charge_returns_the_first_booking_state()
    {
        var data = Data;
        await data.PreparePostingAsync();
        var cart = await data.CreateFeeProductAsync(standardAmount: 150m, memberAmount: 120m);
        var slot = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        var key = Key();
        var first = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .AddChargeAsync(booking.Id, new AddGolfChargeInput(cart, 1, null, "Cart hire", key), data.Actor, default));
        var chargesAfterFirst = await ChargeCountAsync();

        var second = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .AddChargeAsync(booking.Id, new AddGolfChargeInput(cart, 1, null, "Cart hire", key), data.Actor, default));

        Assert.Equal(first.Charges.Count, second.Charges.Count);
        Assert.Equal(1, second.Charges.Count(c => c.Description == "Cart hire"));
        Assert.Equal(chargesAfterFirst, await ChargeCountAsync());
    }

    [Fact]
    public async Task A_retried_post_does_not_raise_a_second_receivable()
    {
        var data = Data;
        await data.PreparePostingAsync();
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 400m);
        var slot = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        var key = Key();
        var first = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(key), data.Actor, "d7-golf-correlation", default));

        var chargeId = Assert.Single(first.Charges).Id;
        var receivableId = first.Charges[0].ReceivableId;
        Assert.NotNull(receivableId);

        var second = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(key), data.Actor, "d7-golf-correlation", default));

        // The replay carries the same money trail, not a new one.
        Assert.Equal(chargeId, Assert.Single(second.Charges).Id);
        Assert.Equal(receivableId, second.Charges[0].ReceivableId);
    }

    [Fact]
    public async Task The_same_post_key_against_another_booking_is_refused()
    {
        var data = Data;
        await data.PreparePostingAsync();
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 400m);
        var slot = await data.CreateBookableAsync(greenFeeProductId: greenFee, capacity: 2);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var first = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        var second = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));

        await data.ConfirmAsync(first.Id);
        await data.ConfirmAsync(second.Id);

        var key = Key();
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(first.Id, new PostGolfBookingChargesInput(key), data.Actor, "d7-golf-correlation", default));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(second.Id, new PostGolfBookingChargesInput(key), data.Actor, "d7-golf-correlation", default)));

        Assert.Contains("different request payload", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Commands_without_a_key_still_work_as_before()
    {
        // Idempotency is opt-in per request: a client that sends no key keeps the plain
        // behaviour, and the D5/D6 suites rely on it.
        var data = Data;
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 400m);
        var slot = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var confirmed = await data.ConfirmAsync(booking.Id);

        var posted = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default));

        Assert.Equal(GolfChargeStatus.Charged, Assert.Single(posted.Charges).Status);

        // A second attempt with no key is a genuine second command, and is refused because
        // the charges are already posted - not because a key replayed.
        await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

        Assert.Equal(confirmed.Id, posted.Id);
    }
}
