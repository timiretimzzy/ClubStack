using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// Shared golf plumbing: paging, reference validation, the availability
/// projection and the GOLF-012 restriction check, so every golf service enforces
/// the same rules in the same place.
/// </summary>
internal static class GolfServiceHelpers
{
    public static (int Page, int PageSize) NormalizePage(int page, int pageSize)
        => (page < 1 ? 1 : page, pageSize is < 1 or > 200 ? 50 : pageSize);

    public static Dictionary<string, string[]> FieldError(string field, string message)
        => new(StringComparer.Ordinal) { [field] = [message] };

    /// <summary>
    /// Phase 3 §79 reference integrity with a field-level validation message,
    /// matching how the D6 services report a missing related record.
    /// </summary>
    public static TEntity Require<TEntity>(TEntity? entity, string field, string message)
        where TEntity : class
        => entity ?? throw new ValidationException("The golf operation could not be completed.", FieldError(field, message));

    /// <summary>
    /// GOLF-021: rejects a transition the Phase 8 §7.7 state machine does not
    /// define. The message names the current state and the attempted one so the
    /// tee-sheet operator can see why the action was refused.
    /// </summary>
    public static void EnsureTransition(GolfBookingStatus from, GolfBookingStatus to, string reference)
    {
        if (GolfDomain.IsValidTransition(from, to))
        {
            return;
        }

        throw new ConflictException(
            $"Booking {reference} is {from} and cannot become {to}. " +
            "Phase 8 §7.7 allows Draft → Confirmed, Confirmed → CheckedIn, CheckedIn → Completed, " +
            "Confirmed → Cancelled and Confirmed → NoShow only.");
    }

    /// <summary>
    /// GOLF-012: applies the configured member/guest/player-count restrictions of
    /// the tee and the slot. Returns the violation text, or null when the
    /// composition is allowed.
    /// </summary>
    public static string? RestrictionViolation(
        GolfTeeEntity tee,
        GolfTeeSlotEntity slot,
        int playerCount,
        int guestCount)
        => GolfDomain.ValidateRestrictions(
            tee.MembersOnly,
            tee.GuestsAllowed,
            tee.MinimumPlayers,
            slot.MaximumPlayers ?? tee.MaximumPlayers,
            playerCount,
            guestCount);

    /// <summary>
    /// Phase 9 §12: availability is derived from the configured capacity and the
    /// players already held by capacity-consuming bookings. A Draft hold is
    /// excluded because GOLF-010 validates availability when a booking is
    /// confirmed, not when it is drafted.
    /// </summary>
    public static async Task<int> TakenPlayersAsync(
        ClubStackDbContext context,
        Guid teeSlotId,
        Guid? excludingBookingId,
        CancellationToken cancellationToken)
    {
        var consumingStatuses = new[]
        {
            GolfBookingStatus.Confirmed,
            GolfBookingStatus.CheckedIn,
            GolfBookingStatus.Completed,
            GolfBookingStatus.NoShow,
        };

        return await context.GolfBookingPlayers
            .AsNoTracking()
            .Where(p => p.Booking.TeeSlotId == teeSlotId)
            .Where(p => excludingBookingId == null || p.BookingId != excludingBookingId)
            .Where(p => consumingStatuses.Contains(p.Booking.Status))
            .Where(p => p.Status != GolfBookingPlayerStatus.Cancelled)
            .SumAsync(p => (int?)1, cancellationToken) ?? 0;
    }
}
