using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Platform;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>Numbering configuration (D1 Platform, Phase 8 §8.1 <c>numbering_sequences</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/numbering-sequences")]
[Authorize]
public sealed class NumberingSequencesController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public NumberingSequencesController(
        IPlatformConfigurationStore store,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(IReadOnlyList<NumberingSequenceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetNumberingSequencesAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(NumberingSequenceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetNumberingSequenceAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformNumberingManage)]
    [ProducesResponseType(typeof(NumberingSequenceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] NumberingSequenceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.CreateNumberingSequenceAsync(
            new NumberingSequenceInput(request.Prefix, request.FormatWidth, request.LastNumber), cancellationToken);
        await RecordAuditAsync("Platform.Numbering.Create", "NumberingSequence", row.Id.ToString(), row.Prefix, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformNumberingManage)]
    [ProducesResponseType(typeof(NumberingSequenceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] NumberingSequenceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdateNumberingSequenceAsync(
            id, new NumberingSequenceInput(request.Prefix, request.FormatWidth, request.LastNumber), cancellationToken);
        await RecordAuditAsync("Platform.Numbering.Update", "NumberingSequence", row.Id.ToString(), row.Prefix, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static NumberingSequenceResponse ToResponse(NumberingSequenceDto d) => new()
    {
        Id = d.Id,
        Prefix = d.Prefix,
        FormatWidth = d.FormatWidth,
        LastNumber = d.LastNumber,
        Example = d.Example,
    };
}