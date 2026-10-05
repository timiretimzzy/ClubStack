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

/// <summary>Payment method reference data (D1 Platform, Phase 8 §8.1 <c>payment_methods</c>).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/payment-methods")]
[Authorize]
public sealed class PaymentMethodsController : AuditedControllerBase
{
    private readonly IPlatformConfigurationStore _store;

    public PaymentMethodsController(
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
    [ProducesResponseType(typeof(IReadOnlyList<PaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _store.GetPaymentMethodsAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformConfigurationView)]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _store.GetPaymentMethodAsync(id, cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.PlatformPaymentMethodManage)]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.CreatePaymentMethodAsync(new PaymentMethodInput(request.Name, request.Code, request.Kind, request.IsActive), cancellationToken);
        await RecordAuditAsync("Platform.PaymentMethod.Create", "PaymentMethod", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.PlatformPaymentMethodManage)]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var row = await _store.UpdatePaymentMethodAsync(id, new PaymentMethodInput(request.Name, request.Code, request.Kind, request.IsActive), cancellationToken);
        await RecordAuditAsync("Platform.PaymentMethod.Update", "PaymentMethod", row.Id.ToString(), row.Code, cancellationToken: cancellationToken);
        return Ok(ToResponse(row));
    }

    private static PaymentMethodResponse ToResponse(PaymentMethodDto d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Code = d.Code,
        Kind = d.Kind,
        IsActive = d.IsActive,
        CreatedAtUtc = d.CreatedAtUtc,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };
}