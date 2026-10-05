using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Application.Membership;
using ClubStack.Domain.Golf;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Tee-sheet generation and availability (GOLF-002/003/004/005, Phase 9 §12), and the
/// guest lifecycle (GOLF-008/009, Phase 3 §45).
///
/// <para>The booking suite proves a slot enforces capacity once a booking exists. This
/// suite proves the sheet itself is generated correctly, that closure removes a slot from
/// the bookable view without deleting it, and that a guest is a golf-owned record that
/// never becomes a member.</para>
/// </summary>
[Collection("Database")]
public sealed class D7GolfTeeSheetAndGuestTests : DatabaseTestBase
{
    public D7GolfTeeSheetAndGuestTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private GolfTestData Data => GolfTestData.Create(Services, Fixture.ContextFactory);

    /// <summary>
    /// The fixture's fixed booking date. Each test owns its course and tee, so several
    /// tests may generate a sheet for the same day without colliding.
    /// </summary>
    private static DateOnly SheetDate => GolfTestData.DefaultDate;

    // ---------------------------------------------------------------------
    // Tee-sheet generation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_generated_tee_sheet_covers_every_slot_in_the_window()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 4);

        var date = SheetDate;
        var firstDay = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GenerateTeeSlotsAsync(
                new GenerateGolfTeeSlotsInput(teeId, date, date.AddDays(2), new TimeOnly(8, 0), new TimeOnly(10, 0), 30, 4),
                "d7",
                default));

        // 08:00, 08:30, 09:00, 09:30 and 10:00.
        Assert.Equal(5, firstDay.Items.Count);
        Assert.Equal(5, firstDay.TotalCount);
        Assert.All(firstDay.Items, slot =>
        {
            Assert.Equal(teeId, slot.TeeId);
            Assert.Equal(4, slot.Capacity);
            Assert.Equal(TeeSlotStatus.Open, slot.Status);
            Assert.Equal(0, slot.TakenPlayers);
            Assert.Equal(4, slot.OpenPlayers);
            Assert.Equal(10, slot.DurationMinutes);
        });
        Assert.Equal(
            new[] { "08:00", "08:30", "09:00", "09:30", "10:00" },
            firstDay.Items.Select(s => s.StartTime.ToString("HH\\:mm")).ToArray());

        // The sheet read model is one day at a time, matching GET /api/v1/golf/tee-sheet,
        // so the other two generated days are read the same way a caller would.
        for (var offset = 1; offset <= 2; offset++)
        {
            var day = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
                .GetTeeSheetAsync(new GolfTeeSheetInput(date.AddDays(offset), courseId, teeId), default));

            Assert.Equal(date.AddDays(offset), day.Items.Select(s => s.Date).Distinct().Single());
            Assert.Equal(5, day.Items.Count);
        }

        // Re-running the same window is idempotent: it fills gaps, it does not duplicate.
        var again = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GenerateTeeSlotsAsync(
                new GenerateGolfTeeSlotsInput(teeId, date, date.AddDays(2), new TimeOnly(8, 0), new TimeOnly(10, 0), 30, 4),
                "d7",
                default));

        Assert.Equal(5, again.Items.Count);
    }

    [Fact]
    public async Task A_generated_tee_sheet_never_overlaps_itself()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 4);

        var date = SheetDate;
        var result = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GenerateTeeSlotsAsync(
                new GenerateGolfTeeSlotsInput(teeId, date, date, new TimeOnly(9, 0), new TimeOnly(9, 30), 30, 4, 30),
                "d7",
                default));

        // 30-minute starts with a 30-minute duration abut exactly; they may not overlap.
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(new[] { new TimeOnly(9, 0), new TimeOnly(9, 30) }, result.Items.Select(s => s.StartTime).ToArray());
    }

    [Fact]
    public async Task A_generated_tee_sheet_keeps_the_tees_own_capacity_and_rules()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 3, membersOnly: true, guestsAllowed: false);

        var date = SheetDate;
        var result = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GenerateTeeSlotsAsync(
                new GenerateGolfTeeSlotsInput(teeId, date, date, new TimeOnly(8, 0), new TimeOnly(9, 0), 60, null),
                "d7",
                default));

        // A null capacity on the request means "use the tee's own", not "unlimited".
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, slot =>
        {
            Assert.Equal(3, slot.Capacity);
            Assert.True(slot.MembersOnly);
            Assert.False(slot.GuestsAllowed);
            Assert.Equal(3, slot.OpenPlayers);
        });
    }

    [Fact]
    public async Task A_tee_sheet_window_that_would_be_unmanageable_is_refused()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 4);

        var date = SheetDate;
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
                .GenerateTeeSlotsAsync(
                    new GenerateGolfTeeSlotsInput(teeId, date, date.AddDays(20), new TimeOnly(8, 0), new TimeOnly(18, 0), 30, 4),
                    "d7",
                    default)));

        Assert.Contains("intervalMinutes", string.Join(" ", ex.Errors.Select(e => e.Key)));
    }

    [Fact]
    public async Task A_tee_sheet_window_longer_than_the_permitted_range_is_refused()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 4);

        var date = SheetDate;
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
                .GenerateTeeSlotsAsync(
                    new GenerateGolfTeeSlotsInput(teeId, date, date.AddDays(90), new TimeOnly(8, 0), new TimeOnly(9, 0), 60, 4),
                    "d7",
                    default)));

        Assert.Contains("toDate", string.Join(" ", ex.Errors.Select(e => e.Key)));
    }

    [Fact]
    public async Task A_tee_sheet_cannot_be_generated_for_an_inactive_tee()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 4);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfCourseService>()
            .SetTeeStatusAsync(teeId, GolfTeeStatus.Inactive, "d7", default));

        var date = SheetDate;
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
                .GenerateTeeSlotsAsync(
                    new GenerateGolfTeeSlotsInput(teeId, date, date, new TimeOnly(8, 0), new TimeOnly(9, 0), 60, 4),
                    "d7",
                    default)));

        Assert.Contains("teeId", string.Join(" ", ex.Errors.Select(e => e.Key)));

        // Nothing was published behind the refusal.
        var sheet = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSheetAsync(new GolfTeeSheetInput(date, courseId, teeId), default));

        Assert.Empty(sheet.Items);
    }

    [Fact]
    public async Task A_hand_created_slot_joins_the_sheet_with_its_own_override()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var teeId = await data.CreateTeeAsync(courseId, capacity: 4);

        var date = SheetDate;
        var created = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .CreateTeeSlotAsync(
                new CreateGolfTeeSlotInput(teeId, date, new TimeOnly(7, 0), 10, 2, 2),
                "d7",
                default));

        // The slot lowered the tee player ceiling for this one departure only.
        Assert.Equal(2, created.Capacity);
        Assert.Equal(2, created.MaximumPlayers);
        Assert.Equal(2, created.OpenPlayers);

        var sheet = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSheetAsync(new GolfTeeSheetInput(date, courseId, teeId), default));

        Assert.Equal(created.Id, Assert.Single(sheet.Items).Id);
    }

    // ---------------------------------------------------------------------
    // Availability and closure
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Availability_is_derived_from_confirmed_bookings_and_reacts_to_closure()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 2);
        var bookedBy = await data.CreateMemberAsync();
        var members = await data.CreateMembersAsync(2);

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[0], null));
        await data.ConfirmAsync(booking.Id);

        var open = await data.SlotAsync(slot.TeeSlotId);
        Assert.Equal(1, open.TakenPlayers);
        Assert.Equal(1, open.OpenPlayers);

        // A draft on the same slot does not move the numbers, so a desk holding a booking
        // does not shrink the sheet for everyone else.
        var held = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(members[1], null));
        Assert.Equal(1, (await data.SlotAsync(slot.TeeSlotId)).TakenPlayers);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .SetTeeSlotStatusAsync(slot.TeeSlotId, TeeSlotStatus.Blocked, "d7", default));

        var blocked = await data.SlotAsync(slot.TeeSlotId);
        Assert.Equal(TeeSlotStatus.Blocked, blocked.Status);

        var bookable = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSheetAsync(new GolfTeeSheetInput(blocked.Date, slot.CourseId, slot.TeeId, BookableOnly: true), default));

        Assert.DoesNotContain(bookable.Items, s => s.Id == slot.TeeSlotId);

        // Reopening the slot restores it to the bookable view.
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .SetTeeSlotStatusAsync(slot.TeeSlotId, TeeSlotStatus.Open, "d7", default));

        var reopened = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSheetAsync(new GolfTeeSheetInput(blocked.Date, slot.CourseId, slot.TeeId, BookableOnly: true), default));

        Assert.Contains(reopened.Items, s => s.Id == slot.TeeSlotId);

        // The held draft is still held, and the slot is still only one place short.
        Assert.Equal(GolfBookingStatus.Draft, held.Status);
        Assert.Equal(1, (await data.SlotAsync(slot.TeeSlotId)).OpenPlayers);
    }

    [Fact]
    public async Task A_full_slot_reports_no_open_places_and_stays_bookable()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 1);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        var full = await data.SlotAsync(slot.TeeSlotId);
        Assert.Equal(1, full.TakenPlayers);
        Assert.Equal(0, full.OpenPlayers);

        // Full is not the same as closed: the slot is still on the sheet, with no room.
        var bookable = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSheetAsync(new GolfTeeSheetInput(full.Date, slot.CourseId, slot.TeeId, BookableOnly: true), default));

        var shown = Assert.Single(bookable.Items, s => s.Id == slot.TeeSlotId);
        Assert.Equal(0, shown.OpenPlayers);
    }

    [Fact]
    public async Task A_cancellation_returns_the_places_to_the_sheet()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync(capacity: 2);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        var confirmed = await data.ConfirmAsync(booking.Id);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CancelBookingAsync(confirmed.Id, new CancelGolfBookingInput("Weather"), "d7", default));

        var reopened = await data.SlotAsync(slot.TeeSlotId);
        Assert.Equal(0, reopened.TakenPlayers);
        Assert.Equal(2, reopened.OpenPlayers);
    }

    [Fact]
    public async Task A_tee_sheet_can_be_read_across_every_tee_of_a_course_for_a_day()
    {
        var data = Data;
        var courseId = await data.CreateCourseAsync();
        var morning = await data.CreateTeeAsync(courseId, capacity: 4);
        var afternoon = await data.CreateTeeAsync(courseId, capacity: 2);

        var date = SheetDate;
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GenerateTeeSlotsAsync(
                new GenerateGolfTeeSlotsInput(morning, date, date, new TimeOnly(8, 0), new TimeOnly(9, 0), 60, null),
                "d7",
                default));
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GenerateTeeSlotsAsync(
                new GenerateGolfTeeSlotsInput(afternoon, date, date, new TimeOnly(13, 0), new TimeOnly(14, 0), 60, null),
                "d7",
                default));

        var sheet = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSheetAsync(new GolfTeeSheetInput(date, courseId, null), default));

        Assert.Equal(4, sheet.Items.Count);
        Assert.Equal(2, sheet.Items.Count(s => s.TeeId == morning));
        Assert.Equal(2, sheet.Items.Count(s => s.TeeId == afternoon));
        Assert.Equal(4, sheet.Items.Where(s => s.TeeId == morning).Select(s => s.Capacity).Distinct().Single());
        Assert.Equal(2, sheet.Items.Where(s => s.TeeId == afternoon).Select(s => s.Capacity).Distinct().Single());
    }

    // ---------------------------------------------------------------------
    // Guests
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_guest_is_a_golf_record_with_a_gst_reference()
    {
        var data = Data;
        var host = await data.CreateMemberAsync();

        var guest = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .CreateGuestAsync(new CreateGolfGuestInput("Walking Visitor", "082 555 0100", host, "Plays on Fridays"), "d7", default));

        Assert.StartsWith("GST-", guest.Reference);
        Assert.Equal(10, guest.Reference.Length);
        Assert.Equal("Walking Visitor", guest.FullName);
        Assert.Equal(host, guest.HostMemberId);
        Assert.Equal(GolfGuestStatus.Active, guest.Status);
        Assert.Equal(0, guest.VisitCount);
        Assert.Null(guest.LastVisitedAtUtc);

        // A guest is a golf-owned record, not a D2 member: nothing about booking one
        // created a person or a member in the membership directory.
        var members = await data.WithScopeAsync(sp => sp.GetRequiredService<IMembershipDirectoryService>()
            .SearchMembersAsync(new MemberSearchInput(guest.FullName), default));
        var persons = await data.WithScopeAsync(sp => sp.GetRequiredService<IMembershipDirectoryService>()
            .SearchPersonsAsync(new PersonSearchInput(guest.FullName), default));

        Assert.Empty(members.Items);
        Assert.Empty(persons.Items);
    }

    [Fact]
    public async Task Two_guests_of_the_same_name_get_different_references()
    {
        var data = Data;

        var first = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .CreateGuestAsync(new CreateGolfGuestInput("Repeat Visitor", null, null, null), "d7", default));
        var second = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .CreateGuestAsync(new CreateGolfGuestInput("Repeat Visitor", null, null, null), "d7", default));

        // The reference identifies the record, so two people who share a name are still
        // two records with two references.
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Reference, second.Reference);
    }

    [Fact]
    public async Task A_guest_cannot_be_created_without_a_name()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
                .CreateGuestAsync(new CreateGolfGuestInput("   ", null, null, null), "d7", default)));

        Assert.Contains("fullName", string.Join(" ", ex.Errors.Select(e => e.Key)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_guest_visit_is_recorded_when_a_booking_completes()
    {
        var data = Data;
        var guestFee = await data.CreateFeeProductAsync(standardAmount: 50m);
        var slot = await data.CreateBookableAsync(guestFeeProductId: guestFee);
        var bookedBy = await data.CreateMemberAsync();
        var guestId = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var before = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .GetGuestAsync(guestId, default));
        Assert.Equal(0, before.VisitCount);

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(null, guestId));
        var confirmed = await data.ConfirmAsync(booking.Id);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CheckInBookingAsync(confirmed.Id, "d7", default));
        var completed = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CompleteBookingAsync(confirmed.Id, "d7", default));

        Assert.Equal(GolfBookingStatus.Completed, completed.Status);
        Assert.Equal(GolfBookingPlayerStatus.CheckedIn, completed.Players.Single().Status);

        var after = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .GetGuestAsync(guestId, default));

        Assert.Equal(1, after.VisitCount);
        Assert.NotNull(after.LastVisitedAtUtc);
    }

    [Fact]
    public async Task A_cancelled_visit_is_not_counted_as_a_visit()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var guestId = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var booking = await data.CreateBookingAsync(
            slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(null, guestId));
        var confirmed = await data.ConfirmAsync(booking.Id);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CancelBookingAsync(confirmed.Id, new CancelGolfBookingInput("Guest did not arrive"), "d7", default));

        var guest = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .GetGuestAsync(guestId, default));

        Assert.Equal(0, guest.VisitCount);
        Assert.Null(guest.LastVisitedAtUtc);
    }

    [Fact]
    public async Task An_inactive_guest_cannot_be_booked_but_the_history_remains()
    {
        var data = Data;
        var slot = await data.CreateBookableAsync();
        var bookedBy = await data.CreateMemberAsync();
        var guestId = await data.CreateGuestAsync(hostMemberId: bookedBy);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .SetGuestStatusAsync(guestId, GolfGuestStatus.Inactive, "d7", default));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => data.CreateBookingAsync(slot.TeeSlotId, bookedBy, new GolfBookingPlayerInput(null, guestId)));

        Assert.Contains("Guest", string.Join(" ", ex.Errors.Select(e => string.Join(" ", e.Value))), StringComparison.OrdinalIgnoreCase);

        // Deactivating a guest is not deleting them: the record and its visit count remain.
        var guest = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .GetGuestAsync(guestId, default));

        Assert.Equal(GolfGuestStatus.Inactive, guest.Status);
        Assert.Equal(0, guest.VisitCount);
    }

    [Fact]
    public async Task Guests_can_be_searched_by_name_and_status()
    {
        var data = Data;
        var marker = UniquePrefix("GST");

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .CreateGuestAsync(new CreateGolfGuestInput($"Ada {marker}", null, null, null), "d7", default));
        var retired = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .CreateGuestAsync(new CreateGolfGuestInput($"Bo {marker}", null, null, null), "d7", default));

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .SetGuestStatusAsync(retired.Id, GolfGuestStatus.Inactive, "d7", default));

        var byName = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .SearchGuestsAsync(new GolfGuestSearchInput(marker, null), default));
        Assert.Equal(2, byName.Items.Count);

        var activeOnly = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
            .SearchGuestsAsync(new GolfGuestSearchInput(marker, GolfGuestStatus.Active), default));
        Assert.Contains(marker, Assert.Single(activeOnly.Items).FullName);
    }

    [Fact]
    public async Task An_unknown_guest_is_reported_as_not_found()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => Data.WithScopeAsync(sp => sp.GetRequiredService<IGolfGuestService>()
                .GetGuestAsync(Guid.CreateVersion7(), default)));

        Assert.Contains("Guest", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
