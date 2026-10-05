using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// GOLF-008: capture visitor details. Phase 3 §45 keeps guest information
/// "separate from membership", so this service deliberately has no create-member
/// path: a guest is recorded on the golf side only, and the host link points at an
/// existing D2 member.
/// </summary>
public sealed class EntityFrameworkGolfGuestService : IGolfGuestService
{
    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;

    public EntityFrameworkGolfGuestService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _clock = clock;
    }

    public async Task<GolfGuestSearchResult> SearchGuestsAsync(
        GolfGuestSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = GolfServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<GolfGuestRecordEntity> query = _context.GolfGuestRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            // Wildcarded, because a search box that only matched a whole stored value
            // would be useless once a club has more than a page of guests.
            var search = $"%{input.Search.Trim()}%";
            query = query.Where(g =>
                EF.Functions.ILike(g.FullName, search)
                || EF.Functions.ILike(g.Reference, search)
                || (g.ContactDetails != null && EF.Functions.ILike(g.ContactDetails, search)));
        }

        if (input.Status is { } status)
        {
            query = query.Where(g => g.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var guests = await query
            .OrderBy(g => g.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GolfGuestSearchResult(guests.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<GolfGuestDto> GetGuestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var guest = await _context.GolfGuestRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Golf guest '{id}' was not found.");

        return ToDto(guest);
    }

    public async Task<GolfGuestDto> CreateGuestAsync(
        CreateGolfGuestInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var fullName = RequireText(input.FullName, 160, "fullName");

        if (input.ContactDetails is { } contact && contact.Trim().Length > 256)
        {
            throw new ValidationException(
                "The golf guest could not be saved.",
                GolfServiceHelpers.FieldError("contactDetails", "Contact details cannot exceed 256 characters."));
        }

        if (input.Notes is { } notes && notes.Trim().Length > 512)
        {
            throw new ValidationException(
                "The golf guest could not be saved.",
                GolfServiceHelpers.FieldError("notes", "Notes cannot exceed 512 characters."));
        }

        // Phase 3 §45 HostPersonId must reference a real person. The host is an
        // existing D2 member; golf never creates one.
        if (input.HostMemberId is { } hostId)
        {
            var hostExists = await _context.Members.AnyAsync(m => m.Id == hostId, cancellationToken);
            if (!hostExists)
            {
                throw new ValidationException(
                    "The golf guest could not be saved.",
                    GolfServiceHelpers.FieldError("hostMemberId", $"Member '{hostId}' does not exist."));
            }
        }

        var now = _clock.UtcNow;
        var guest = new GolfGuestRecordEntity
        {
            Reference = await _references.GenerateAsync(
                GolfGuestReference.Prefix,
                GolfGuestReference.Width,
                cancellationToken),
            FullName = fullName,
            ContactDetails = Trim(input.ContactDetails),
            HostMemberId = input.HostMemberId,
            Status = GolfGuestStatus.Active,
            VisitCount = 0,
            Notes = Trim(input.Notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.GolfGuestRecords.Add(guest);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(guest);
    }

    public async Task<GolfGuestDto> SetGuestStatusAsync(
        Guid id,
        GolfGuestStatus status,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var guest = await _context.GolfGuestRecords.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Golf guest '{id}' was not found.");

        guest.Status = status;
        guest.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(guest);
    }

    /// <summary>
    /// Records a completed visit on the guest record (Phase 3 §45 History). Called
    /// when a booking completes so the guest list reflects real activity.
    /// </summary>
    internal async Task RecordVisitAsync(Guid guestId, DateTimeOffset visitedAtUtc, CancellationToken cancellationToken)
    {
        var guest = await _context.GolfGuestRecords.FirstOrDefaultAsync(g => g.Id == guestId, cancellationToken);
        if (guest is null)
        {
            return;
        }

        guest.VisitCount += 1;
        guest.LastVisitedAtUtc = visitedAtUtc;
        guest.UpdatedAtUtc = _clock.UtcNow;
    }

    internal static GolfGuestDto ToDto(GolfGuestRecordEntity guest) => new(
        guest.Id,
        guest.Reference,
        guest.FullName,
        guest.ContactDetails,
        guest.HostMemberId,
        guest.Status,
        guest.VisitCount,
        guest.LastVisitedAtUtc,
        guest.Notes,
        guest.CreatedAtUtc,
        guest.UpdatedAtUtc);

    private static string RequireText(string? value, int maxLength, string field)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 || text.Length > maxLength)
        {
            throw new ValidationException(
                "The golf guest could not be saved.",
                GolfServiceHelpers.FieldError(field, $"A value of 1 to {maxLength} characters is required."));
        }

        return text;
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
