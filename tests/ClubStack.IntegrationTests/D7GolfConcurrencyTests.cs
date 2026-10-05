using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D7 concurrency against real PostgreSQL: GOLF-010/011, where the capacity check and the
/// write that consumes capacity must be one atomic act.
///
/// <para>Each test drives two genuinely concurrent commands from two independent scopes
/// against the same tee slot. This is the only kind of test that can prove the
/// <c>SELECT ... FOR UPDATE</c> lock and its transaction actually serialise the two
/// writers; a sequential test would pass even without the lock.</para>
/// </summary>
[Collection("Database")]
public sealed class D7GolfConcurrencyTests : DatabaseTestBase
{
    public D7GolfConcurrencyTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private GolfTestData Data => GolfTestData.Create(Services, Fixture.ContextFactory);

    [Fact]
    public async Task Two_desks_racing_for_the_last_place_produce_exactly_one_booking()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var first = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        var second = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));

        // Both desks press "confirm" at the same time for the one remaining place.
        var outcomes = await RaceAsync(
            () => data.ConfirmAsync(first.Id),
            () => data.ConfirmAsync(second.Id));

        Assert.Single(outcomes.Won);
        Assert.Single(outcomes.Lost);

        var failure = Assert.IsType<ConflictException>(outcomes.Lost.Single().Exception);
        Assert.Contains("open place(s)", failure.Message, StringComparison.OrdinalIgnoreCase);

        // The invariant: the slot never holds more than its capacity, whichever order the
        // two commands actually ran in.
        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(1, slot.TakenPlayers);
        Assert.Equal(0, slot.OpenPlayers);

        var confirmed = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .SearchBookingsAsync(
                new GolfBookingSearchInput(null, null, fixture.CourseId, fixture.TeeId, null, GolfBookingStatus.Confirmed),
                default));

        Assert.Single(confirmed.Items);
    }

    [Fact]
    public async Task Concurrent_confirmations_that_both_fit_are_both_accepted()
    {
        var data = Data;
        var fixture = await data.CreateBookableAsync(capacity: 4);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(4);

        var first = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[0], null),
            new GolfBookingPlayerInput(members[1], null));
        var second = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(members[2], null),
            new GolfBookingPlayerInput(members[3], null));

        var outcomes = await RaceAsync(
            () => data.ConfirmAsync(first.Id),
            () => data.ConfirmAsync(second.Id));

        // The lock must not be a blanket refusal: when there is room for both, both win.
        Assert.Empty(outcomes.Lost);

        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(4, slot.TakenPlayers);
        Assert.Equal(0, slot.OpenPlayers);
    }

    [Fact]
    public async Task The_database_trigger_refuses_overbooking_even_without_the_service()
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

        // Bypass the service entirely and write the two statements a desk's confirmation
        // would produce. The capacity trigger counts on the player rows, so the booking is
        // made capacity-consuming first and the player row is then touched. The service is
        // not the only thing standing between two desks and a double booking, so the storage
        // layer has to refuse too.
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE golf_bookings SET \"Status\" = {(int)GolfBookingStatus.Confirmed}, \"ConfirmedAtUtc\" = now() WHERE \"Id\" = {second.Id}");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE golf_booking_players SET \"Fee\" = \"Fee\" WHERE \"BookingId\" = {second.Id}"));

        Assert.Equal("23514", ex.SqlState);
        Assert.Contains("overbooking is not permitted", ex.Message, StringComparison.OrdinalIgnoreCase);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task The_database_trigger_allows_an_overbooking_that_records_an_override()
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
        Assert.True(overridden.CapacityOverridden);

        var slot = await data.SlotAsync(fixture.TeeSlotId);
        Assert.Equal(2, slot.TakenPlayers);

        // The trigger permits the overflow only while the whole override record is present,
        // so stripping one field out of it and re-touching the players loses the place.
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE golf_bookings SET \"OverrideReason\" = NULL WHERE \"Id\" = {second.Id}");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE golf_booking_players SET \"Fee\" = \"Fee\" WHERE \"BookingId\" = {second.Id}"));

        Assert.Contains("overbooking is not permitted", ex.Message, StringComparison.OrdinalIgnoreCase);

        await transaction.RollbackAsync();
    }

    /// <summary>
    /// Runs two commands that must overlap in time. Both are started before either is
    /// awaited, and the gate guarantees both have reached the database before either is
    /// allowed to finish, so the two transactions genuinely contend.
    /// </summary>
    private static async Task<RaceOutcome> RaceAsync(Func<Task<GolfBookingDto>> left, Func<Task<GolfBookingDto>> right)
    {
        var gate = new SemaphoreSlim(0, 2);
        var ready = new CountdownEvent(2);

        async Task<(GolfBookingDto? Result, Exception? Exception)> Run(Func<Task<GolfBookingDto>> command)
        {
            ready.Signal();
            await gate.WaitAsync();

            try
            {
                return (await command(), null);
            }
            catch (Exception ex)
            {
                return (null, ex);
            }
        }

        var leftTask = Task.Run(() => Run(left));
        var rightTask = Task.Run(() => Run(right));

        ready.Wait();
        gate.Release(2);

        var results = await Task.WhenAll(leftTask, rightTask);

        return new RaceOutcome(
            results.Where(r => r.Result is not null).ToList(),
            results.Where(r => r.Exception is not null).ToList());
    }

    private sealed record RaceOutcome(
        IReadOnlyList<(GolfBookingDto? Result, Exception? Exception)> Won,
        IReadOnlyList<(GolfBookingDto? Result, Exception? Exception)> Lost);

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
