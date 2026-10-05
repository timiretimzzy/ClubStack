using ClubStack.Application.Accounts;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Application.Golf;
using ClubStack.Application.Idempotency;
using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using ClubStack.Infrastructure.Pos;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Golf;

/// <summary>
/// Golf charges: GOLF-016 (calculate applicable fees), GOLF-017 (apply/record
/// charges), XMOD-004 (charge the member account) and XMOD-005 (finance
/// traceability).
///
/// <para><b>The financial path.</b> Phase 3 §75 defines a golf charge as
/// "Golf Charge + Account/Payment relationship + Financial Source Record", so a
/// posted golf charge must be visible from three places: the golf charge row, the
/// D3 member account, and the D6 finance ledger. This service writes all three
/// inside one <see cref="IUnitOfWork"/> transaction, so a booking can never be
/// charged to a member account without the matching receivable, or the reverse.</para>
///
/// <para><b>Reusing D6 rather than extending it.</b> A green fee billed to a
/// member account is a member-account charge: it debits the member receivable and
/// credits club revenue, which is exactly the mapping of the existing
/// <see cref="FinancialPostingRuleCodes.PosMemberAccountCharge"/> rule (Phase 8
/// §14.2). D6 B7 forbids a ninth rule identity, so D7 routes through that rule
/// instead of inventing a golf-specific one. Only the source type and id differ
/// (<c>GolfBooking</c> / booking id, per Phase 3's GOLF_BOOKING source record).</para>
/// </summary>
public sealed class EntityFrameworkGolfChargeService : IGolfChargeService
{
    /// <summary>
    /// Phase 3 §75 source type for the financial source record: <c>GOLF_BOOKING /
    /// booking-id</c>, expressed in the same PascalCase shape D6 already uses for
    /// <c>PosSale</c> so the source columns are queried consistently.
    /// </summary>
    public const string GolfBookingSourceType = "GolfBooking";

    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberAccountService _accounts;
    private readonly IReceivableService _receivables;
    private readonly IIdempotencyStore _idempotency;
    private readonly ISystemClock _clock;

    public EntityFrameworkGolfChargeService(
        ClubStackDbContext context,
        IUnitOfWork unitOfWork,
        IMemberAccountService accounts,
        IReceivableService receivables,
        IIdempotencyStore idempotency,
        ISystemClock clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _accounts = accounts;
        _receivables = receivables;
        _idempotency = idempotency;
        _clock = clock;
    }

