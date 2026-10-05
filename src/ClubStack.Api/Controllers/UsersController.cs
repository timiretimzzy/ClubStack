using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Identity;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>User administration (D1 Identity, Phase 8 §8.2 <c>users</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/users")]
[Authorize]
public sealed class UsersController : AuditedControllerBase
{
    private readonly IIdentityAdministrationService _service;

    public UsersController(
        IIdentityAdministrationService service,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserView)]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await _service.GetUsersAsync(cancellationToken);
        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserView)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await _service.GetUserAsync(id, cancellationToken);
        return Ok(ToResponse(user));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserCreate)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _service.CreateUserAsync(new CreateUserInput(
            request.UserName,
            request.Email,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.Password,
            request.Roles), cancellationToken);

        await RecordAuditAsync("Identity.User.Create", "User", user.Id.ToString(), user.UserName, cancellationToken: cancellationToken);
        return Ok(ToResponse(user));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserEdit)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _service.UpdateUserAsync(id, new UpdateUserInput(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.IsActive,
            request.Roles), cancellationToken);

        await RecordAuditAsync("Identity.User.Update", "User", user.Id.ToString(), user.UserName, cancellationToken: cancellationToken);
        return Ok(ToResponse(user));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserDeactivate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Deactivated by administrator." : request.Reason;
        await _service.DeactivateUserAsync(id, reason, cancellationToken);

        await RecordAuditAsync("Identity.User.Deactivate", "User", id.ToString(), reason, cancellationToken: cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "Permission:" + Permissions.IdentityUserResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _service.ResetPasswordAsync(id, request.NewPassword, cancellationToken);

        await RecordAuditAsync("Identity.User.ResetPassword", "User", id.ToString(), cancellationToken: cancellationToken);
        return NoContent();
    }

    private static UserResponse ToResponse(UserDto d) => new()
    {
        Id = d.Id,
        UserName = d.UserName,
        FirstName = d.FirstName,
        LastName = d.LastName,
        Email = d.Email,
        PhoneNumber = d.PhoneNumber,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        LastLoginAtUtc = d.LastLoginAtUtc,
        Roles = d.Roles,
    };
}