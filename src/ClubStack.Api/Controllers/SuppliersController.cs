using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Inventory;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Supplier master data (Phase 2 PROC-001, Phase 3 §35). Only the shared supplier
/// foundation is implemented in D4; requisitions, purchase orders, goods receipts
/// and invoices belong to the procurement slice.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/suppliers")]
[Authorize]
public sealed class SuppliersController : AuditedControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(
        ISupplierService suppliers,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _suppliers = suppliers;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.ProcurementSupplierView)]
    [ProducesResponseType(typeof(SupplierSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _suppliers.SearchAsync(query, page, pageSize, cancellationToken);
        return Ok(new SupplierSearchResponse
        {
            Items = result.Items.Select(ToSupplierResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.ProcurementSupplierView)]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetAsync(id, cancellationToken);
        return Ok(ToSupplierResponse(supplier));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.ProcurementSupplierManage)]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var supplier = await _suppliers.CreateAsync(
            new CreateSupplierInput(
                request.SupplierCode,
                request.Name,
                request.ContactPerson,
                request.Email,
                request.Phone,
                request.PaymentTerms,
                request.TaxId),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Procurement.Supplier.Create",
            "Supplier",
            supplier.Id.ToString(),
            supplier.SupplierCode,
            cancellationToken: cancellationToken);

        return Ok(ToSupplierResponse(supplier));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.ProcurementSupplierManage)]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var supplier = await _suppliers.UpdateAsync(
            id,
            new UpdateSupplierInput(
                request.Name,
                request.ContactPerson,
                request.Email,
                request.Phone,
                request.PaymentTerms,
                request.TaxId,
                request.IsActive),
            ActorUserId(),
            cancellationToken);

        return Ok(ToSupplierResponse(supplier));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static SupplierResponse ToSupplierResponse(SupplierDto s) => new()
    {
        Id = s.Id,
        SupplierCode = s.SupplierCode,
        Name = s.Name,
        ContactPerson = s.ContactPerson,
        Email = s.Email,
        Phone = s.Phone,
        PaymentTerms = s.PaymentTerms,
        TaxId = s.TaxId,
        Status = s.Status,
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
    };
}