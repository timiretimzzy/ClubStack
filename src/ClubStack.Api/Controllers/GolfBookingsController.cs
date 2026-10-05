using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Golf;
using ClubStack.Contracts.Common;
using ClubStack.Domain.Golf;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Golf bookings and golf charges (GOLF-006 through GOLF-022, XMOD-004, XMOD-005).
///
/// <para>The Phase 8 §11 booking routes are reproduced exactly. The
/// <c>complete</c>, player-management and charge routes are derived: Phase 8 §7.7
/// defines the Completed state and GOLF-017/XMOD-004 require charges to be applied
/// and recorded, but §11 lists no route for them, so they follow the existing
/// <c>{id}/action</c> convention used by the rest of the API.</para>
///
/// <para>Every command is audited, and the confirm route additionally requires
/// <c>Golf.Booking.Override</c> when the caller is about to confirm a booking over
/// capacity (GOLF-011).</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/golf")]
[Authorize]
public sealed class GolfBookingsController : AuditedControllerBase
{
    private readonly IGolfBookingService _bookings;
    private readonly IGolfChargeService _charges;
    private readonly IGolfGuestService _guests;
    private readonly IAuthorizationService _authorization;

    public GolfBookingsController(
        IGolfBookingService bookings,
        IGolfChargeService charges,
        IGolfGuestService guests,
        IAuthorizationService authorization,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _bookings = bookings;
        _charges = charges;
        _guests = guests;
        _authorization = authorization;
    }

    // ---- Bookings ---------------------------------------------------------

