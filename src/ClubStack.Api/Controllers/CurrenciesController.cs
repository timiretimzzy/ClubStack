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

/// <summary>Currency reference data (D1 Platform, Phase 8 §8.1 <c>currencies</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/currencies")]
[Authorize]
public sealed class CurrenciesController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public CurrenciesController(
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
    [ProducesResponseType(typeof(IReadOnlyList<CurrencyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetCurrenciesAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetCurrencyAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformCurrencyManage)]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CurrencyRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.CreateCurrencyAsync(new CurrencyInput(
            request.Code, request.Name, request.Symbol, request.DecimalPlaces, request.IsDefault, request.IsActive), cancellationToken);
        await RecordAuditAsync("Platform.Currency.Create", "Currency", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformCurrencyManage)]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CurrencyRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdateCurrencyAsync(id, new CurrencyInput(
            request.Code, request.Name, request.Symbol, request.DecimalPlaces, request.IsDefault, request.IsActive), cancellationToken);
        await RecordAuditAsync("Platform.Currency.Update", "Currency", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static CurrencyResponse ToResponse(CurrencyDto d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        Symbol = d.Symbol,
        DecimalPlaces = d.DecimalPlaces,
        IsDefault = d.IsDefault,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}