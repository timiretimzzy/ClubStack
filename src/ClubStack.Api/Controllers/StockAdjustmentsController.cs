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
/// Stock adjustments and wastage (Phase 2 INV-026/INV-027, Phase 3 §34,
/// Phase 8 §8.6-8.8). Differing permissions separate the two; both post the
/// stock ledger immediately. Every adjustment is a sensitive action and is
/// audited here in the API layer (Phase 8 §5).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/adjustments")]
[Authorize]
public sealed class StockAdjustmentsController : AuditedControllerBase
{
    private readonly IStockAdjustmentService _adjustments;
    private readonly IAuthorizationService _authorization;

    public StockAdjustmentsController(
        IStockAdjustmentService adjustments,
        IAuthorizationService authorization,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _adjustments = adjustments;
        _authorization = authorization;
    }

    [HttpGet]
    [ProducesResponseType(typeof(StockAdjustmentSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? adjustmentType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (!await AuthorizeScopeAsync(adjustmentType))
        {
            return Forbid();
        }

        var result = await _adjustments.SearchAsync(adjustmentType, page, pageSize, cancellationToken);
        return Ok(new StockAdjustmentSearchResponse
        {
            Items = result.Items.Select(ToAdjustmentResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StockAdjustmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var adjustment = await _adjustments.GetAsync(id, cancellationToken);
        if (!await AuthorizeScopeAsync(adjustment.AdjustmentType))
        {
            return Forbid();
        }

        return Ok(ToAdjustmentResponse(adjustment));
    }

    [HttpPost]
    [ProducesResponseType(typeof(StockAdjustmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!await AuthorizeScopeAsync(request.AdjustmentType))
        {
            return Forbid();
        }

        var adjustment = await _adjustments.CreateAsync(
            new CreateStockAdjustmentInput(
                request.AdjustmentType,
                request.LocationId,
                request.Reason,
                request.Lines
                    .Select(l => new CreateStockAdjustmentLineInput(l.ProductId, l.Quantity, l.UnitCost))
                    .ToList(),
                request.BusinessDateUtc),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            adjustment.AdjustmentType == "Wastage"
                ? "Inventory.Stock.Wastage"
                : "Inventory.Stock.Adjust",
            "StockAdjustment",
            adjustment.Id.ToString(),
            adjustment.AdjustmentNumber,
            cancellationToken: cancellationToken);

        return Ok(ToAdjustmentResponse(adjustment));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// Type-scoped permission gate: Wastage requests need <c>Inventory.Stock.Wastage</c>,
    /// Adjustment requests need <c>Inventory.Stock.Adjust</c>. An unfiltered request spans
    /// both documents and therefore requires both permissions.
    /// </summary>
    private async Task<bool> AuthorizeScopeAsync(string? adjustmentType)
    {
        if (string.IsNullOrWhiteSpace(adjustmentType))
        {
            return await IsAuthorizedAsync(Permissions.InventoryStockAdjust)
                && await IsAuthorizedAsync(Permissions.InventoryStockWastage);
        }

        return string.Equals(adjustmentType.Trim(), "Wastage", StringComparison.OrdinalIgnoreCase)
            ? await IsAuthorizedAsync(Permissions.InventoryStockWastage)
            : await IsAuthorizedAsync(Permissions.InventoryStockAdjust);
    }

    private async Task<bool> IsAuthorizedAsync(string permission)
        => (await _authorization.AuthorizeAsync(User, $"Permission:{permission}")).Succeeded;

    internal static StockAdjustmentResponse ToAdjustmentResponse(StockAdjustmentDto a) => new()
    {
        Id = a.Id,
        AdjustmentNumber = a.AdjustmentNumber,
        AdjustmentType = a.AdjustmentType,
        LocationId = a.LocationId,
        LocationCode = a.LocationCode,
        Reason = a.Reason,
        BusinessDateUtc = a.BusinessDateUtc,
        CreatedByUserId = a.CreatedByUserId,
        PostedAtUtc = a.PostedAtUtc,
        CreatedAtUtc = a.CreatedAtUtc,
        Lines = a.Lines.Select(l => new StockAdjustmentLineResponse
        {
            ProductId = l.ProductId,
            ProductSku = l.ProductSku,
            ProductName = l.ProductName,
            Quantity = l.Quantity,
            UnitCost = l.UnitCost,
        }).ToList(),
    };
}