    [HttpGet("bookings")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingView)]
    [ProducesResponseType(typeof(GolfBookingSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchBookings(
        [FromQuery] GolfBookingSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bookings.SearchBookingsAsync(
            new GolfBookingSearchInput(
                request.FromDate,
                request.ToDate,
                request.CourseId,
                request.TeeId,
                request.MemberId,
                request.Status,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new GolfBookingSearchResponse
        {
            Items = result.Items.Select(ToBookingResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    /// <summary>Phase 8 §11 <c>GET /api/v1/golf/bookings/{id}</c>.</summary>
    [HttpGet("bookings/{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingView)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken)
        => Ok(ToBookingResponse(await _bookings.GetBookingAsync(id, cancellationToken)));

    /// <summary>
    /// Phase 8 §11 <c>POST /api/v1/golf/bookings</c>. The booking is created as a
    /// Draft: GOLF-010 validates availability at confirmation, so a draft hold does
    /// not consume capacity.
    /// </summary>
    [HttpPost("bookings")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingCreate)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBooking(
        [FromBody] CreateGolfBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var booking = await _bookings.CreateBookingAsync(
            new CreateGolfBookingInput(
                request.TeeSlotId,
                request.BookedByMemberId,
                request.Players
                    .Select(p => new GolfBookingPlayerInput(p.MemberId, p.GolfGuestId, p.Position))
                    .ToList(),
                request.BusinessDateUtc,
                request.IdempotencyKey),
            ActorUserId(),
            CorrelationId,
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.Create",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson:
                $"teeSlot={booking.TeeSlotId};players={booking.PlayerCount};member={booking.BookedByMemberId}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, ToBookingResponse(booking));
    }

    /// <summary>GOLF-007: add a player to a draft booking.</summary>
    [HttpPost("bookings/{id:guid}/players")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingEdit)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPlayer(
        Guid id,
        [FromBody] GolfBookingPlayerRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var booking = await _bookings.AddPlayerAsync(
            id,
            new GolfBookingPlayerInput(request.MemberId, request.GolfGuestId, request.Position),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.AddPlayer",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"players={booking.PlayerCount}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>GOLF-007: remove a player from a draft booking.</summary>
    [HttpDelete("bookings/{id:guid}/players/{playerId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingEdit)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemovePlayer(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var booking = await _bookings.RemovePlayerAsync(id, playerId, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.RemovePlayer",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"player={playerId};players={booking.PlayerCount}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>
    /// Phase 8 §11 <c>POST /api/v1/golf/bookings/{id}/confirm</c>. GOLF-010
    /// validates availability and GOLF-011 prevents overbooking; an overload is
    /// refused unless the caller holds the sensitive override permission and records
    /// a reason.
    /// </summary>
    [HttpPost("bookings/{id:guid}/confirm")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingEdit)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmBooking(
        Guid id,
        [FromBody] ConfirmGolfBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.OverrideCapacity && !await IsAuthorizedAsync(Permissions.GolfBookingOverride))
        {
            // Forbid rather than a 404/403 ambiguity: the caller's request was
            // understood, and only the sensitive permission is missing.
            return Forbid();
        }

        var booking = await _bookings.ConfirmBookingAsync(
            id,
            new ConfirmGolfBookingInput(
                request.Note,
                request.OverrideCapacity,
                request.OverrideReason,
                request.IdempotencyKey),
            ActorUserId(),
            CorrelationId,
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.Confirm",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson:
                $"players={booking.PlayerCount};total={booking.TotalFee};{booking.CurrencyCode};" +
                $"overridden={booking.CapacityOverridden};reason={booking.OverrideReason}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>GOLF-013: check-in on arrival.</summary>
    [HttpPost("bookings/{id:guid}/check-in")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingCheckIn)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn(
        Guid id,
        CancellationToken cancellationToken)
    {
        var booking = await _bookings.CheckInBookingAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.CheckIn",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"checkedInAt={booking.CheckedInAtUtc:O}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>
    /// Derived from Phase 8 §7.7: the Completed state is defined by the state
    /// machine but §11 lists no route for it.
    /// </summary>
    [HttpPost("bookings/{id:guid}/complete")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingEdit)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var booking = await _bookings.CompleteBookingAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.Complete",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"completedAt={booking.CompletedAtUtc:O}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>GOLF-020: cancellation, which frees the slot's places immediately.</summary>
    [HttpPost("bookings/{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingCancel)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelGolfBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var booking = await _bookings.CancelBookingAsync(
            id,
            new CancelGolfBookingInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.Cancel",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: request.Reason,
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>GOLF-022: the party did not arrive.</summary>
    [HttpPost("bookings/{id:guid}/no-show")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingEdit)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> NoShow(
        Guid id,
        CancellationToken cancellationToken)
    {
        var booking = await _bookings.NoShowBookingAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Booking.NoShow",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"noShowAt={booking.NoShowAtUtc:O}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    // ---- Golf charges (GOLF-017, XMOD-004, XMOD-005) ----------------------

    [HttpGet("charges")]
    [Authorize(Policy = "Permission:" + Permissions.GolfBookingView)]
    [ProducesResponseType(typeof(GolfChargeSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchCharges(
        [FromQuery] GolfChargeSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _charges.SearchChargesAsync(
            new GolfChargeSearchInput(
                request.BookingId,
                request.ChargeType,
                request.Status,
                request.MemberId,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new GolfChargeSearchResponse
        {
            Items = result.Items.Select(ToChargeResponse).ToList(),
            TotalCount = result.TotalCount,
            TotalAmount = result.TotalAmount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    /// <summary>
    /// GOLF-017: an additional charge priced from the D4 catalogue (cart, caddie,
    /// rental or any other configured product).
    /// </summary>
    [HttpPost("bookings/{id:guid}/charges")]
    [Authorize(Policy = "Permission:" + Permissions.GolfChargeCreate)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddCharge(
        Guid id,
        [FromBody] AddGolfChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var booking = await _charges.AddChargeAsync(
            id,
            new AddGolfChargeInput(
                request.ProductId,
                request.Quantity,
                request.BookingPlayerId,
                request.Description,
                request.IdempotencyKey),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Charge.Create",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"product={request.ProductId};quantity={request.Quantity ?? 1}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    /// <summary>
    /// XMOD-004/XMOD-005: post the booking's pending charges to the booking member's
    /// D3 account and raise the D6 receivable, in one transaction.
    /// </summary>
    [HttpPost("bookings/{id:guid}/charges/post")]
    [Authorize(Policy = "Permission:" + Permissions.GolfChargeCreate)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostCharges(
        Guid id,
        [FromBody] PostGolfBookingChargesRequest? request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var booking = await _charges.PostBookingChargesAsync(
            id,
            new PostGolfBookingChargesInput(request?.IdempotencyKey),
            ActorUserId(),
            CorrelationId,
            cancellationToken);

        var charged = booking.Charges.Where(c => c.Status == GolfChargeStatus.Charged).ToList();
        var total = charged.Sum(c => c.Amount);

        await RecordAuditAsync(
            "Golf.Charge.Post",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson:
                $"charges={charged.Count};total={total};{booking.CurrencyCode};" +
                $"receivable={charged.FirstOrDefault()?.ReceivableId};" +
                $"accountTransaction={charged.FirstOrDefault()?.AccountTransactionId}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    [HttpPost("bookings/{id:guid}/charges/{chargeId:guid}/waive")]
    [Authorize(Policy = "Permission:" + Permissions.GolfChargeCreate)]
    [ProducesResponseType(typeof(GolfBookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> WaiveCharge(
        Guid id,
        Guid chargeId,
        [FromBody] WaiveGolfChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var booking = await _charges.WaiveChargeAsync(
            id,
            chargeId,
            request.Reason,
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Charge.Waive",
            "GolfBooking",
            booking.Id.ToString(),
            booking.Reference,
            metadataJson: $"charge={chargeId};reason={request.Reason}",
            cancellationToken: cancellationToken);

        return Ok(ToBookingResponse(booking));
    }

    // ---- Guests (GOLF-008/GOLF-009) ---------------------------------------

    [HttpGet("guests")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSheetView)]
    [ProducesResponseType(typeof(GolfGuestSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchGuests(
        [FromQuery] GolfGuestSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(ToGuestSearchResponse(await _guests.SearchGuestsAsync(
            new GolfGuestSearchInput(request.Search, request.Status, request.Page, request.PageSize),
            cancellationToken)));

    [HttpPost("guests")]
    [Authorize(Policy = "Permission:" + Permissions.GolfGuestManage)]
    [ProducesResponseType(typeof(GolfGuestResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGuest(
        [FromBody] CreateGolfGuestRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var guest = await _guests.CreateGuestAsync(
            new CreateGolfGuestInput(request.FullName, request.ContactDetails, request.HostMemberId, request.Notes),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Guest.Create",
            "GolfGuest",
            guest.Id.ToString(),
            guest.Reference,
            metadataJson: $"host={guest.HostMemberId}",
            cancellationToken: cancellationToken);

        return Created($"/{ApiConstants.ApiRoutePrefix}/golf/guests/{guest.Id}", ToGuestResponse(guest));
    }

    [HttpGet("guests/{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSheetView)]
    [ProducesResponseType(typeof(GolfGuestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGuest(Guid id, CancellationToken cancellationToken)
        => Ok(ToGuestResponse(await _guests.GetGuestAsync(id, cancellationToken)));

    [HttpPost("guests/{id:guid}/status")]
    [Authorize(Policy = "Permission:" + Permissions.GolfGuestManage)]
    [ProducesResponseType(typeof(GolfGuestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetGuestStatus(
        Guid id,
        [FromBody] SetGolfGuestStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var guest = await _guests.SetGuestStatusAsync(id, request.Status, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Guest.SetStatus",
            "GolfGuest",
            guest.Id.ToString(),
            guest.Reference,
            metadataJson: $"status={guest.Status}",
            cancellationToken: cancellationToken);

        return Ok(ToGuestResponse(guest));
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<bool> IsAuthorizedAsync(string permission)
        => (await _authorization.AuthorizeAsync(User, $"Permission:{permission}")).Succeeded;

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static GolfBookingResponse ToBookingResponse(GolfBookingDto b) => new()
    {
        Id = b.Id,
        Reference = b.Reference,
        TeeSlotId = b.TeeSlotId,
        CourseId = b.CourseId,
        TeeId = b.TeeId,
        CourseName = b.CourseName,
        TeeName = b.TeeName,
        TeeDate = b.TeeDate,
        StartTime = b.StartTime,
        Status = b.Status,
        BookedByMemberId = b.BookedByMemberId,
        PlayerCount = b.PlayerCount,
        TotalFee = b.TotalFee,
        CurrencyCode = b.CurrencyCode,
        CancellationReason = b.CancellationReason,
        OverrideReason = b.OverrideReason,
        CapacityOverridden = b.CapacityOverridden,
        CreatedAtUtc = b.CreatedAtUtc,
        ConfirmedAtUtc = b.ConfirmedAtUtc,
        CheckedInAtUtc = b.CheckedInAtUtc,
        CompletedAtUtc = b.CompletedAtUtc,
        CancelledAtUtc = b.CancelledAtUtc,
        NoShowAtUtc = b.NoShowAtUtc,
        CreatedByUserId = b.CreatedByUserId,
        CorrelationId = b.CorrelationId,
        Players = b.Players
            .Select(p => new GolfBookingPlayerResponse
            {
                Id = p.Id,
                BookingId = p.BookingId,
                PlayerType = p.PlayerType,
                MemberId = p.MemberId,
                MemberNumber = p.MemberNumber,
                MemberName = p.MemberName,
                GolfGuestId = p.GolfGuestId,
                GuestName = p.GuestName,
                Fee = p.Fee,
                CurrencyCode = p.CurrencyCode,
                AccountTransactionId = p.AccountTransactionId,
                ReceivableId = p.ReceivableId,
                Status = p.Status,
                Position = p.Position,
            })
            .ToList(),
        Charges = b.Charges.Select(ToChargeResponse).ToList(),
    };

    internal static GolfBookingChargeResponse ToChargeResponse(GolfBookingChargeDto c) => new()
    {
        Id = c.Id,
        BookingId = c.BookingId,
        BookingPlayerId = c.BookingPlayerId,
        ChargeType = c.ChargeType,
        Description = c.Description,
        Amount = c.Amount,
        CurrencyCode = c.CurrencyCode,
        Status = c.Status,
        ProductId = c.ProductId,
        AccountTransactionId = c.AccountTransactionId,
        ReceivableId = c.ReceivableId,
        JournalId = c.JournalId,
        CreatedAtUtc = c.CreatedAtUtc,
    };

    internal static GolfGuestResponse ToGuestResponse(GolfGuestDto g) => new()
    {
        Id = g.Id,
        Reference = g.Reference,
        FullName = g.FullName,
        ContactDetails = g.ContactDetails,
        HostMemberId = g.HostMemberId,
        Status = g.Status,
        VisitCount = g.VisitCount,
        LastVisitedAtUtc = g.LastVisitedAtUtc,
        Notes = g.Notes,
        CreatedAtUtc = g.CreatedAtUtc,
        UpdatedAtUtc = g.UpdatedAtUtc,
    };

    private static GolfGuestSearchResponse ToGuestSearchResponse(GolfGuestSearchResult result) => new()
    {
        Items = result.Items.Select(ToGuestResponse).ToList(),
        TotalCount = result.TotalCount,
        Page = result.Page,
        PageSize = result.PageSize,
    };
}
