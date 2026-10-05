using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// GOLF-001 (configure courses), Phase 3 §40/§41 (holes and tees) and GOLF-012
/// (configured member/guest/player-count restrictions on the tee).
///
/// <para>Fees are configuration, not code: a tee points at D4 catalogue products
/// whose prices the club maintains, so GOLF-016 is satisfied without a golf price
/// table and without a hard-coded amount anywhere.</para>
/// </summary>
public sealed class EntityFrameworkGolfCourseService : IGolfCourseService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkGolfCourseService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<GolfCourseSearchResult> SearchCoursesAsync(
        GolfCourseSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = GolfServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<GolfCourseEntity> query = _context.GolfCourses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var code = input.Code.Trim();
            query = query.Where(c => EF.Functions.ILike(c.Code, code) || EF.Functions.ILike(c.Name, code));
        }

        if (input.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var courses = await query
            .OrderBy(c => c.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<GolfCourseDto>(courses.Count);
        foreach (var course in courses)
        {
            items.Add(await ProjectAsync(course, cancellationToken));
        }

        return new GolfCourseSearchResult(items, total, page, pageSize);
    }

    public async Task<GolfCourseDto> GetCourseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _context.GolfCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Golf course '{id}' was not found.");

        return await ProjectAsync(course, cancellationToken);
    }

    public async Task<GolfCourseDto> CreateCourseAsync(
        CreateGolfCourseInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var code = RequireCode(input.Code, "code");

        if (await _context.GolfCourses.AnyAsync(c => c.Code == code, cancellationToken))
        {
            throw new ConflictException($"A golf course with code '{code}' already exists.");
        }

        var holes = ValidateHoles(input.NumberOfHoles, input.Holes);

        var course = new GolfCourseEntity
        {
            Code = code,
            Name = RequireName(input.Name, "name"),
            NumberOfHoles = input.NumberOfHoles,
            Status = GolfCourseStatus.Active,
            CurrencyCode = NormalizeCurrency(input.CurrencyCode),
            Description = Trim(input.Description),
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        course.Holes = holes
            .Select(h => new GolfHoleEntity
            {
                Course = course,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Name = Trim(h.Name),
            })
            .ToList();

        _context.GolfCourses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetCourseAsync(course.Id, cancellationToken);
    }

    public async Task<GolfCourseDto> SetCourseStatusAsync(
        Guid id,
        GolfCourseStatus status,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.GolfCourses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Golf course '{id}' was not found.");

        course.Status = status;
        course.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetCourseAsync(id, cancellationToken);
    }

    public async Task<GolfTeeDto> CreateTeeAsync(
        Guid courseId,
        CreateGolfTeeInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.GolfCourses.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken)
            ?? throw new NotFoundException($"Golf course '{courseId}' was not found.");

        var code = RequireCode(input.Code, "code");

        if (await _context.GolfTees.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new ConflictException($"A golf tee with code '{code}' already exists.");
        }

        await EnsureFeeProductsAsync(input, cancellationToken);
        ValidateTeeShape(input.Capacity, input.MinimumPlayers, input.MaximumPlayers, code);

        var tee = new GolfTeeEntity
        {
            CourseId = course.Id,
            Code = code,
            Name = RequireName(input.Name, "name"),
            Capacity = input.Capacity,
            Status = GolfTeeStatus.Active,
            MembersOnly = input.MembersOnly,
            GuestsAllowed = input.GuestsAllowed,
            MinimumPlayers = input.MinimumPlayers,
            MaximumPlayers = input.MaximumPlayers,
            GreenFeeProductId = input.GreenFeeProductId,
            GuestFeeProductId = input.GuestFeeProductId,
            Notes = Trim(input.Notes),
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        _context.GolfTees.Add(tee);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetTeeAsync(tee.Id, cancellationToken);
    }

    public async Task<GolfTeeDto> UpdateTeeAsync(
        Guid teeId,
        CreateGolfTeeInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tee = await _context.GolfTees.FirstOrDefaultAsync(t => t.Id == teeId, cancellationToken)
            ?? throw new NotFoundException($"Golf tee '{teeId}' was not found.");

        var code = RequireCode(input.Code, "code");

        if (await _context.GolfTees.AnyAsync(t => t.Code == code && t.Id != teeId, cancellationToken))
        {
            throw new ConflictException($"A golf tee with code '{code}' already exists.");
        }

        await EnsureFeeProductsAsync(input, cancellationToken);
        ValidateTeeShape(input.Capacity, input.MinimumPlayers, input.MaximumPlayers, code);

        tee.Code = code;
        tee.Name = RequireName(input.Name, "name");
        tee.Capacity = input.Capacity;
        tee.MembersOnly = input.MembersOnly;
        tee.GuestsAllowed = input.GuestsAllowed;
        tee.MinimumPlayers = input.MinimumPlayers;
        tee.MaximumPlayers = input.MaximumPlayers;
        tee.GreenFeeProductId = input.GreenFeeProductId;
        tee.GuestFeeProductId = input.GuestFeeProductId;
        tee.Notes = Trim(input.Notes);
        tee.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetTeeAsync(teeId, cancellationToken);
    }

    public async Task SetTeeStatusAsync(
        Guid teeId,
        GolfTeeStatus status,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tee = await _context.GolfTees.FirstOrDefaultAsync(t => t.Id == teeId, cancellationToken)
            ?? throw new NotFoundException($"Golf tee '{teeId}' was not found.");

        tee.Status = status;
        tee.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads one tee. Exposed because tees are addressed by their own id in the
    /// Phase 8 §11 tee commands (<c>PUT tees/{id}</c>, tee status, and the tee a slot
    /// belongs to), so the client needs to resolve a tee without loading the whole course.
    /// </summary>
    public async Task<GolfTeeDto> GetTeeAsync(Guid teeId, CancellationToken cancellationToken = default)
    {
        var tee = await _context.GolfTees
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teeId, cancellationToken)
            ?? throw new NotFoundException($"Golf tee '{teeId}' was not found.");

        return ToTeeDto(tee);
    }

    private async Task<GolfCourseDto> ProjectAsync(GolfCourseEntity course, CancellationToken cancellationToken)
    {
        var tees = await _context.GolfTees
            .AsNoTracking()
            .Where(t => t.CourseId == course.Id)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);

        var holes = await _context.GolfHoles
            .AsNoTracking()
            .Where(h => h.CourseId == course.Id)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync(cancellationToken);

        return new GolfCourseDto(
            course.Id,
            course.Code,
            course.Name,
            course.NumberOfHoles,
            course.Status,
            course.CurrencyCode,
            course.Description,
            course.CreatedAtUtc,
            course.UpdatedAtUtc,
            holes.Select(h => new GolfHoleDto(h.Id, h.CourseId, h.HoleNumber, h.Par, h.Yardage, h.Name)).ToList(),
            tees.Select(ToTeeDto).ToList());
    }

    internal static GolfTeeDto ToTeeDto(GolfTeeEntity tee) => new(
        tee.Id,
        tee.CourseId,
        tee.Code,
        tee.Name,
        tee.Capacity,
        tee.Status,
        tee.MembersOnly,
        tee.GuestsAllowed,
        tee.MinimumPlayers,
        tee.MaximumPlayers,
        tee.GreenFeeProductId,
        tee.GuestFeeProductId,
        tee.Notes,
        tee.CreatedAtUtc,
        tee.UpdatedAtUtc);

    // ---------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------

    private static string RequireCode(string value, string field)
    {
        var code = value?.Trim() ?? string.Empty;
        if (code.Length is 0 or > 32)
        {
            throw new ValidationException(
                "The golf course could not be saved.",
                GolfServiceHelpers.FieldError(field, "A code of 1 to 32 characters is required."));
        }

        return code.ToUpperInvariant();
    }

    private static string RequireName(string value, string field)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 160)
        {
            throw new ValidationException(
                "The golf course could not be saved.",
                GolfServiceHelpers.FieldError(field, "A name of 1 to 160 characters is required."));
        }

        return name;
    }

    private static string NormalizeCurrency(string value)
    {
        var currency = value?.Trim() ?? string.Empty;
        if (currency.Length is 0 or > 8)
        {
            throw new ValidationException(
                "The golf course could not be saved.",
                GolfServiceHelpers.FieldError("currencyCode", "A currency code of 1 to 8 characters is required."));
        }

        return currency.ToUpperInvariant();
    }

    /// <summary>
    /// GOLF-001/Phase 3 §40: when a hole list is supplied it must cover the
    /// declared hole count exactly once each, so a course cannot be configured
    /// with a contradictory number of holes.
    /// </summary>
    private static List<CreateGolfHoleInput> ValidateHoles(int numberOfHoles, IReadOnlyList<CreateGolfHoleInput> holes)
    {
        if (numberOfHoles < 1 || numberOfHoles > 36)
        {
            throw new ValidationException(
                "The golf course could not be saved.",
                GolfServiceHelpers.FieldError("numberOfHoles", "A course must declare between 1 and 36 holes."));
        }

        if (holes is null || holes.Count == 0)
        {
            return [];
        }

        if (holes.Count != numberOfHoles)
        {
            throw new ValidationException(
                "The golf course could not be saved.",
                GolfServiceHelpers.FieldError(
                    "holes",
                    $"{holes.Count} hole(s) were supplied but the course declares {numberOfHoles}."));
        }

        var numbers = holes.Select(h => h.HoleNumber).OrderBy(n => n).ToList();
        for (var i = 0; i < numbers.Count; i++)
        {
            if (numbers[i] != i + 1)
            {
                throw new ValidationException(
                    "The golf course could not be saved.",
                    GolfServiceHelpers.FieldError("holes", "Holes must be numbered 1 to the declared hole count with no gaps or duplicates."));
            }
        }

        foreach (var hole in holes)
        {
            if (hole.Par is < 2 or > 6)
            {
                throw new ValidationException(
                    "The golf course could not be saved.",
                    GolfServiceHelpers.FieldError("holes", "A hole par must be between 2 and 6."));
            }
        }

        return holes.ToList();
    }

    private static void ValidateTeeShape(int capacity, int? minimumPlayers, int? maximumPlayers, string code)
    {
        if (capacity < GolfDomain.MinSlotCapacity || capacity > GolfDomain.MaxSlotCapacity)
        {
            throw new ValidationException(
                "The golf tee could not be saved.",
                GolfServiceHelpers.FieldError(
                    "capacity",
                    $"Tee '{code}' capacity must be between {GolfDomain.MinSlotCapacity} and {GolfDomain.MaxSlotCapacity} players."));
        }

        if (minimumPlayers is { } minimum && minimum < 1)
        {
            throw new ValidationException(
                "The golf tee could not be saved.",
                GolfServiceHelpers.FieldError("minimumPlayers", "A minimum player count must be at least 1."));
        }

        if (maximumPlayers is { } maximum && maximum < 1)
        {
            throw new ValidationException(
                "The golf tee could not be saved.",
                GolfServiceHelpers.FieldError("maximumPlayers", "A maximum player count must be at least 1."));
        }

        if (minimumPlayers is { } lo && maximumPlayers is { } hi && lo > hi)
        {
            throw new ValidationException(
                "The golf tee could not be saved.",
                GolfServiceHelpers.FieldError("minimumPlayers", "The minimum player count cannot exceed the maximum."));
        }
    }

    /// <summary>
    /// GOLF-016: the fee products must exist in the D4 catalogue. Golf never owns a
    /// price, so a tee cannot be pointed at a product that does not exist.
    /// </summary>
    private async Task EnsureFeeProductsAsync(CreateGolfTeeInput input, CancellationToken cancellationToken)
    {
        if (input.GreenFeeProductId is { } greenId)
        {
            var exists = await _context.Products.AnyAsync(p => p.Id == greenId, cancellationToken);
            if (!exists)
            {
                throw new ValidationException(
                    "The golf tee could not be saved.",
                    GolfServiceHelpers.FieldError("greenFeeProductId", $"Product '{greenId}' does not exist in the catalogue."));
            }
        }

        if (input.GuestFeeProductId is { } guestId)
        {
            var exists = await _context.Products.AnyAsync(p => p.Id == guestId, cancellationToken);
            if (!exists)
            {
                throw new ValidationException(
                    "The golf tee could not be saved.",
                    GolfServiceHelpers.FieldError("guestFeeProductId", $"Product '{guestId}' does not exist in the catalogue."));
            }
        }
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