    public async Task<GolfChargeSearchResult> SearchChargesAsync(
        GolfChargeSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = GolfServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<GolfChargeEntity> query = _context.GolfCharges.AsNoTracking();

        if (input.BookingId is { } bookingId)
        {
            query = query.Where(c => c.BookingId == bookingId);
        }

        if (input.ChargeType is { } chargeType)
        {
            query = query.Where(c => c.ChargeType == chargeType);
        }

        if (input.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        if (input.MemberId is { } memberId)
        {
            query = query.Where(c => c.Booking.BookedByMemberId == memberId);
        }

        var total = await query.CountAsync(cancellationToken);
        var totalAmount = await query
            .Where(c => c.Status == GolfChargeStatus.Charged)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0m;

        var charges = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = charges
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

        return new GolfChargeSearchResult(items, total, totalAmount, page, pageSize);
    }

    public async Task<GolfBookingDto> AddChargeAsync(
        Guid bookingId,
        AddGolfChargeInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GolfIdempotency.NormalizeKey(input.IdempotencyKey);
        var requestHash = idempotencyKey is null
            ? null
            : IdempotencyRequestHash.Compute(
                $"{bookingId:N}|{input.ProductId:N}|{input.Quantity}|{input.BookingPlayerId?.ToString("N") ?? "-"}|{input.Description}");

        if (idempotencyKey is not null)
        {
            var (created, existing) = await _idempotency.TryAcquireAsync(
                idempotencyKey,
                GolfIdempotency.AddChargeOperation,
                actorUserId,
                requestHash,
                _clock.UtcNow.ToString("O"),
                cancellationToken);
            if (!created)
            {
                return GolfIdempotency.ReplayStoredOrConflict<GolfBookingDto>(
                    existing, "golf charge addition", requestHash);
            }
        }

        try
        {
            var updated = await AddChargeCoreAsync(bookingId, input, actorUserId, cancellationToken);

            if (idempotencyKey is not null)
            {
                await _idempotency.CompleteAsync(
                    idempotencyKey,
                    GolfIdempotency.AddChargeOperation,
                    actorUserId,
                    GolfJson.Serialize(updated),
                    cancellationToken);
            }

            return updated;
        }
        catch
        {
            await GolfIdempotency.MarkFailedAsync(
                _idempotency,
                idempotencyKey,
                GolfIdempotency.AddChargeOperation,
                actorUserId,
                "The golf charge was not added.",
                cancellationToken);
            throw;
        }
    }

    private async Task<GolfBookingDto> AddChargeCoreAsync(
        Guid bookingId,
        AddGolfChargeInput input,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var booking = await _context.GolfBookings
            .Include(b => b.TeeSlot).ThenInclude(s => s.Course)
            .Include(b => b.TeeSlot).ThenInclude(s => s.Tee)
            .Include(b => b.Players).ThenInclude(p => p.Member).ThenInclude(m => m!.Person)
            .Include(b => b.Players).ThenInclude(p => p.GolfGuest)
            .Include(b => b.Charges)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException($"Golf booking '{bookingId}' was not found.");

        if (booking.Status is GolfBookingStatus.Cancelled or GolfBookingStatus.Draft)
        {
            // GOLF-017 charges belong to a live booking. A draft has no confirmed
            // composition, and a cancelled booking's charges were voided.
            throw new ConflictException(
                $"Booking {booking.Reference} is {booking.Status}; charges cannot be added to it.");
        }

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == input.ProductId, cancellationToken)
            ?? throw new ValidationException(
                "The golf charge could not be added.",
                GolfServiceHelpers.FieldError("productId", $"Product '{input.ProductId}' does not exist in the catalogue."));

        if (input.BookingPlayerId is { } playerId && booking.Players.All(p => p.Id != playerId))
        {
            throw new ValidationException(
                "The golf charge could not be added.",
                GolfServiceHelpers.FieldError("bookingPlayerId", $"Booking '{booking.Reference}' has no player '{playerId}'."));
        }

        // A booking is charged in the course currency, and golf never converts an amount.
        // Catching the mismatch here, while the operator is still on the charge, is far
        // better than surfacing it as an unpostable booking later.
        if (!string.Equals(product.CurrencyCode, booking.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "The golf charge could not be added.",
                GolfServiceHelpers.FieldError(
                    "productId",
                    $"Product '{product.Sku}' is priced in {product.CurrencyCode} but booking '{booking.Reference}' is charged in {booking.CurrencyCode}."));
        }

        // Carts, caddies and rentals (Phase 6 §14) are sold from the D4 catalogue, so
        // the amount is the configured unit price times the quantity, resolved with
        // the same helper D5 uses. No golf-specific price is introduced.
        var quantity = input.Quantity ?? 1;
        if (quantity < 1 || quantity > 100)
        {
            throw new ValidationException(
                "The golf charge could not be added.",
                GolfServiceHelpers.FieldError("quantity", "A quantity must be between 1 and 100."));
        }

        var prices = await _context.ProductPrices
            .AsNoTracking()
            .Where(p => p.ProductId == product.Id)
            .ToListAsync(cancellationToken);

        var (unitAmount, priceSource) = EntityFrameworkPosProductService.ResolveSellingPrice(
            product.Id,
            prices,
            booking.BookedByMemberId);

        var charge = new GolfChargeEntity
        {
            BookingId = booking.Id,
            BookingPlayerId = input.BookingPlayerId,
            // Carts, caddies and rentals (Phase 6 §14) are sold from the D4
            // catalogue, so an additional charge is always an
            // <see cref="GolfChargeType.Additional"/> line. Golf does not
            // classify products into cart/caddie/rental kinds, because no
            // authoritative document defines such a taxonomy; the product's own
            // category is the club's configuration and stays in D4.
            ChargeType = GolfChargeType.Additional,
            Description = string.IsNullOrWhiteSpace(input.Description)
                ? product.Name
                : input.Description.Trim(),
            Amount = GolfDomain.NormalizeAmount(unitAmount * quantity),
            CurrencyCode = product.CurrencyCode,
            Status = GolfChargeStatus.Pending,
            ProductId = product.Id,
            CreatedAtUtc = _clock.UtcNow,
        };

        if (charge.Amount <= 0m)
        {
            throw new ValidationException(
                "The golf charge could not be added.",
                GolfServiceHelpers.FieldError(
                    "productId",
                    $"Product '{product.Sku}' has no {priceSource} price configured, so no amount can be charged."));
        }

        // The Booking navigation is what links the new row to its parent. Appending it to
        // the loaded collection as well would list it twice in the returned DTO, because
        // EF's fixup adds it again once the foreign key is written.
        charge.Booking = booking;
        _context.GolfCharges.Add(charge);
        await _context.SaveChangesAsync(cancellationToken);

        return EntityFrameworkGolfBookingService.ToDto(booking);
    }

