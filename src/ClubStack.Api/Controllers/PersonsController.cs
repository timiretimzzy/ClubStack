using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Membership;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Person records — the identity side of the membership directory (Phase 8 §8.3
/// <c>persons</c>, Phase 2 MEM-001/MEM-003). Persons exist independently of a
/// membership; duplicate warnings are returned on likely matches.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/persons")]
[Authorize]
public sealed class PersonsController : AuditedControllerBase
{
    private readonly IMembershipDirectoryService _directory;

    public PersonsController(
        IMembershipDirectoryService directory,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _directory = directory;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(PersonResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var person = await _directory.GetPersonAsync(id, cancellationToken);
        return Ok(ToResponse(person));
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberView)]
    [ProducesResponseType(typeof(PersonSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _directory.SearchPersonsAsync(new PersonSearchInput(query, page, pageSize), cancellationToken);
        return Ok(new PersonSearchResponse
        {
            Items = result.Items.Select(ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.MembershipMemberCreate)]
    [ProducesResponseType(typeof(PersonCreatedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] PersonRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var input = new CreatePersonInput(
            request.LegalName,
            request.PreferredName,
            request.IdNumber,
            request.DateOfBirth,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Region,
            request.PostalCode,
            request.Country,
            request.Notes);

        var result = await _directory.CreatePersonAsync(input, cancellationToken);

        await RecordAuditAsync(
            "Membership.Person.Create",
            "Person",
            result.Person.Id.ToString(),
            result.Person.LegalName,
            cancellationToken: cancellationToken);

        return Ok(new PersonCreatedResponse
        {
            Person = ToResponse(result.Person),
            DuplicateWarnings = result.DuplicateWarnings.ToList(),
        });
    }

    internal static PersonResponse ToResponse(ClubStack.Application.Membership.PersonDto p) => new()
    {
        Id = p.Id,
        LegalName = p.LegalName,
        PreferredName = p.PreferredName,
        MemberId = p.MemberId,
        MemberNumber = p.MemberNumber,
        IdNumber = p.IdNumber,
        DateOfBirth = p.DateOfBirth,
        Email = p.Email,
        Phone = p.Phone,
        AddressLine1 = p.AddressLine1,
        AddressLine2 = p.AddressLine2,
        City = p.City,
        Region = p.Region,
        PostalCode = p.PostalCode,
        Country = p.Country,
        Notes = p.Notes,
        IsActive = p.IsActive,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc,
    };
}