using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Golf;
using ClubStack.Contracts.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Golf courses, tees, the tee sheet and tee slots (GOLF-001 through GOLF-005).
///
/// <para>The two Phase 8 §11 read routes, <c>GET /golf/courses</c> and
/// <c>GET /golf/tee-sheet</c>, and the slot listing <c>GET /golf/tee-slots</c> are
/// the documented surface. The write routes below them are the derived
/// configuration commands Phase 2 GOLF-001 to GOLF-005 require, each gated on the
/// matching Golf permission.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/golf")]
[Authorize]
public sealed class GolfCoursesController : AuditedControllerBase
{
    private readonly IGolfCourseService _courses;
    private readonly IGolfTeeSlotService _teeSlots;

    public GolfCoursesController(
        IGolfCourseService courses,
        IGolfTeeSlotService teeSlots,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _courses = courses;
        _teeSlots = teeSlots;
    }

    // ---- GOLF-001: courses -------------------------------------------------

    /// <summary>Phase 8 §11 <c>GET /api/v1/golf/courses</c>.</summary>
    [HttpGet("courses")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseView)]
    [ProducesResponseType(typeof(GolfCourseSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchCourses(
        [FromQuery] GolfCourseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _courses.SearchCoursesAsync(
            new GolfCourseSearchInput(request.Code, request.Status, request.Page, request.PageSize),
            cancellationToken);

        return Ok(new GolfCourseSearchResponse
        {
            Items = result.Items.Select(ToCourseResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("courses/{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseView)]
    [ProducesResponseType(typeof(GolfCourseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourse(Guid id, CancellationToken cancellationToken)
        => Ok(ToCourseResponse(await _courses.GetCourseAsync(id, cancellationToken)));

    [HttpPost("courses")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseManage)]
    [ProducesResponseType(typeof(GolfCourseResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCourse(
        [FromBody] CreateGolfCourseRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var course = await _courses.CreateCourseAsync(
            new CreateGolfCourseInput(
                request.Code,
                request.Name,
                request.NumberOfHoles,
                request.CurrencyCode,
                request.Description,
                request.Holes
                    .Select(h => new CreateGolfHoleInput(h.HoleNumber, h.Par, h.Yardage, h.Name))
                    .ToList()),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.Course.Create",
            "GolfCourse",
            course.Id.ToString(),
            course.Code,
            metadataJson: $"holes={course.NumberOfHoles};{course.CurrencyCode}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetCourse), new { id = course.Id }, ToCourseResponse(course));
    }

    /// <summary>
    /// GOLF-001: a course is inactivated rather than deleted, so historic bookings
    /// keep a valid reference (Phase 3 §79).
    /// </summary>
    [HttpPost("courses/{id:guid}/status")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseManage)]
    [ProducesResponseType(typeof(GolfCourseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCourseStatus(
        Guid id,
        [FromBody] SetGolfCourseStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var course = await _courses.SetCourseStatusAsync(id, request.Status, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Course.SetStatus",
            "GolfCourse",
            course.Id.ToString(),
            course.Code,
            metadataJson: $"status={course.Status}",
            cancellationToken: cancellationToken);

        return Ok(ToCourseResponse(course));
    }

    // ---- GOLF-001/Phase 3 §41: tees ---------------------------------------

    [HttpPost("courses/{courseId:guid}/tees")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseManage)]
    [ProducesResponseType(typeof(GolfTeeResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTee(
        Guid courseId,
        [FromBody] CreateGolfTeeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tee = await _courses.CreateTeeAsync(courseId, ToTeeInput(request), ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Tee.Create",
            "GolfTee",
            tee.Id.ToString(),
            tee.Code,
            metadataJson:
                $"course={courseId};capacity={tee.Capacity};membersOnly={tee.MembersOnly};guestsAllowed={tee.GuestsAllowed}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetTee), new { teeId = tee.Id }, ToTeeResponse(tee));
    }

    [HttpGet("tees/{teeId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseView)]
    [ProducesResponseType(typeof(GolfTeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTee(Guid teeId, CancellationToken cancellationToken)
    {
        var tee = await _courses.GetTeeAsync(teeId, cancellationToken);

        return Ok(ToTeeResponse(tee));
    }

    [HttpPut("tees/{teeId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseManage)]
    [ProducesResponseType(typeof(GolfTeeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTee(
        Guid teeId,
        [FromBody] CreateGolfTeeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tee = await _courses.UpdateTeeAsync(teeId, ToTeeInput(request), ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Tee.Update",
            "GolfTee",
            tee.Id.ToString(),
            tee.Code,
            metadataJson: $"capacity={tee.Capacity};minimumPlayers={tee.MinimumPlayers};maximumPlayers={tee.MaximumPlayers}",
            cancellationToken: cancellationToken);

        return Ok(ToTeeResponse(tee));
    }

    [HttpPost("tees/{teeId:guid}/status")]
    [Authorize(Policy = "Permission:" + Permissions.GolfCourseManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetTeeStatus(
        Guid teeId,
        [FromBody] SetGolfTeeStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _courses.SetTeeStatusAsync(teeId, request.Status, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.Tee.SetStatus",
            "GolfTee",
            teeId.ToString(),
            metadataJson: $"status={request.Status}",
            cancellationToken: cancellationToken);

        return NoContent();
    }

    // ---- GOLF-002/003/004/005: tee sheet and slots -----------------------

    /// <summary>
    /// Phase 8 §11 <c>GET /api/v1/golf/tee-sheet</c>. Availability is derived per
    /// Phase 9 §12 from course, tee, date, slot, capacity, existing bookings,
    /// restrictions and closure/blocking.
    /// </summary>
    [HttpGet("tee-sheet")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSheetView)]
    [ProducesResponseType(typeof(GolfTeeSlotSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeeSheet(
        [FromQuery] GolfTeeSheetRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _teeSlots.GetTeeSheetAsync(
            new GolfTeeSheetInput(
                request.Date,
                request.CourseId,
                request.TeeId,
                request.BookableOnly,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(ToSlotSearchResponse(result));
    }

    /// <summary>Phase 8 §11 <c>GET /api/v1/golf/tee-slots</c>.</summary>
    [HttpGet("tee-slots")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSheetView)]
    [ProducesResponseType(typeof(GolfTeeSlotSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchTeeSlots(
        [FromQuery] GolfTeeSheetRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _teeSlots.GetTeeSheetAsync(
            new GolfTeeSheetInput(
                request.Date,
                request.CourseId,
                request.TeeId,
                request.BookableOnly,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(ToSlotSearchResponse(result));
    }

    [HttpGet("tee-slots/{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSheetView)]
    [ProducesResponseType(typeof(GolfTeeSlotResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeeSlot(Guid id, CancellationToken cancellationToken)
        => Ok(ToSlotResponse(await _teeSlots.GetTeeSlotAsync(id, cancellationToken)));

    [HttpPost("tee-slots")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSlotManage)]
    [ProducesResponseType(typeof(GolfTeeSlotResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTeeSlot(
        [FromBody] CreateGolfTeeSlotRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var slot = await _teeSlots.CreateTeeSlotAsync(
            new CreateGolfTeeSlotInput(
                request.TeeId,
                request.Date,
                request.StartTime,
                request.DurationMinutes,
                request.Capacity,
                request.MaximumPlayers,
                request.Status),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.TeeSlot.Create",
            "GolfTeeSlot",
            slot.Id.ToString(),
            $"{slot.TeeCode} {slot.Date:yyyy-MM-dd} {slot.StartTime:HH\\:mm}",
            metadataJson: $"capacity={slot.Capacity};status={slot.Status}",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetTeeSlot), new { id = slot.Id }, ToSlotResponse(slot));
    }

    /// <summary>GOLF-003: generate a run of slots for a date range.</summary>
    [HttpPost("tee-slots/generate")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSlotManage)]
    [ProducesResponseType(typeof(GolfTeeSlotSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateTeeSlots(
        [FromBody] GenerateGolfTeeSlotsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _teeSlots.GenerateTeeSlotsAsync(
            new GenerateGolfTeeSlotsInput(
                request.TeeId,
                request.FromDate,
                request.ToDate,
                request.FirstStartTime,
                request.LastStartTime,
                request.IntervalMinutes,
                request.Capacity,
                request.DurationMinutes),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Golf.TeeSlot.Generate",
            "GolfTee",
            request.TeeId.ToString(),
            metadataJson:
                $"from={request.FromDate:yyyy-MM-dd};to={request.ToDate:yyyy-MM-dd};interval={request.IntervalMinutes};generated={result.TotalCount}",
            cancellationToken: cancellationToken);

        return Ok(ToSlotSearchResponse(result));
    }

    /// <summary>
    /// GOLF-002: operational closure/unavailability of a slot. A blocked slot is
    /// never bookable, which is how the club publishes a closure period.
    /// </summary>
    [HttpPost("tee-slots/{id:guid}/status")]
    [Authorize(Policy = "Permission:" + Permissions.GolfTeeSlotManage)]
    [ProducesResponseType(typeof(GolfTeeSlotResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetTeeSlotStatus(
        Guid id,
        [FromBody] SetGolfTeeSlotStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var slot = await _teeSlots.SetTeeSlotStatusAsync(id, request.Status, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Golf.TeeSlot.SetStatus",
            "GolfTeeSlot",
            slot.Id.ToString(),
            $"{slot.TeeCode} {slot.Date:yyyy-MM-dd} {slot.StartTime:HH\\:mm}",
            metadataJson: $"status={slot.Status}",
            cancellationToken: cancellationToken);

        return Ok(ToSlotResponse(slot));
    }

    private static CreateGolfTeeInput ToTeeInput(CreateGolfTeeRequest request) => new(
        request.Code,
        request.Name,
        request.Capacity,
        request.MembersOnly,
        request.GuestsAllowed,
        request.MinimumPlayers,
        request.MaximumPlayers,
        request.GreenFeeProductId,
        request.GuestFeeProductId,
        request.Notes);

    private static GolfTeeSlotSearchResponse ToSlotSearchResponse(GolfTeeSlotSearchResult result) => new()
    {
        Items = result.Items.Select(ToSlotResponse).ToList(),
        TotalCount = result.TotalCount,
        Page = result.Page,
        PageSize = result.PageSize,
    };

    internal static GolfTeeSlotResponse ToSlotResponse(GolfTeeSlotDto s) => new()
    {
        Id = s.Id,
        CourseId = s.CourseId,
        TeeId = s.TeeId,
        CourseCode = s.CourseCode,
        CourseName = s.CourseName,
        TeeCode = s.TeeCode,
        TeeName = s.TeeName,
        Date = s.Date,
        StartTime = s.StartTime,
        DurationMinutes = s.DurationMinutes,
        Capacity = s.Capacity,
        TakenPlayers = s.TakenPlayers,
        OpenPlayers = s.OpenPlayers,
        Status = s.Status,
        MaximumPlayers = s.MaximumPlayers,
        MembersOnly = s.MembersOnly,
        GuestsAllowed = s.GuestsAllowed,
        CurrencyCode = s.CurrencyCode,
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
    };

    internal static GolfTeeResponse ToTeeResponse(GolfTeeDto t) => new()
    {
        Id = t.Id,
        CourseId = t.CourseId,
        Code = t.Code,
        Name = t.Name,
        Capacity = t.Capacity,
        Status = t.Status,
        MembersOnly = t.MembersOnly,
        GuestsAllowed = t.GuestsAllowed,
        MinimumPlayers = t.MinimumPlayers,
        MaximumPlayers = t.MaximumPlayers,
        GreenFeeProductId = t.GreenFeeProductId,
        GuestFeeProductId = t.GuestFeeProductId,
        Notes = t.Notes,
        CreatedAtUtc = t.CreatedAtUtc,
        UpdatedAtUtc = t.UpdatedAtUtc,
    };

    internal static GolfCourseResponse ToCourseResponse(GolfCourseDto c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        NumberOfHoles = c.NumberOfHoles,
        Status = c.Status,
        CurrencyCode = c.CurrencyCode,
        Description = c.Description,
        CreatedAtUtc = c.CreatedAtUtc,
        UpdatedAtUtc = c.UpdatedAtUtc,
        Holes = c.Holes
            .Select(h => new GolfHoleResponse
            {
                Id = h.Id,
                CourseId = h.CourseId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Name = h.Name,
            })
            .ToList(),
        Tees = c.Tees.Select(ToTeeResponse).ToList(),
    };

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
