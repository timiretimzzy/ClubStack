using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// Tee slots and availability: GOLF-002 (publish a tee sheet), GOLF-003
/// (tee-sheet generation), GOLF-004 (configured capacity), GOLF-005 (availability
/// queries) and the Phase 9 §12 availability inputs.
///
/// <para>Phase 3 §42 derives availability from slot capacity, bookings, closures
/// and restrictions, so this service stores the configured inputs and projects the
/// derived figures. Nothing here caches availability: every read is recomputed
/// from the live booking set, which is what makes GOLF-011's overbooking guard
/// correct rather than approximate.</para>
/// </summary>
public sealed class EntityFrameworkGolfTeeSlotService : IGolfTeeSlotService
{
    /// <summary>
    /// Guard rails on tee-sheet generation so a mistyped range cannot attempt an
    /// unbounded insert. These bound the request, not club policy.
    /// </summary>
    private const int MaxGeneratedRangeDays = 62;

    private const int MaxGeneratedSlots = 400;

    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkGolfTeeSlotService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<GolfTeeSlotSearchResult> GetTeeSheetAsync(
        GolfTeeSheetInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = GolfServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<GolfTeeSlotEntity> query = _context.GolfTeeSlots
            .AsNoTracking()
            .Where(s => s.Date == input.Date);

        if (input.CourseId is { } courseId)
        {
            query = query.Where(s => s.CourseId == courseId);
        }

        if (input.TeeId is { } teeId)
        {
            query = query.Where(s => s.TeeId == teeId);
        }

        if (input.BookableOnly)
        {
            // Operational closure/blocking is one of the Phase 9 §12 availability
            // inputs, so a blocked slot is filtered out of the bookable view.
            query = query
                .Where(s => s.Status == TeeSlotStatus.Open)
                .Where(s => s.Course.Status == GolfCourseStatus.Active)
                .Where(s => s.Tee.Status == GolfTeeStatus.Active);
        }

        var total = await query.CountAsync(cancellationToken);

        var slots = await query
            .Include(s => s.Course)
            .Include(s => s.Tee)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Tee.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var taken = await TakenBySlotAsync(slots.Select(s => s.Id), cancellationToken);

        var items = slots
            .Select(s => ToDto(s, taken.GetValueOrDefault(s.Id)))
            .ToList();

        return new GolfTeeSlotSearchResult(items, total, page, pageSize);
    }

