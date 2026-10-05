using ClubStack.Application.Exceptions;
using ClubStack.Application.Golf;
using ClubStack.Application.Idempotency;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using ClubStack.Infrastructure.Pos;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// GOLF-006/007 (create a booking, configure its players), GOLF-010 (validate
/// availability before confirming), GOLF-011 (prevent overbooking), GOLF-013
/// (check-in), GOLF-014, GOLF-020 (cancellation) and GOLF-022 (no-show).
///
/// <para><b>Concurrency.</b> GOLF-011 is the requirement that makes this service
/// interesting: two desks confirming against the last free places of one slot at
/// the same moment must not both succeed. Availability is therefore never trusted
/// from a read taken before the write. Confirm takes a PostgreSQL
/// <c>SELECT … FOR UPDATE</c> lock on the tee-slot row, re-reads the consuming
/// player count inside the lock and only then writes, so the check and the write
/// are one serialised operation. The lock order (booking, then tee slot) is fixed
/// across every command here so two concurrent commands cannot deadlock.</para>
///
/// <para><b>Money.</b> Fees are resolved from the D4 catalogue using D5's
/// established selling-price resolution and snapshotted onto the player at
/// confirmation, so a later price change never rewrites a confirmed booking.</para>
/// </summary>
public sealed class EntityFrameworkGolfBookingService : IGolfBookingService
{
    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;
    private readonly IIdempotencyStore _idempotency;
    private readonly EntityFrameworkGolfGuestService _guests;

