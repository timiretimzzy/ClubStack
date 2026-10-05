using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Contracts.Common;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Pos;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Cashier sessions (D5 Cashiering). A session is opened with a closing float,
/// activated by the first completed sale, tracked with cash movements, closed
/// against the declared float (expected-vs-declared variance) and reconciled
/// (variance approval) by a supervisor.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/cashier-sessions")]
[Authorize]
public sealed class CashierSessionsController : AuditedControllerBase
{
    private readonly ICashierSessionService _sessions;

    public CashierSessionsController(
        ICashierSessionService sessions,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _sessions = sessions;
    }

[HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.CashieringSessionOpen)]
    [ProducesResponseType(typeof(CashierSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Open([FromBody] OpenCashierSessionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var session = await _sessions.OpenAsync(
            new OpenSessionInput(request.RegisterId, request.OpeningFloat, request.Note),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Cashiering.Session.Open",
            "CashierSession",
            session.Id.ToString(),
            session.SessionNumber,
            cancellationToken: cancellationToken);

        return Ok(ToSessionResponse(session));
    }

[HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.CashieringSessionView)]
    [ProducesResponseType(typeof(CashierSessionSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? registerId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessions.SearchAsync(
            new CashierSessionSearchInput(registerId, status, page, pageSize),
            cancellationToken);

        return Ok(new CashierSessionSearchResponse
        {
            Items = result.Items.Select(ToSessionResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    
    [ProducesResponseType(typeof(CashierSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(id, cancellationToken);
        return Ok(ToSessionResponse(session));
    }

[HttpPost("{id:guid}/movements")]
    [Authorize(Policy = "Permission:" + Permissions.CashieringCashMovementCreate)]
    [ProducesResponseType(typeof(CashierSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddCashMovement(Guid id, [FromBody] CashMovementRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var session = await _sessions.AddCashMovementAsync(
            id,
            new CashMovementInput(request.MovementType, request.Amount, request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Cashiering.Session.Movement",
            "CashierSession",
            session.Id.ToString(),
            session.SessionNumber,
            cancellationToken: cancellationToken);

        return Ok(ToSessionResponse(session));
    }

[HttpPost("{id:guid}/close")]
    [Authorize(Policy = "Permission:" + Permissions.CashieringSessionClose)]
    [ProducesResponseType(typeof(CashierSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCashierSessionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var session = await _sessions.CloseAsync(
            id,
            new CloseSessionInput(request.DeclaredCash),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Cashiering.Session.Close",
            "CashierSession",
            session.Id.ToString(),
            session.SessionNumber,
            cancellationToken: cancellationToken);

        return Ok(ToSessionResponse(session));
    }

    [HttpPost("{id:guid}/reconcile")]
    [Authorize(Policy = "Permission:" + Permissions.CashieringSessionReconcile)]
    [ProducesResponseType(typeof(CashierSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessions.ReconcileAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Cashiering.Session.Reconcile",
            "CashierSession",
            session.Id.ToString(),
            session.SessionNumber,
            cancellationToken: cancellationToken);

        return Ok(ToSessionResponse(session));
    }

    private static CashierSessionResponse ToSessionResponse(CashierSessionDto s) => new()
    {
        Id = s.Id,
        SessionNumber = s.SessionNumber,
        RegisterId = s.RegisterId,
        RegisterCode = s.RegisterCode,
        CashierUserId = s.CashierUserId,
        Status = s.Status,
        OpeningFloat = s.OpeningFloat,
        ExpectedCash = s.ExpectedCash,
        DeclaredCash = s.DeclaredCash,
        Variance = s.Variance,
        VarianceApproved = s.VarianceApproved,
        Note = s.Note,
        OpenedAtUtc = s.OpenedAtUtc,
        StartedAtUtc = s.StartedAtUtc,
        ClosingStartedAtUtc = s.ClosingStartedAtUtc,
        ClosedAtUtc = s.ClosedAtUtc,
        ReconciledAtUtc = s.ReconciledAtUtc,
        ReconciledByUserId = s.ReconciledByUserId,
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
        Movements = s.Movements.Select(m => new CashMovementResponse
        {
            Id = m.Id,
            SessionId = m.SessionId,
            MovementType = m.MovementType,
            Amount = m.Amount,
            SignedAmount = m.SignedAmount,
            Reason = m.Reason,
            CreatedByUserId = m.CreatedByUserId,
            CreatedAtUtc = m.CreatedAtUtc,
        }).ToList(),
    };

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}