    public async Task<GolfTeeSlotDto> GetTeeSlotAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var slot = await LoadSlotAsync(id, cancellationToken);
        var taken = await GolfServiceHelpers.TakenPlayersAsync(_context, id, null, cancellationToken);
        return ToDto(slot, taken);
    }

    public async Task<GolfTeeSlotDto> CreateTeeSlotAsync(
        CreateGolfTeeSlotInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tee = await _context.GolfTees.FirstOrDefaultAsync(t => t.Id == input.TeeId, cancellationToken)
            ?? throw new ValidationException(
                "The tee slot could not be saved.",
                GolfServiceHelpers.FieldError("teeId", $"Golf tee '{input.TeeId}' does not exist."));

        ValidateSlot(input.StartTime, input.DurationMinutes);
        var capacity = ResolveCapacity(input.Capacity, tee.Capacity);
        ValidateCapacity(capacity);

        if (await _context.GolfTeeSlots.AnyAsync(
                s => s.TeeId == input.TeeId && s.Date == input.Date && s.StartTime == input.StartTime,
                cancellationToken))
        {
            throw new ConflictException(
                $"Tee '{tee.Code}' already has a slot starting at {input.StartTime:HH\\:mm} on {input.Date:yyyy-MM-dd}.");
        }

        var now = _clock.UtcNow;
        var slot = new GolfTeeSlotEntity
        {
            CourseId = tee.CourseId,
            TeeId = tee.Id,
            Date = input.Date,
            StartTime = input.StartTime,
            DurationMinutes = input.DurationMinutes,
            Capacity = capacity,
            Status = input.Status,
            MaximumPlayers = input.MaximumPlayers,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.GolfTeeSlots.Add(slot);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetTeeSlotAsync(slot.Id, cancellationToken);
    }

    public async Task<GolfTeeSlotSearchResult> GenerateTeeSlotsAsync(
        GenerateGolfTeeSlotsInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tee = await _context.GolfTees.FirstOrDefaultAsync(t => t.Id == input.TeeId, cancellationToken)
            ?? throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError("teeId", $"Golf tee '{input.TeeId}' does not exist."));

        if (tee.Status != GolfTeeStatus.Active)
        {
            // A deactivated tee must not publish departures. The bookable view already
            // hides them, so generating here would create slots nobody could book and
            // an operator would read as a successful publish.
            throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError(
                    "teeId",
                    $"Golf tee '{input.TeeId}' is {tee.Status} and cannot publish a tee sheet."));
        }

        if (input.ToDate < input.FromDate)
        {
            throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError("toDate", "The end date cannot be before the start date."));
        }

        var dayCount = input.ToDate.DayNumber - input.FromDate.DayNumber + 1;
        if (dayCount > MaxGeneratedRangeDays)
        {
            throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError(
                    "toDate",
                    $"A tee sheet can be generated for at most {MaxGeneratedRangeDays} days at a time."));
        }

        ValidateSlot(input.FirstStartTime, input.DurationMinutes);
        ValidateInterval(input.FirstStartTime, input.LastStartTime, input.IntervalMinutes);

        var capacity = ResolveCapacity(input.Capacity, tee.Capacity);
        ValidateCapacity(capacity);

        var startTimes = BuildStartTimes(input.FirstStartTime, input.LastStartTime, input.IntervalMinutes);
        if ((long)dayCount * startTimes.Count > MaxGeneratedSlots)
        {
            throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError(
                    "intervalMinutes",
                    $"That range and interval would create more than {MaxGeneratedSlots} slots. Increase the interval or shorten the range."));
        }

        var dates = Enumerable.Range(0, dayCount).Select(offset => input.FromDate.AddDays(offset)).ToList();

        var existing = await _context.GolfTeeSlots
            .AsNoTracking()
            .Where(s => s.TeeId == tee.Id && dates.Contains(s.Date))
            .Select(s => new { s.Date, s.StartTime })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(e => (e.Date, e.StartTime))
            .ToHashSet();

        var now = _clock.UtcNow;
        var created = 0;
        foreach (var date in dates)
        {
            foreach (var startTime in startTimes)
            {
                // GOLF-002: generation fills the gaps in a published tee sheet and
                // never displaces a slot the club has already configured.
                if (!existingKeys.Add((date, startTime)))
                {
                    continue;
                }

                _context.GolfTeeSlots.Add(new GolfTeeSlotEntity
                {
                    CourseId = tee.CourseId,
                    TeeId = tee.Id,
                    Date = date,
                    StartTime = startTime,
                    DurationMinutes = input.DurationMinutes,
                    Capacity = capacity,
                    Status = TeeSlotStatus.Open,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });

                created++;
            }
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetTeeSheetAsync(
            new GolfTeeSheetInput(input.FromDate, tee.CourseId, tee.Id),
            cancellationToken);
    }

    public async Task<GolfTeeSlotDto> SetTeeSlotStatusAsync(
        Guid id,
        TeeSlotStatus status,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var slot = await _context.GolfTeeSlots.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Tee slot '{id}' was not found.");

        if (slot.Status == TeeSlotStatus.Blocked && status == TeeSlotStatus.Blocked)
        {
            // Idempotent no-op: blocking an already-blocked slot is not a change.
            return await GetTeeSlotAsync(id, cancellationToken);
        }

        slot.Status = status;
        slot.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetTeeSlotAsync(id, cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Projection
    // ---------------------------------------------------------------------

    private async Task<GolfTeeSlotEntity> LoadSlotAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.GolfTeeSlots
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Tee)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Tee slot '{id}' was not found.");
    }

    /// <summary>
    /// One grouped query for the page instead of a count per slot, so the tee sheet
    /// does not degrade into an N+1 as a day's sheet fills.
    /// </summary>
    private async Task<Dictionary<Guid, int>> TakenBySlotAsync(
        IEnumerable<Guid> slotIds,
        CancellationToken cancellationToken)
    {
        var ids = slotIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var consumingStatuses = new[]
        {
            GolfBookingStatus.Confirmed,
            GolfBookingStatus.CheckedIn,
            GolfBookingStatus.Completed,
            GolfBookingStatus.NoShow,
        };

        return await _context.GolfBookingPlayers
            .AsNoTracking()
            .Where(p => ids.Contains(p.Booking.TeeSlotId))
            .Where(p => consumingStatuses.Contains(p.Booking.Status))
            .Where(p => p.Status != GolfBookingPlayerStatus.Cancelled)
            .GroupBy(p => p.Booking.TeeSlotId)
            .Select(g => new { SlotId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SlotId, x => x.Count, cancellationToken);
    }

    internal static GolfTeeSlotDto ToDto(GolfTeeSlotEntity slot, int takenPlayers)
    {
        var effectiveCapacity = GolfDomain.RemainingCapacity(slot.Capacity, takenPlayers, 0);

        return new GolfTeeSlotDto(
            slot.Id,
            slot.CourseId,
            slot.TeeId,
            slot.Course.Code,
            slot.Course.Name,
            slot.Tee.Code,
            slot.Tee.Name,
            slot.Date,
            slot.StartTime,
            slot.DurationMinutes,
            slot.Capacity,
            takenPlayers,
            effectiveCapacity.Open,
            slot.Status,
            slot.MaximumPlayers,
            slot.Tee.MembersOnly,
            slot.Tee.GuestsAllowed,
            slot.Course.CurrencyCode,
            slot.CreatedAtUtc,
            slot.UpdatedAtUtc);
    }

    // ---------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------

    private static int ResolveCapacity(int? requested, int teeCapacity)
    {
        if (requested is { } value)
        {
            return value;
        }

        if (teeCapacity < GolfDomain.MinSlotCapacity)
        {
            throw new ValidationException(
                "The tee slot could not be saved.",
                GolfServiceHelpers.FieldError("capacity", "The tee has no usable capacity to inherit; set the slot capacity explicitly."));
        }

        return teeCapacity;
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity < GolfDomain.MinSlotCapacity || capacity > GolfDomain.MaxSlotCapacity)
        {
            throw new ValidationException(
                "The tee slot could not be saved.",
                GolfServiceHelpers.FieldError(
                    "capacity",
                    $"Slot capacity must be between {GolfDomain.MinSlotCapacity} and {GolfDomain.MaxSlotCapacity} players."));
        }
    }

    private static void ValidateSlot(TimeOnly startTime, int durationMinutes)
    {
        if (durationMinutes is < 5 or > 240)
        {
            throw new ValidationException(
                "The tee slot could not be saved.",
                GolfServiceHelpers.FieldError("durationMinutes", "A slot duration must be between 5 and 240 minutes."));
        }

        if (startTime < new TimeOnly(0, 0) || startTime > new TimeOnly(23, 45))
        {
            throw new ValidationException(
                "The tee slot could not be saved.",
                GolfServiceHelpers.FieldError("startTime", "A slot start time must fall within a single day."));
        }
    }

    private static void ValidateInterval(TimeOnly first, TimeOnly last, int intervalMinutes)
    {
        if (intervalMinutes is < 5 or > 240)
        {
            throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError("intervalMinutes", "The interval must be between 5 and 240 minutes."));
        }

        if (last < first)
        {
            throw new ValidationException(
                "The tee sheet could not be generated.",
                GolfServiceHelpers.FieldError("lastStartTime", "The last start time cannot be before the first start time."));
        }
    }

    private static List<TimeOnly> BuildStartTimes(TimeOnly first, TimeOnly last, int intervalMinutes)
    {
        var times = new List<TimeOnly>();
        var minutes = first.ToTimeSpan().TotalMinutes;
        var lastMinutes = last.ToTimeSpan().TotalMinutes;

        while (minutes <= lastMinutes)
        {
            times.Add(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)));
            minutes += intervalMinutes;
        }

        return times;
    }
}