    public async Task<GolfBookingDto> WaiveChargeAsync(
        Guid bookingId,
        Guid chargeId,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var charge = await _context.GolfCharges
            .Include(c => c.Booking)
            .FirstOrDefaultAsync(c => c.Id == chargeId && c.BookingId == bookingId, cancellationToken)
            ?? throw new NotFoundException($"Golf booking '{bookingId}' has no charge '{chargeId}'.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                "The golf charge could not be waived.",
                GolfServiceHelpers.FieldError("reason", "A waiver reason is required."));
        }

        if (charge.Status != GolfChargeStatus.Pending)
        {
            // D6 B4: a posted journal is immutable, so a waiver cannot quietly erase
            // a charge that is already on the member's ledger.
            throw new ConflictException(
                $"Charge '{chargeId}' is {charge.Status} and cannot be waived. Cancel or credit the booking instead.");
        }

        charge.Status = GolfChargeStatus.Waived;
        charge.WaivedReason = reason.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return await GetBookingAsync(bookingId, cancellationToken);
    }

    public async Task<GolfBookingDto> PostBookingChargesAsync(
        Guid bookingId,
        PostGolfBookingChargesInput input,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GolfIdempotency.NormalizeKey(input.IdempotencyKey);
        // Posting has no payload beyond its target, so the booking id is the whole identity
        // of the request. Without it a key reused against another booking would replay the
        // first booking's receivables.
        var requestHash = idempotencyKey is null ? null : IdempotencyRequestHash.Compute($"{bookingId:N}");

        if (idempotencyKey is not null)
        {
            var (created, existing) = await _idempotency.TryAcquireAsync(
                idempotencyKey, GolfIdempotency.PostChargesOperation, actorUserId, requestHash, correlationId, cancellationToken);
            if (!created)
            {
                return GolfIdempotency.ReplayStoredOrConflict<GolfBookingDto>(
                    existing, "golf charge posting", requestHash);
            }
        }

        try
        {
            var posted = await PostBookingChargesCoreAsync(bookingId, actorUserId, correlationId, cancellationToken);

            if (idempotencyKey is not null)
            {
                await _idempotency.CompleteAsync(
                    idempotencyKey,
                    GolfIdempotency.PostChargesOperation,
                    actorUserId,
                    GolfJson.Serialize(posted),
                    cancellationToken);
            }

            return posted;
        }
        catch
        {
            await GolfIdempotency.MarkFailedAsync(
                _idempotency,
                idempotencyKey,
                GolfIdempotency.PostChargesOperation,
                actorUserId,
                "The golf charges were not posted.",
                cancellationToken);
            throw;
        }
    }

    private async Task<GolfBookingDto> PostBookingChargesCoreAsync(
        Guid bookingId,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // XMOD-004: charge the member account. ACC-001 creates the account on a member's
        // first use, and that creation is only persisted when the account service owns the
        // transaction. Resolving it here, before the golf transaction opens, means the row
        // already exists when the charge is written into that transaction. Reading the
        // member id outside the lock is safe: a booking's member never changes.
        var bookedByMemberId = await _context.GolfBookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => (Guid?)b.BookedByMemberId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Golf booking '{bookingId}' was not found.");

        var account = await _accounts.GetAccountForMemberAsync(bookedByMemberId, cancellationToken);

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            // Lock the booking for the whole posting so two desks pressing "post
            // charges" at the same moment cannot both create an account charge and a
            // receivable for the same booking. The lock is taken before the read, so the
            // pending-charge set used to build the total is the locked, current one.
            await _context.Database
                .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM golf_bookings WHERE \"Id\" = {bookingId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);

            var booking = await _context.GolfBookings
                .Include(b => b.TeeSlot).ThenInclude(s => s.Course)
                .Include(b => b.TeeSlot).ThenInclude(s => s.Tee)
                .Include(b => b.Players).ThenInclude(p => p.Member).ThenInclude(m => m!.Person)
                .Include(b => b.Players).ThenInclude(p => p.GolfGuest)
                .Include(b => b.Charges)
                .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
                ?? throw new NotFoundException($"Golf booking '{bookingId}' was not found.");

            if (booking.Status is GolfBookingStatus.Draft or GolfBookingStatus.Cancelled)
            {
                throw new ConflictException(
                    $"Booking {booking.Reference} is {booking.Status}; its charges cannot be posted.");
            }

            var pending = booking.Charges.Where(c => c.Status == GolfChargeStatus.Pending).ToList();
            if (pending.Count == 0)
            {
                var alreadyCharged = booking.Charges.Any(c => c.Status == GolfChargeStatus.Charged);
                throw alreadyCharged
                    ? new ConflictException($"Booking {booking.Reference} has already been charged.")
                    : new ConflictException($"Booking {booking.Reference} has no pending charges to post.");
            }

            var total = GolfDomain.NormalizeAmount(pending.Sum(c => c.Amount));
            if (total <= 0m)
            {
                throw new ConflictException($"Booking {booking.Reference} has no positive charges to post.");
            }

            // One posting produces one account charge and one receivable, both denominated
            // in a single currency. Summing mixed-currency lines would post a total that
            // means nothing, so a booking whose charges are not all in the course currency
            // is refused instead of silently mis-stated.
            var currencies = pending
                .Select(c => c.CurrencyCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (currencies.Count > 1 || !string.Equals(currencies[0], booking.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    $"Booking {booking.Reference} has charges in {string.Join(", ", currencies)}, which cannot be posted to a " +
                    $"{booking.CurrencyCode} account charge. Golf never converts an amount; reconcile the charge currencies first.");
            }

            var currencyCode = pending[0].CurrencyCode;

            // XMOD-004: charge the member account that was resolved above. Golf never creates
            // a second account for a member, it uses the one ACC-001 owns.
            var transaction = await _accounts.ChargeAsync(
                account.Id,
                new ChargeInput(
                    $"Golf booking {booking.Reference}",
                    total,
                    currencyCode,
                    booking.Reference,
                    GolfBookingSourceType,
                    booking.Id,
                    booking.ConfirmedAtUtc ?? booking.CreatedAtUtc),
                actorUserId,
                cancellationToken);

            // XMOD-005 / Phase 3 §75: the financial source record. The receivable is
            // created through the existing D6 service, so the journal is produced by
            // the configured mapping table rather than by a golf-specific account.
            var receivable = await _receivables.CreateAsync(
                new CreateReceivableInput(
                    booking.BookedByMemberId,
                    GolfBookingSourceType,
                    booking.Id,
                    booking.Reference,
                    total,
                    booking.ConfirmedAtUtc ?? booking.CreatedAtUtc,
                    null,
                    currencyCode,
                    null,
                    null,
                    $"Golf charges {booking.Reference}",
                    FinancialPostingRuleCodes.PosMemberAccountCharge,
                    null),
                actorUserId,
                cancellationToken);

            foreach (var charge in pending)
            {
                charge.Status = GolfChargeStatus.Charged;
                charge.AccountTransactionId = transaction.Id;
                charge.ReceivableId = receivable.Id;
                charge.JournalId = receivable.JournalId;
            }

            // Phase 3 §44 records the payment/account reference on the player as
            // well, so a player's charge is traceable from the player row.
            foreach (var player in booking.Players)
            {
                var playerCharges = pending.Where(c => c.BookingPlayerId == player.Id).ToList();
                if (playerCharges.Count == 0)
                {
                    continue;
                }

                player.AccountTransactionId = playerCharges[0].AccountTransactionId;
                player.ReceivableId = playerCharges[0].ReceivableId;
            }

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return await GetBookingAsync(bookingId, cancellationToken);
    }

    private async Task<GolfBookingDto> GetBookingAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await _context.GolfBookings
            .AsNoTracking()
            .Include(b => b.TeeSlot).ThenInclude(s => s.Course)
            .Include(b => b.TeeSlot).ThenInclude(s => s.Tee)
            .Include(b => b.Players).ThenInclude(p => p.Member).ThenInclude(m => m!.Person)
            .Include(b => b.Players).ThenInclude(p => p.GolfGuest)
            .Include(b => b.Charges)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Golf booking '{id}' was not found.");

        return EntityFrameworkGolfBookingService.ToDto(booking);
    }
}
