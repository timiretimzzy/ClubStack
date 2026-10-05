using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClubStack.IntegrationTests;

/// <summary>
/// The D7 invariants that live in the database rather than in a service: the XOR between a
/// member and a guest player, the reference formats, the state machine, the uniqueness of
/// a green fee per player, and the traceability of a posted charge.
///
/// <para>Each test builds a valid row through the production services and then corrupts
/// it with EF or SQL and asserts the constraint refuses it. A service test cannot prove
/// this: it only proves the service asked nicely. These prove that no writer, including a
/// future one, can produce the bad row.</para>
/// </summary>
[Collection("Database")]
public sealed class D7GolfDatabaseIntegrityTests : DatabaseTestBase
{
    public D7GolfDatabaseIntegrityTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private GolfTestData Data => GolfTestData.Create(Services, Fixture.ContextFactory);

    [Fact]
    public async Task A_player_cannot_be_neither_a_member_nor_a_guest()
    {
        var scenario = await ScenarioAsync();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var player = await context.GolfBookingPlayers.SingleAsync(p => p.Id == scenario.PlayerId);

        player.MemberId = null;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertCheckViolation(ex, "CK_golf_booking_players_Person");
    }

    [Fact]
    public async Task A_player_cannot_be_both_a_member_and_a_guest()
    {
        var scenario = await ScenarioAsync();
        var guestId = await Data.CreateGuestAsync(hostMemberId: scenario.MemberId);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var player = await context.GolfBookingPlayers.SingleAsync(p => p.Id == scenario.PlayerId);

        player.GolfGuestId = guestId;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertCheckViolation(ex, "CK_golf_booking_players_Person");
    }

    [Fact]
    public async Task A_guest_player_must_name_a_guest()
    {
        var scenario = await ScenarioAsync();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var player = await context.GolfBookingPlayers.SingleAsync(p => p.Id == scenario.PlayerId);

        player.PlayerType = GolfPlayerType.Guest;
        player.MemberId = null;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertCheckViolation(ex, "CK_golf_booking_players_Person");
    }

