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

/// <summary>Tax rate reference data (D1 Platform, Phase 8 §8.1 <c>tax_rates</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/tax-rates")]
[Authorize]
public sealed class TaxRatesController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public TaxRatesController(
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
    [ProducesResponseType(typeof(IReadOnlyList<TaxRateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetTaxRatesAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(TaxRateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetTaxRateAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformTaxManage)]
    [ProducesResponseType(typeof(TaxRateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] TaxRateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.CreateTaxRateAsync(new TaxRateInput(request.Name, request.Rate, request.IsActive), cancellationToken);
        await RecordAuditAsync("Platform.Tax.Create", "TaxRate", row.Id.ToString(), row.Name, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformTaxManage)]
    [ProducesResponseType(typeof(TaxRateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TaxRateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdateTaxRateAsync(id, new TaxRateInput(request.Name, request.Rate, request.IsActive), cancellationToken);
        await RecordAuditAsync("Platform.Tax.Update", "TaxRate", row.Id.ToString(), row.Name, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static TaxRateResponse ToResponse(TaxRateDto d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Rate = d.Rate,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}