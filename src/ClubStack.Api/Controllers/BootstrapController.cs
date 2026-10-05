using System.Text;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Bootstrap;
using ClubStack.Application.Correlation;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Controlled first-run bootstrap (Phase 9 §27). Status is observable
/// anonymously; the create endpoints enforce the enable gate and refuse to
/// re-run once completed.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/bootstrap")]
public sealed class BootstrapController : AuditedControllerBase
{
    private readonly IBootstrapService _bootstrapService;

    public BootstrapController(
        IBootstrapService bootstrapService,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _bootstrapService = bootstrapService;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BootstrapStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var status = await _bootstrapService.GetStatusAsync(cancellationToken);
        return Ok(new BootstrapStatusResponse
        {
            Completed = status.Completed,
            AdministratorCreated = status.AdministratorCreated,
            ClubCreated = status.ClubCreated,
            ClubId = status.ClubId,
            CompletedAtUtc = status.CompletedAtUtc,
        });
    }

    [HttpPost("admin")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BootstrapStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdministrator([FromBody] CreateAdministratorRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var status = await _bootstrapService.CreateAdministratorAsync(
            new CreateAdministratorInput(
                request.UserName,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName),
            cancellationToken);

        await RecordAuditAsync(
            action: "Bootstrap.Admin.Create",
            entityType: "Bootstrap",
            reference: request.UserName,
            metadataJson: System.Text.Json.JsonSerializer.Serialize(new { status.Completed }),
            cancellationToken: cancellationToken);

        return Ok(ToResponse(status));
    }

    [HttpPost("club")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BootstrapStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateClub([FromBody] CreateClubRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var status = await _bootstrapService.CreateClubAsync(
            new CreateClubInput(request.Name, request.Code, request.CurrencyCode, request.Country),
            cancellationToken);

        await RecordAuditAsync(
            action: "Bootstrap.Club.Create",
            entityType: "Bootstrap",
            reference: request.Code,
            metadataJson: System.Text.Json.JsonSerializer.Serialize(new { ClubId = status.ClubId }),
            cancellationToken: cancellationToken);

        return Ok(ToResponse(status));
    }

    private static BootstrapStatusResponse ToResponse(BootstrapStatus status) => new()
    {
        Completed = status.Completed,
        AdministratorCreated = status.AdministratorCreated,
        ClubCreated = status.ClubCreated,
        ClubId = status.ClubId,
        CompletedAtUtc = status.CompletedAtUtc,
    };
}