    public EntityFrameworkGolfBookingService(
        ClubStackDbContext context,
        IUnitOfWork unitOfWork,
        IReferenceGenerator references,
        ISystemClock clock,
        IIdempotencyStore idempotency,
        EntityFrameworkGolfGuestService guests)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _references = references;
        _clock = clock;
        _idempotency = idempotency;
        _guests = guests;
    }

    public async Task<GolfBookingSearchResult> SearchBookingsAsync(
        GolfBookingSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = GolfServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<GolfBookingEntity> query = Filtered(input);

        var total = await query.CountAsync(cancellationToken);
        var bookings = await query
            .Include(b => b.TeeSlot).ThenInclude(s => s.Course)
            .Include(b => b.TeeSlot).ThenInclude(s => s.Tee)
            .Include(b => b.Players).ThenInclude(p => p.Member).ThenInclude(m => m!.Person)
            .Include(b => b.Players).ThenInclude(p => p.GolfGuest)
            .Include(b => b.Charges)
            .OrderByDescending(b => b.TeeSlot.Date)
            .ThenBy(b => b.TeeSlot.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GolfBookingSearchResult(bookings.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<GolfBookingDto> GetBookingAsync(Guid id, CancellationToken cancellationToken = default)
        => ToDto(await LoadAsync(id, cancellationToken));

    public async Task<GolfBookingDto> CreateBookingAsync(
        CreateGolfBookingInput input,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GolfIdempotency.NormalizeKey(input.IdempotencyKey);
        var requestHash = idempotencyKey is null
            ? null
            : IdempotencyRequestHash.Compute(BookingPayload(input));

        if (idempotencyKey is not null)
        {
            var (created, existing) = await _idempotency.TryAcquireAsync(
                idempotencyKey, GolfIdempotency.CreateBookingOperation, actorUserId, requestHash, correlationId, cancellationToken);
            if (!created)
            {
                return GolfIdempotency.ReplayStoredOrConflict<GolfBookingDto>(
                    existing, "golf booking creation", requestHash);
            }
        }

        try
        {
            var booking = await CreateBookingCoreAsync(input, actorUserId, correlationId, cancellationToken);

            if (idempotencyKey is not null)
            {
                await _idempotency.CompleteAsync(
                    idempotencyKey,
                    GolfIdempotency.CreateBookingOperation,
                    actorUserId,
                    GolfJson.Serialize(booking),
                    cancellationToken);
            }

            return booking;
        }
        catch
        {
            // A key claimed for a request that then failed must be released, or the
            // client's retry would be refused as "in progress" forever.
            await GolfIdempotency.MarkFailedAsync(
                _idempotency,
                idempotencyKey,
                GolfIdempotency.CreateBookingOperation,
                actorUserId,
                "The golf booking was not created.",
                cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Canonical description of a booking request, so the same idempotency key used for
    /// a materially different booking is detected rather than replayed.
    /// </summary>
    private static string BookingPayload(CreateGolfBookingInput input)
    {
        var players = string.Join(
            ";",
            input.Players?.Select(p => $"{p.MemberId?.ToString("N") ?? "-"}/{p.GolfGuestId?.ToString("N") ?? "-"}/{p.Position?.ToString() ?? "-"}")
            ?? []);

        return $"{input.TeeSlotId:N}|{input.BookedByMemberId:N}|{players}";
    }

    private async Task<GolfBookingDto> CreateBookingCoreAsync(
        CreateGolfBookingInput input,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (input.Players is null || input.Players.Count == 0)
        {
            throw new ValidationException(
                "The golf booking could not be created.",
                GolfServiceHelpers.FieldError("players", "A booking must contain at least one player."));
        }

        if (input.Players.Count > GolfDomain.MaxPlayersPerBooking)
        {
            throw new ValidationException(
                "The golf booking could not be created.",
                GolfServiceHelpers.FieldError(
                    "players",
                    $"A booking cannot contain more than {GolfDomain.MaxPlayersPerBooking} players."));
        }

        var slot = await ReadSlotAsync(input.TeeSlotId, cancellationToken);
        EnsureSlotAcceptsBookings(slot);

        await EnsureMemberExistsAsync(input.BookedByMemberId, "bookedByMemberId", cancellationToken);

        var now = _clock.UtcNow;
        var booking = new GolfBookingEntity
        {
            Reference = await _references.GenerateAsync(
                GolfBookingReference.Prefix,
                GolfBookingReference.Width,
                cancellationToken),
            TeeSlotId = slot.Id,
            BookedByMemberId = input.BookedByMemberId,
            // GOLF-010 validates availability at confirmation, so a booking starts
            // as a Draft hold that does not consume capacity.
            Status = GolfBookingStatus.Draft,
            CurrencyCode = slot.Course.CurrencyCode,
            CreatedAtUtc = now,
            CreatedByUserId = actorUserId,
            CorrelationId = correlationId,
        };

        _context.GolfBookings.Add(booking);

        var position = 0;
        foreach (var player in input.Players)
        {
            var resolved = await ResolvePlayerAsync(player, booking.CurrencyCode, position + 1, cancellationToken);
            booking.Players.Add(resolved);
            position++;
        }

        ValidateComposition(booking, slot);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetBookingAsync(booking.Id, cancellationToken);
    }

    public async Task<GolfBookingDto> AddPlayerAsync(
        Guid bookingId,
        GolfBookingPlayerInput player,
        string actorUserId,
        CancellationToken cancellationToken = default)
        => await ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);

                // Players can only be arranged while the booking is still a draft: a
                // confirmed booking has priced fees and, from GOLF-010 onward, holds
                // capacity that changing the composition would invalidate.
                if (booking.Status != GolfBookingStatus.Draft)
                {
                    throw new ConflictException(
                        $"Booking {booking.Reference} is {booking.Status}; players can only be changed while it is Draft.");
                }

                var slot = await ReadSlotAsync(booking.TeeSlotId, cancellationToken);

                var position = player.Position
                               ?? (booking.Players.Count == 0 ? 1 : booking.Players.Max(p => p.Position) + 1);
                var resolved = await ResolvePlayerAsync(player, booking.CurrencyCode, position, cancellationToken);

                // The Booking navigation links the new row to its parent. The booking is
                // already tracked, so appending to the collection alone would be discovered
                // as a change to a row that does not exist and written as an UPDATE;
                // registering the entity is what makes it an INSERT.
                resolved.Booking = booking;
                _context.GolfBookingPlayers.Add(resolved);
                ValidateComposition(booking, slot);
                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    public async Task<GolfBookingDto> RemovePlayerAsync(
        Guid bookingId,
        Guid playerId,
        string actorUserId,
        CancellationToken cancellationToken = default)
        => await ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);

                if (booking.Status != GolfBookingStatus.Draft)
                {
                    throw new ConflictException(
                        $"Booking {booking.Reference} is {booking.Status}; players can only be changed while it is Draft.");
                }

                var player = booking.Players.FirstOrDefault(p => p.Id == playerId)
                             ?? throw new NotFoundException($"Booking '{bookingId}' has no player '{playerId}'.");

                if (booking.Players.Count == 1)
                {
                    throw new ConflictException(
                        $"Booking {booking.Reference} must keep at least one player. Cancel the booking instead.");
                }

                booking.Players.Remove(player);
                _context.GolfBookingPlayers.Remove(player);
                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    public async Task<GolfBookingDto> ConfirmBookingAsync(
        Guid bookingId,
        ConfirmGolfBookingInput input,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GolfIdempotency.NormalizeKey(input.IdempotencyKey);
        // The target booking is part of the operation's identity: without it, the same key
        // against a different booking would replay the wrong confirmation.
        var requestHash = idempotencyKey is null
            ? null
            : IdempotencyRequestHash.Compute(
                $"{bookingId:N}|{input.OverrideCapacity}|{input.Note}|{input.OverrideReason}");

        if (idempotencyKey is not null)
        {
            var (created, existing) = await _idempotency.TryAcquireAsync(
                idempotencyKey, GolfIdempotency.ConfirmBookingOperation, actorUserId, requestHash, correlationId, cancellationToken);
            if (!created)
            {
                return GolfIdempotency.ReplayStoredOrConflict<GolfBookingDto>(
                    existing, "golf booking confirmation", requestHash);
            }
        }

        try
        {
            var confirmed = await ConfirmBookingCoreAsync(bookingId, input, actorUserId, correlationId, cancellationToken);

            if (idempotencyKey is not null)
            {
                await _idempotency.CompleteAsync(
                    idempotencyKey,
                    GolfIdempotency.ConfirmBookingOperation,
                    actorUserId,
                    GolfJson.Serialize(confirmed),
                    cancellationToken);
            }

            return confirmed;
        }
        catch
        {
            await GolfIdempotency.MarkFailedAsync(
                _idempotency,
                idempotencyKey,
                GolfIdempotency.ConfirmBookingOperation,
                actorUserId,
                "The golf booking was not confirmed.",
                cancellationToken);
            throw;
        }
    }

    private Task<GolfBookingDto> ConfirmBookingCoreAsync(
        Guid bookingId,
        ConfirmGolfBookingInput input,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);
                GolfServiceHelpers.EnsureTransition(booking.Status, GolfBookingStatus.Confirmed, booking.Reference);

                // Lock order is always booking → tee slot. Taking the slot lock here is what
                // makes the capacity check below atomic with the write.
                var slot = await LoadSlotForUpdateAsync(booking.TeeSlotId, cancellationToken);
                EnsureSlotAcceptsBookings(slot);

                var taken = await GolfServiceHelpers.TakenPlayersAsync(_context, slot.Id, booking.Id, cancellationToken);
                var requested = booking.Players.Count;
                var (open, overload) = GolfDomain.RemainingCapacity(slot.Capacity, taken, requested);

                if (overload > 0)
                {
                    if (!input.OverrideCapacity)
                    {
                        // GOLF-011: refuse rather than overbook. The message carries the live
                        // numbers so the operator can decide whether to override.
                        throw new ConflictException(
                            $"Tee slot {slot.Date:yyyy-MM-dd} {slot.StartTime:HH\\:mm} has {open} open place(s) but this booking needs {requested}. " +
                            "Use an authorized override to confirm it anyway.");
                    }

                    if (string.IsNullOrWhiteSpace(input.OverrideReason))
                    {
                        throw new ValidationException(
                            "The golf booking could not be confirmed.",
                            GolfServiceHelpers.FieldError(
                                "overrideReason",
                                "An override requires a reason, which is recorded with the actor and timestamp."));
                    }

                    // Phase 9 §12 requires the override record to name the actor, reason,
                    // timestamp and affected booking/slot.
                    booking.CapacityOverridden = true;
                    booking.OverrideReason = input.OverrideReason.Trim();
                    booking.OverrideByUserId = actorUserId;
                    booking.OverriddenAtUtc = _clock.UtcNow;
                }

                ValidateComposition(booking, slot);
                await ApplyFeesAsync(booking, slot.Tee, cancellationToken);

                booking.Status = GolfBookingStatus.Confirmed;
                booking.ConfirmedAtUtc = _clock.UtcNow;
                ApplyPlayerStatus(booking, GolfBookingStatus.Confirmed);

                await RaiseGreenFeeChargesAsync(booking, slot, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    public async Task<GolfBookingDto> CheckInBookingAsync(
        Guid bookingId,
        string actorUserId,
        CancellationToken cancellationToken = default)
        => await ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);
                GolfServiceHelpers.EnsureTransition(booking.Status, GolfBookingStatus.CheckedIn, booking.Reference);

                booking.Status = GolfBookingStatus.CheckedIn;
                booking.CheckedInAtUtc = _clock.UtcNow;
                ApplyPlayerStatus(booking, GolfBookingStatus.CheckedIn);
                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    public async Task<GolfBookingDto> CompleteBookingAsync(
        Guid bookingId,
        string actorUserId,
        CancellationToken cancellationToken = default)
        => await ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);
                GolfServiceHelpers.EnsureTransition(booking.Status, GolfBookingStatus.Completed, booking.Reference);

                booking.Status = GolfBookingStatus.Completed;
                booking.CompletedAtUtc = _clock.UtcNow;

                // Phase 3 §45: a completed visit updates the guest's visit history.
                var visitedAt = booking.CompletedAtUtc.Value;
                var guestIds = booking.Players
                    .Where(p => p.PlayerType == GolfPlayerType.Guest && p.GolfGuestId is { } guestId)
                    .Select(p => p.GolfGuestId!.Value)
                    .Distinct()
                    .ToList();

                foreach (var guestId in guestIds)
                {
                    await _guests.RecordVisitAsync(guestId, visitedAt, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    public async Task<GolfBookingDto> CancelBookingAsync(
        Guid bookingId,
        CancelGolfBookingInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
        => await ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);
                GolfServiceHelpers.EnsureTransition(booking.Status, GolfBookingStatus.Cancelled, booking.Reference);

                if (string.IsNullOrWhiteSpace(input.Reason))
                {
                    throw new ValidationException(
                        "The golf booking could not be cancelled.",
                        GolfServiceHelpers.FieldError("reason", "A cancellation reason is required."));
                }

                booking.Status = GolfBookingStatus.Cancelled;
                booking.CancelledAtUtc = _clock.UtcNow;
                booking.CancellationReason = input.Reason.Trim();
                ApplyPlayerStatus(booking, GolfBookingStatus.Cancelled);

                // GOLF-020 frees the places immediately, and a cancelled booking must not
                // leave an unpaid charge outstanding: its pending charges are cancelled
                // rather than posted (Phase 3 §75 charges belong to a live booking).
                CancelPendingCharges(booking, "The booking was cancelled.");

                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    public async Task<GolfBookingDto> NoShowBookingAsync(
        Guid bookingId,
        string actorUserId,
        CancellationToken cancellationToken = default)
        => await ExecuteLockedAsync(
            async () =>
            {
                var booking = await LoadForUpdateAsync(bookingId, cancellationToken);
                GolfServiceHelpers.EnsureTransition(booking.Status, GolfBookingStatus.NoShow, booking.Reference);

                booking.Status = GolfBookingStatus.NoShow;
                booking.NoShowAtUtc = _clock.UtcNow;
                ApplyPlayerStatus(booking, GolfBookingStatus.NoShow);
                await _context.SaveChangesAsync(cancellationToken);

                return await GetBookingAsync(bookingId, cancellationToken);
            },
            cancellationToken);

    // ---------------------------------------------------------------------
    // Loading and locking
    // ---------------------------------------------------------------------

    private IQueryable<GolfBookingEntity> Filtered(GolfBookingSearchInput input)
    {
        IQueryable<GolfBookingEntity> query = _context.GolfBookings.AsNoTracking();

        if (input.FromDate is { } from)
        {
            query = query.Where(b => b.TeeSlot.Date >= from);
        }

        if (input.ToDate is { } to)
        {
            query = query.Where(b => b.TeeSlot.Date <= to);
        }

        if (input.CourseId is { } courseId)
        {
            query = query.Where(b => b.TeeSlot.CourseId == courseId);
        }

        if (input.TeeId is { } teeId)
        {
            query = query.Where(b => b.TeeSlot.TeeId == teeId);
        }

        if (input.MemberId is { } memberId)
        {
            // Either the member made the booking, or the member is a player on it.
            query = query.Where(b => b.BookedByMemberId == memberId || b.Players.Any(p => p.MemberId == memberId));
        }

        if (input.Status is { } status)
        {
            query = query.Where(b => b.Status == status);
        }

        return query;
    }

    private async Task<GolfBookingEntity> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.GolfBookings
            .AsNoTracking()
            .Include(b => b.TeeSlot).ThenInclude(s => s.Course)
            .Include(b => b.TeeSlot).ThenInclude(s => s.Tee)
            .Include(b => b.Players).ThenInclude(p => p.Member).ThenInclude(m => m!.Person)
            .Include(b => b.Players).ThenInclude(p => p.GolfGuest)
            .Include(b => b.Charges)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Golf booking '{id}' was not found.");
    }

    private IQueryable<GolfBookingEntity> BookingGraph()
        => _context.GolfBookings
            .Include(b => b.TeeSlot).ThenInclude(s => s.Course)
            .Include(b => b.TeeSlot).ThenInclude(s => s.Tee)
            .Include(b => b.Players).ThenInclude(p => p.Member).ThenInclude(m => m!.Person)
            .Include(b => b.Players).ThenInclude(p => p.GolfGuest)
            .Include(b => b.Charges);

    private async Task<GolfBookingEntity> LoadForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        // Lock first, then read. Taking the lock after the read would only serialise the
        // write, while the state that justified the write was still taken from a snapshot
        // a concurrent transaction could already have changed.
        await LockBookingRowAsync(id, cancellationToken);

        return await BookingGraph()
                   .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Golf booking '{id}' was not found.");
    }

    private IQueryable<GolfTeeSlotEntity> LoadSlotAsync(Guid id, CancellationToken cancellationToken)
        => _context.GolfTeeSlots
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Tee)
            .Where(s => s.Id == id);

    private async Task<GolfTeeSlotEntity> ReadSlotAsync(Guid id, CancellationToken cancellationToken)
        => await LoadSlotAsync(id, cancellationToken).FirstOrDefaultAsync(cancellationToken)
           ?? throw new ValidationException(
               "The golf operation could not be completed.",
               GolfServiceHelpers.FieldError("teeSlotId", $"Tee slot '{id}' does not exist."));

    private async Task<GolfTeeSlotEntity> LoadSlotForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        await LockTeeSlotRowAsync(id, cancellationToken);
        return await ReadSlotAsync(id, cancellationToken);
    }

    /// <summary>
    /// Takes a PostgreSQL row-level write lock on the booking. EF Core cannot express
    /// <c>FOR UPDATE</c> in LINQ, and Npgsql requires the raw statement, so this is
    /// one of the two places D7 reaches for SQL directly. Every other golf query stays
    /// in LINQ.
    ///
    /// <para>The relation name is a literal in the statement rather than a hole, because
    /// an interpolated hole becomes a query parameter and PostgreSQL does not accept a
    /// parameter where a relation is expected. Only the id is parameterised. The
    /// projected column is aliased <c>Value</c> because that is the name EF Core binds
    /// a scalar <see cref="System.Data.Common.DbCommand"/> result to.</para>
    /// </summary>
    private async Task LockBookingRowAsync(Guid id, CancellationToken cancellationToken)
        => await _context.Database
            .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM golf_bookings WHERE \"Id\" = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Takes the row-level write lock on the tee slot, which is the row that serialises
    /// competing confirmations of the same slot. Locking the slot rather than a booking
    /// is what makes two desks confirming the last place race correctly.
    /// </summary>
    private async Task LockTeeSlotRowAsync(Guid id, CancellationToken cancellationToken)
        => await _context.Database
            .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM tee_slots WHERE \"Id\" = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Runs a booking command inside one transaction. A <c>FOR UPDATE</c> lock is only
    /// held until the statement that took it finishes unless a transaction encloses it, so
    /// without this the capacity re-read would race the write it guards. The D5/D6
    /// services use the same <see cref="IUnitOfWork"/> boundary.
    /// </summary>
    private async Task<TResult> ExecuteLockedAsync<TResult>(
        Func<Task<TResult>> command,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var result = await command();
            await _unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ExecuteLockedAsync] Rollback failed: {rollbackEx.GetType().Name}: {rollbackEx.Message}");
            }
            throw;
        }
    }

    /// ---------------------------------------------------------------------
    /// Rules
    /// ---------------------------------------------------------------------

    private static void EnsureSlotAcceptsBookings(GolfTeeSlotEntity slot)
    {
        if (slot.Course.Status != GolfCourseStatus.Active)
        {
            throw new ConflictException(
                $"Course '{slot.Course.Code}' is {slot.Course.Status} and is not accepting bookings.");
        }

        if (slot.Tee.Status != GolfTeeStatus.Active)
        {
            throw new ConflictException(
                $"Tee '{slot.Tee.Code}' is {slot.Tee.Status} and is not accepting bookings.");
        }

        if (slot.Status == TeeSlotStatus.Blocked)
        {
            // GOLF-002: a blocked slot is a closure or unavailability period.
            throw new ConflictException(
                $"The tee slot for {slot.Date:yyyy-MM-dd} {slot.StartTime:HH\\:mm} is {slot.Status} and is not bookable.");
        }
    }

    private async Task EnsureMemberExistsAsync(Guid memberId, string field, CancellationToken cancellationToken)
    {
        if (memberId == Guid.Empty)
        {
            throw new ValidationException(
                "The golf booking could not be created.",
                GolfServiceHelpers.FieldError(field, "A member is required."));
        }

        if (!await _context.Members.AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new ValidationException(
                "The golf booking could not be created.",
                GolfServiceHelpers.FieldError(field, $"Member '{memberId}' does not exist."));
        }
    }

    /// <summary>
    /// GOLF-012 / Phase 3 §44: a player is either a member or a golf guest, never
    /// both and never neither. The membership and guest records are verified here so
    /// a booking cannot reference a person who does not exist.
    /// </summary>
    private async Task<GolfBookingPlayerEntity> ResolvePlayerAsync(
        GolfBookingPlayerInput input,
        string currencyCode,
        int position,
        CancellationToken cancellationToken)
    {
        var hasMember = input.MemberId is { } memberId && memberId != Guid.Empty;
        var hasGuest = input.GolfGuestId is { } guestId && guestId != Guid.Empty;

        if (hasMember == hasGuest)
        {
            throw new ValidationException(
                "The golf booking could not be saved.",
                GolfServiceHelpers.FieldError(
                    "players",
                    "Each player must be exactly one of a member or a golf guest record."));
        }

        GolfPlayerType playerType;
        if (hasMember)
        {
            var member = await _context.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == input.MemberId, cancellationToken);

            if (member is null)
            {
                throw new ValidationException(
                    "The golf booking could not be saved.",
                    GolfServiceHelpers.FieldError("players", $"Member '{input.MemberId}' does not exist."));
            }

            playerType = GolfPlayerType.Member;
        }
        else
        {
            var guest = await _context.GolfGuestRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == input.GolfGuestId, cancellationToken);

            if (guest is null)
            {
                throw new ValidationException(
                    "The golf booking could not be saved.",
                    GolfServiceHelpers.FieldError("players", $"Golf guest '{input.GolfGuestId}' does not exist."));
            }

            if (guest.Status != GolfGuestStatus.Active)
            {
                throw new ValidationException(
                    "The golf booking could not be saved.",
                    GolfServiceHelpers.FieldError("players", $"Golf guest '{guest.Reference}' is {guest.Status}."));
            }

            playerType = GolfPlayerType.Guest;
        }

        return new GolfBookingPlayerEntity
        {
            PlayerType = playerType,
            MemberId = hasMember ? input.MemberId : null,
            GolfGuestId = hasGuest ? input.GolfGuestId : null,
            Status = GolfBookingPlayerStatus.Booked,
            Position = position,
            // A provisional figure so the draft tee sheet shows something; the
            // authoritative amount is snapshotted at confirmation.
            Fee = 0m,
            CurrencyCode = currencyCode,
        };
    }

    /// <summary>
    /// Applies the configured member/guest/player-count restrictions to the whole
    /// booking. This is deliberately a whole-booking check: GOLF-012 is about the
    /// composition of the group, not about each player in isolation.
    /// </summary>
    private static void ValidateComposition(GolfBookingEntity booking, GolfTeeSlotEntity slot)
    {
        var players = booking.Players.ToList();
        var guestCount = players.Count(p => p.PlayerType == GolfPlayerType.Guest);

        var violation = GolfServiceHelpers.RestrictionViolation(slot.Tee, slot, players.Count, guestCount);
        if (violation is not null)
        {
            throw new ValidationException(
                "The golf booking could not be saved.",
                GolfServiceHelpers.FieldError("players", violation));
        }
    }

    /// <summary>
    /// GOLF-016: snapshotted the confirmed fee per player. A member player is priced
    /// from the tee's green-fee product using the D5 member price with its standard
    /// fallback; a guest player is priced from the guest-fee product at the standard
    /// price. The amount is written onto the player and never re-read afterwards.
    /// </summary>
    private async Task ApplyFeesAsync(
        GolfBookingEntity booking,
        GolfTeeEntity tee,
        CancellationToken cancellationToken)
    {
        var memberPrices = await LoadPricesAsync(tee.GreenFeeProductId, cancellationToken);
        var guestPrices = await LoadPricesAsync(tee.GuestFeeProductId, cancellationToken);

        foreach (var player in booking.Players)
        {
            if (player.PlayerType == GolfPlayerType.Member)
            {
                player.Fee = tee.GreenFeeProductId is { } productId
                    ? EntityFrameworkPosProductService.ResolveSellingPrice(productId, memberPrices, player.MemberId).Amount
                    : 0m;
            }
            else
            {
                player.Fee = tee.GuestFeeProductId is { } productId
                    ? EntityFrameworkPosProductService.ResolveSellingPrice(productId, guestPrices, null).Amount
                    : 0m;
            }

            player.Fee = GolfDomain.NormalizeAmount(player.Fee);
            player.CurrencyCode = booking.CurrencyCode;
        }
    }

    private async Task<List<ProductPriceEntity>> LoadPricesAsync(Guid? productId, CancellationToken cancellationToken)
    {
        if (productId is not { } id)
        {
            return [];
        }

        return await _context.ProductPrices
            .AsNoTracking()
            .Where(p => p.ProductId == id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// GOLF-017: raises one pending green-fee charge per player at confirmation, so
    /// the charge row, the player link and the later financial posting all share a
    /// single source. The charge is created here but only reaches the member's
    /// account when the charges are posted.
    /// </summary>
    private Task RaiseGreenFeeChargesAsync(
        GolfBookingEntity booking,
        GolfTeeSlotEntity slot,
        CancellationToken cancellationToken)
    {
        foreach (var player in booking.Players)
        {
            var charge = new GolfChargeEntity
            {
                BookingPlayerId = player.Id,
                ChargeType = GolfChargeType.GreenFee,
                Description = $"Green fee ({slot.Tee.Code}) {slot.Date:yyyy-MM-dd} {slot.StartTime:HH\\:mm}",
                Amount = player.Fee,
                CurrencyCode = booking.CurrencyCode,
                Status = GolfChargeStatus.Pending,
                ProductId = player.PlayerType == GolfPlayerType.Member
                    ? slot.Tee.GreenFeeProductId
                    : slot.Tee.GuestFeeProductId,
                CreatedAtUtc = _clock.UtcNow,
            };

            // The Booking navigation links the new row to its parent; the booking is already
            // tracked, so the entity must also be registered explicitly or EF would treat
            // the collection append as a change to a row that does not exist.
            charge.Booking = booking;
            _context.GolfCharges.Add(charge);
        }

        return Task.CompletedTask;
    }

    private static void ApplyPlayerStatus(GolfBookingEntity booking, GolfBookingStatus status)
    {
        var playerStatus = GolfDomain.PlayerStatusFor(status);
        foreach (var player in booking.Players)
        {
            player.Status = playerStatus;
        }
    }

    private static void CancelPendingCharges(GolfBookingEntity booking, string reason)
    {
        foreach (var charge in booking.Charges.Where(c => c.Status == GolfChargeStatus.Pending))
        {
            charge.Status = GolfChargeStatus.Cancelled;
            charge.CancellationReason = reason;
        }
    }

    // ---------------------------------------------------------------------
    // Projection
    // ---------------------------------------------------------------------

    internal static GolfBookingDto ToDto(GolfBookingEntity booking)
    {
        var players = booking.Players
            .OrderBy(p => p.Position)
            .Select(p => new GolfBookingPlayerDto(
                p.Id,
                p.BookingId,
                p.PlayerType,
                p.MemberId,
                p.Member?.MemberNumber,
                p.Member is null ? null : PosServiceHelpers.MemberDisplayName(p.Member),
                p.GolfGuestId,
                p.GolfGuest?.FullName,
                p.Fee,
                p.CurrencyCode,
                p.AccountTransactionId,
                p.ReceivableId,
                p.Status,
                p.Position))
            .ToList();

        var charges = booking.Charges
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new GolfBookingChargeDto(
                c.Id,
                c.BookingId,
                c.BookingPlayerId,
                c.ChargeType,
                c.Description,
                c.Amount,
                c.CurrencyCode,
                c.Status,
                c.ProductId,
                c.AccountTransactionId,
                c.ReceivableId,
                c.JournalId,
                c.CreatedAtUtc))
            .ToList();

        return new GolfBookingDto(
            booking.Id,
            booking.Reference,
            booking.TeeSlotId,
            booking.TeeSlot.CourseId,
            booking.TeeSlot.TeeId,
            booking.TeeSlot.Course.Name,
            booking.TeeSlot.Tee.Name,
            booking.TeeSlot.Date,
            booking.TeeSlot.StartTime,
            booking.Status,
            booking.BookedByMemberId,
            players.Count,
            players.Sum(p => p.Fee),
            booking.CurrencyCode,
            booking.CancellationReason,
            booking.OverrideReason,
            booking.CapacityOverridden,
            booking.CreatedAtUtc,
            booking.ConfirmedAtUtc,
            booking.CheckedInAtUtc,
            booking.CompletedAtUtc,
            booking.CancelledAtUtc,
            booking.NoShowAtUtc,
            booking.CreatedByUserId,
            booking.CorrelationId,
            players,
            charges);
    }
}
