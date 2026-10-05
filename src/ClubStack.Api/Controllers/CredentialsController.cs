using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Credentials;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Credential lifecycle and card resolution (Phase 8 §7.4, Phase 2 CRED-001..010).
/// Every transition is validated against the approved state machine, recorded in
/// credential history and audited. Replace produces a new active credential bound
/// to the same member while retiring the old lost/stolen card.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/credentials")]
[Authorize]
public sealed class CredentialsController : AuditedControllerBase
{
    private readonly ICredentialService _credentials;

    public CredentialsController(
        ICredentialService credentials,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _credentials = credentials;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardView)]
    [ProducesResponseType(typeof(CredentialSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] Guid? memberId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _credentials.SearchAsync(
            new CredentialSearchInput(query, memberId, status, page, pageSize),
            cancellationToken);

        return Ok(new CredentialSearchResponse
        {
            Items = result.Items.Select(ToCredentialResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardView)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var credential = await _credentials.GetAsync(id, cancellationToken);
        return Ok(ToCredentialResponse(credential));
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardView)]
    [ProducesResponseType(typeof(IReadOnlyList<CredentialHistoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var history = await _credentials.GetHistoryAsync(id, cancellationToken);
        return Ok(history.Select(ToHistoryResponse).ToList());
    }

    [HttpPost("resolve")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardView)]
    [ProducesResponseType(typeof(ResolveCredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve([FromBody] ResolveCredentialRequest request, CancellationToken cancellationToken)
    {
        var resolved = await _credentials.ResolveAsync(request.CredentialValue, cancellationToken);
        return Ok(new ResolveCredentialResponse
        {
            CredentialId = resolved.CredentialId,
            CredentialNumber = resolved.CredentialNumber,
            Status = resolved.Status,
            MemberId = resolved.MemberId,
            MemberNumber = resolved.MemberNumber,
            MemberName = resolved.MemberName,
            MemberStatus = resolved.MemberStatus,
        });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardIssue)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Issue([FromBody] IssueCredentialRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var credential = await _credentials.IssueAsync(
            new IssueCredentialInput(request.CredentialType, request.CredentialValue, request.ExpiresAtUtc),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.Issue",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardActivate)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateCredentialRequest request, CancellationToken cancellationToken)
    {
        var credential = await _credentials.ActivateAsync(
            id,
            new ActivateCredentialInput(request.MemberId),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.Activate",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardSuspend)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] CredentialTransitionRequest request, CancellationToken cancellationToken)
    {
        var credential = await _credentials.SuspendAsync(
            id,
            new SuspendCredentialInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.Suspend",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    [HttpPost("{id:guid}/mark-lost")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardMarkLost)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkLost(Guid id, [FromBody] CredentialTransitionRequest request, CancellationToken cancellationToken)
    {
        var credential = await _credentials.MarkLostAsync(
            id,
            new LostCredentialInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.MarkLost",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    [HttpPost("{id:guid}/mark-stolen")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardMarkStolen)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkStolen(Guid id, [FromBody] CredentialTransitionRequest request, CancellationToken cancellationToken)
    {
        var credential = await _credentials.MarkStolenAsync(
            id,
            new StolenCredentialInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.MarkStolen",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardReactivate)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reactivate(Guid id, [FromBody] CredentialTransitionRequest request, CancellationToken cancellationToken)
    {
        var credential = await _credentials.ReactivateAsync(
            id,
            new ReactivateCredentialInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.Reactivate",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    [HttpPost("{id:guid}/replace")]
    [Authorize(Policy = "Permission:" + Permissions.CredentialsCardReplace)]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Replace(Guid id, [FromBody] ReplaceCredentialRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var credential = await _credentials.ReplaceAsync(
            id,
            new ReplaceCredentialInput(request.CredentialType, request.CredentialValue, request.ExpiresAtUtc, request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Credentials.Credential.Replace",
            "Credential",
            credential.Id.ToString(),
            credential.CredentialNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCredentialResponse(credential));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static CredentialResponse ToCredentialResponse(CredentialDto c) => new()
    {
        Id = c.Id,
        CredentialNumber = c.CredentialNumber,
        MemberId = c.MemberId,
        MemberNumber = c.MemberNumber,
        MemberName = c.MemberName,
        CredentialType = c.CredentialType,
        CredentialValue = c.CredentialValue,
        Status = c.Status,
        ActivatedAtUtc = c.ActivatedAtUtc,
        SuspendedAtUtc = c.SuspendedAtUtc,
        ExpiresAtUtc = c.ExpiresAtUtc,
        ReplacedByCredentialId = c.ReplacedByCredentialId,
        ReplacedByCredentialNumber = c.ReplacedByCredentialNumber,
        CreatedAtUtc = c.CreatedAtUtc,
        UpdatedAtUtc = c.UpdatedAtUtc,
    };

    private static CredentialHistoryResponse ToHistoryResponse(CredentialHistoryDto h) => new()
    {
        Id = h.Id,
        CredentialId = h.CredentialId,
        PreviousState = h.PreviousState,
        NewState = h.NewState,
        ChangedByUserId = h.ChangedByUserId,
        Reason = h.Reason,
        ChangedAtUtc = h.ChangedAtUtc,
    };
}