    [Fact]
    public async Task A_booking_reference_must_match_the_documented_format()
    {
        var fixture = await Data.CreateBookableAsync();
        var bookedBy = await Data.CreateMemberAsync();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        context.GolfBookings.Add(new GolfBookingEntity
        {
            Reference = "BOOKING-1",
            TeeSlotId = fixture.TeeSlotId,
            BookedByMemberId = bookedBy,
            CurrencyCode = "ZAR",
            CreatedByUserId = "d7",
            CorrelationId = "d7",
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertCheckViolation(ex, "CK_golf_bookings_Reference");
    }

    [Fact]
    public async Task A_guest_reference_must_match_the_documented_format()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        context.GolfGuestRecords.Add(new GolfGuestRecordEntity
        {
            Reference = "GUEST-1",
            FullName = "Format Test",
            Status = GolfGuestStatus.Active,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertCheckViolation(ex, "CK_golf_guest_records_Reference");
    }

    [Theory]
    [InlineData(GolfBookingStatus.Draft, GolfBookingStatus.CheckedIn)]
    [InlineData(GolfBookingStatus.Draft, GolfBookingStatus.Completed)]
    [InlineData(GolfBookingStatus.Draft, GolfBookingStatus.Cancelled)]
    [InlineData(GolfBookingStatus.Draft, GolfBookingStatus.NoShow)]
    [InlineData(GolfBookingStatus.Confirmed, GolfBookingStatus.Draft)]
    [InlineData(GolfBookingStatus.Confirmed, GolfBookingStatus.Completed)]
    [InlineData(GolfBookingStatus.CheckedIn, GolfBookingStatus.Cancelled)]
    [InlineData(GolfBookingStatus.CheckedIn, GolfBookingStatus.Draft)]
    [InlineData(GolfBookingStatus.Completed, GolfBookingStatus.Cancelled)]
    [InlineData(GolfBookingStatus.Completed, GolfBookingStatus.Confirmed)]
    [InlineData(GolfBookingStatus.Cancelled, GolfBookingStatus.Confirmed)]
    [InlineData(GolfBookingStatus.NoShow, GolfBookingStatus.Confirmed)]
    public async Task The_database_refuses_an_undocumented_transition(
        GolfBookingStatus from,
        GolfBookingStatus to)
    {
        var scenario = await DraftScenarioAsync();
        await ArrangeAsync(scenario, from);

        var ex = await RefuseAsync(scenario, to);

        AssertCheckViolation(ex);
        Assert.Contains("cannot move from", Inner(ex).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(GolfBookingStatus.Draft, GolfBookingStatus.Confirmed)]
    [InlineData(GolfBookingStatus.Confirmed, GolfBookingStatus.CheckedIn)]
    [InlineData(GolfBookingStatus.Confirmed, GolfBookingStatus.Cancelled)]
    [InlineData(GolfBookingStatus.Confirmed, GolfBookingStatus.NoShow)]
    [InlineData(GolfBookingStatus.CheckedIn, GolfBookingStatus.Completed)]
    public async Task The_database_accepts_a_documented_transition(
        GolfBookingStatus from,
        GolfBookingStatus to)
    {
        var scenario = await DraftScenarioAsync();
        await ArrangeAsync(scenario, from);

        await SetAsync(scenario, to);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(to, await context.GolfBookings.Where(b => b.Id == scenario).Select(b => b.Status).SingleAsync());
    }

    [Fact]
    public async Task A_booking_can_be_walked_to_completion()
    {
        var scenario = await DraftScenarioAsync();

        // The trigger must not be a blanket refusal: the whole Phase 3 §7.7 path has to work.
        await SetAsync(scenario, GolfBookingStatus.Confirmed);
        await SetAsync(scenario, GolfBookingStatus.CheckedIn);
        await SetAsync(scenario, GolfBookingStatus.Completed);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var booking = await context.GolfBookings.SingleAsync(b => b.Id == scenario);

        Assert.Equal(GolfBookingStatus.Completed, booking.Status);
    }

    [Fact]
    public async Task A_second_green_fee_for_the_same_player_is_impossible()
    {
        var data = Data;
        var greenFee = await data.CreateFeeProductAsync(standardAmount: 100m);
        var slot = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var confirmed = await data.ConfirmAsync(booking.Id);

        var player = Assert.Single(confirmed.Players);
        var original = Assert.Single(confirmed.Charges);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        context.GolfCharges.Add(new GolfChargeEntity
        {
            BookingId = booking.Id,
            BookingPlayerId = player.Id,
            ChargeType = GolfChargeType.GreenFee,
            Description = "Duplicate green fee",
            Amount = 100m,
            CurrencyCode = "ZAR",
            ProductId = greenFee,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal("23505", Inner(ex).SqlState);
        Assert.Contains("UX_golf_charges_PlayerGreenFee", Inner(ex).Message, StringComparison.Ordinal);

        // The refusal is total: the original charge is untouched and still the only one.
        var reloaded = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .GetBookingAsync(booking.Id, default));

        Assert.Equal(original.Id, Assert.Single(reloaded.Charges).Id);
    }

    [Fact]
    public async Task A_charged_line_cannot_lose_its_financial_traceability()
    {
        var scenario = await ScenarioAsync(greenFee: 100m);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var charge = await context.GolfCharges.SingleAsync(c => c.BookingId == scenario.BookingId);

        // XMOD-004/XMOD-005: a charge is only Charged once it carries both links, so a
        // half-posted charge cannot exist even in principle.
        charge.Status = GolfChargeStatus.Charged;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertCheckViolation(ex, "CK_golf_charges_ChargedTraceability");
    }

    [Fact]
    public async Task A_negative_fee_or_amount_is_impossible()
    {
        var scenario = await ScenarioAsync();

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var player = await context.GolfBookingPlayers.SingleAsync(p => p.Id == scenario.PlayerId);
            player.Fee = -1m;

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            AssertCheckViolation(ex, "CK_golf_booking_players_Fee");
        }

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.GolfCharges.Add(new GolfChargeEntity
            {
                BookingId = scenario.BookingId,
                ChargeType = GolfChargeType.Additional,
                Description = "Negative",
                Amount = -1m,
                CurrencyCode = "ZAR",
            });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            AssertCheckViolation(ex, "CK_golf_charges_Amount");
        }
    }

    [Fact]
    public async Task A_guest_player_is_accepted_by_the_database()
    {
        // The counterpart of the XOR refusals: the valid guest shape must insert cleanly,
        // otherwise the constraint would be refusing guests rather than corruption.
        var data = Data;
        var guestFee = await data.CreateFeeProductAsync(standardAmount: 50m);
        var slot = await data.CreateBookableAsync(guestFeeProductId: guestFee);
        var bookedBy = await data.CreateMemberAsync();
        var guestId = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(null, guestId));

        Assert.Equal(GolfPlayerType.Guest, Assert.Single(booking.Players).PlayerType);
    }

    [Fact]
    public async Task The_integrity_objects_the_migration_installs_are_all_present()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();

        var constraints = await context.Database
            .SqlQuery<string>($"""
                SELECT conname AS "Value"
                FROM pg_constraint
                WHERE conname LIKE 'CK\_golf\_%' OR conname LIKE 'CK\_tee\_slots\_%'
                """)
            .ToListAsync();

        foreach (var expected in new[]
                 {
                     "CK_golf_bookings_Reference",
                     "CK_golf_guest_records_Reference",
                     "CK_golf_booking_players_Person",
                     "CK_golf_booking_players_Position",
                     "CK_golf_booking_players_Fee",
                     "CK_golf_charges_Amount",
                     "CK_golf_charges_ChargedTraceability",
                     "CK_golf_courses_NumberOfHoles",
                     "CK_golf_tees_Capacity",
                     "CK_tee_slots_Capacity",
                     "CK_tee_slots_MaximumPlayers",
                 })
        {
            Assert.Contains(expected, constraints);
        }

        var indexes = await context.Database
            .SqlQuery<string>($"""
                SELECT indexname AS "Value"
                FROM pg_indexes
                WHERE tablename IN ('golf_charges', 'golf_bookings', 'tee_slots')
                """)
            .ToListAsync();

        Assert.Contains("UX_golf_charges_PlayerGreenFee", indexes);
        Assert.Contains("UX_golf_charges_BookingGreenFee", indexes);

        var triggers = await context.Database
            .SqlQuery<string>($"""
                SELECT tgname AS "Value"
                FROM pg_trigger
                WHERE NOT tgisinternal
                """)
            .ToListAsync();

        Assert.Contains("TR_golf_bookings_state_machine", triggers);
        Assert.Contains("TR_golf_booking_players_slot_capacity", triggers);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>A confirmed booking with one member player (and its optional green fee).</summary>
    private async Task<Scenario> ScenarioAsync(decimal? greenFee = null)
    {
        var data = Data;
        Guid? productId = null;
        if (greenFee is not null)
        {
            productId = await data.CreateFeeProductAsync(standardAmount: greenFee.Value);
        }

        var slot = await data.CreateBookableAsync(greenFeeProductId: productId);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var confirmed = await data.ConfirmAsync(booking.Id);

        return new Scenario(confirmed.Id, member, Assert.Single(confirmed.Players).Id);
    }

    /// <summary>A draft booking with one member player, so the state machine starts at its origin.</summary>
    private async Task<Guid> DraftScenarioAsync()
    {
        var fixture = await Data.CreateBookableAsync();
        var bookedBy = await Data.CreateMemberAsync();
        var member = await Data.CreateMemberAsync();

        var booking = await Data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));

        return booking.Id;
    }

