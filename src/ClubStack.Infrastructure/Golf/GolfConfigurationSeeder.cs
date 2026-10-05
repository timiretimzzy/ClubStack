using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Options;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// Development-only golf configuration sample, gated by the same
/// <c>App:EnableDevelopmentSeed</c> flag as the D2 development seeder.
///
/// <para>The purpose is narrow: a development database has no course, tee or tee
/// slot, so the Phase 5 §48 tee sheet would have nothing to render and a smoke test
/// could not exercise the availability path. This seeds exactly one course, its
/// holes, three tees and a short run of slots on a rolling date window.</para>
///
/// <para>It deliberately creates no member, member account, product, price,
/// inventory, financial account, payment or POS transaction. Fees are left
/// unconfigured rather than inventing catalogue data: the club configures the
/// green-fee and guest-fee products in D4, which is where products belong. A
/// booking on this sample data therefore carries a zero fee, which is the honest
/// result of an unconfigured fee, not a default price.</para>
/// </summary>
public sealed class GolfConfigurationSeeder
{
    private const string SampleCourseCode = "MAIN";

    private readonly ClubStackDbContext _context;
    private readonly AppOptions _options;
    private readonly ISystemClock _clock;

    public GolfConfigurationSeeder(
        ClubStackDbContext context,
        IOptions<AppOptions> options,
        ISystemClock clock)
    {
        _context = context;
        _options = options.Value;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableDevelopmentSeed)
        {
            return;
        }

        if (await _context.GolfCourses.AnyAsync(c => c.Code == SampleCourseCode, cancellationToken))
        {
            return;
        }

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var course = new GolfCourseEntity
        {
            Code = SampleCourseCode,
            Name = "Main Course",
            NumberOfHoles = 18,
            Status = GolfCourseStatus.Active,
            CurrencyCode = "EUR",
            Description = "Development sample course seeded by App:EnableDevelopmentSeed.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        for (var hole = 1; hole <= 18; hole++)
        {
            course.Holes.Add(new GolfHoleEntity
            {
                Course = course,
                HoleNumber = hole,
                Par = 4,
            });
        }

        AddTee(course, "CHAMP", "Championship", capacity: 4, membersOnly: false, guestsAllowed: true);
        AddTee(course, "MEMB", "Members", capacity: 4, membersOnly: true, guestsAllowed: false);
        AddTee(course, "AUTO", "Nine holes", capacity: 2, membersOnly: false, guestsAllowed: true);

        _context.GolfCourses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        await SeedSlotsAsync(course, now, today, cancellationToken);
    }

    private void AddTee(GolfCourseEntity course, string code, string name, int capacity, bool membersOnly, bool guestsAllowed)
    {
        course.Tees.Add(new GolfTeeEntity
        {
            Course = course,
            Code = code,
            Name = name,
            Capacity = capacity,
            Status = GolfTeeStatus.Active,
            MembersOnly = membersOnly,
            GuestsAllowed = guestsAllowed,
            CreatedAtUtc = course.CreatedAtUtc,
            UpdatedAtUtc = course.UpdatedAtUtc,
        });
    }

    /// <summary>
    /// Publishes a two-week rolling window of slots so the tee sheet has content on
    /// the day it is opened without seeding an unbounded calendar.
    /// </summary>
    private async Task SeedSlotsAsync(
        GolfCourseEntity course,
        DateTimeOffset now,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var teeIds = await _context.GolfTees
            .Where(t => t.CourseId == course.Id)
            .Select(t => new { t.Id, t.Capacity })
            .ToListAsync(cancellationToken);

        var added = 0;
        for (var day = 0; day < 14; day++)
        {
            var date = today.AddDays(day);

            foreach (var tee in teeIds)
            {
                for (var quarter = 0; quarter < 4; quarter++)
                {
                    _context.GolfTeeSlots.Add(new GolfTeeSlotEntity
                    {
                        CourseId = course.Id,
                        TeeId = tee.Id,
                        Date = date,
                        StartTime = new TimeOnly(7 + quarter, 0),
                        DurationMinutes = 10,
                        Capacity = tee.Capacity,
                        Status = TeeSlotStatus.Open,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                    });

                    added++;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