    /// <summary>Walks a booking into <paramref name="status"/> using only documented edges.</summary>
    private async Task ArrangeAsync(Guid bookingId, GolfBookingStatus status)
    {
        var path = status switch
        {
            GolfBookingStatus.Draft => Array.Empty<GolfBookingStatus>(),
            GolfBookingStatus.Confirmed => [GolfBookingStatus.Confirmed],
            GolfBookingStatus.CheckedIn => [GolfBookingStatus.Confirmed, GolfBookingStatus.CheckedIn],
            GolfBookingStatus.Completed =>
                [GolfBookingStatus.Confirmed, GolfBookingStatus.CheckedIn, GolfBookingStatus.Completed],
            GolfBookingStatus.Cancelled => [GolfBookingStatus.Confirmed, GolfBookingStatus.Cancelled],
            GolfBookingStatus.NoShow => [GolfBookingStatus.Confirmed, GolfBookingStatus.NoShow],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var booking = await context.GolfBookings.SingleAsync(b => b.Id == bookingId);

        foreach (var step in path)
        {
            booking.Status = step;
            await context.SaveChangesAsync();
        }
    }

    /// <summary>Writes a status change straight to the database, past the service's guard.</summary>
    private async Task SetAsync(Guid bookingId, GolfBookingStatus status)
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var booking = await context.GolfBookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;
        await context.SaveChangesAsync();
    }

    /// <summary>As above, but asserting the database refused it.</summary>
    private async Task<DbUpdateException> RefuseAsync(Guid bookingId, GolfBookingStatus status)
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var booking = await context.GolfBookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;

        return await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static PostgresException Inner(DbUpdateException exception) =>
        (PostgresException)exception.InnerException!;

    private static void AssertCheckViolation(DbUpdateException exception, string? constraint = null)
    {
        var inner = Inner(exception);
        Assert.Equal("23514", inner.SqlState);

        if (constraint is not null)
        {
            Assert.Contains(constraint, inner.Message, StringComparison.Ordinal);
        }
    }

    private sealed record Scenario(Guid BookingId, Guid MemberId, Guid PlayerId);
}
