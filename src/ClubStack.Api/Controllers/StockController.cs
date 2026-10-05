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
/// Stock positions, movement history and goods receipts (Phase 2 INV-021..024,
/// Phase 3 §31-32). Balances are cached; the movement ledger is authoritative and
/// never mutated after posting.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/stock")]
[Authorize]
public sealed class StockController : AuditedControllerBase
{
    private readonly IStockLedgerService _ledger;

    public StockController(
        IStockLedgerService ledger,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _ledger = ledger;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockView)]
    [ProducesResponseType(typeof(StockSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? locationId,
        [FromQuery] bool reorderOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _ledger.GetStockAsync(
            new StockSearchInput(productId, locationId, reorderOnly, page, pageSize),
            cancellationToken);

        return Ok(new StockSearchResponse
        {
            Items = result.Items.Select(ToBalanceResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("movements")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockView)]
    [ProducesResponseType(typeof(MovementSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMovements(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? locationId,
        [FromQuery] string? movementType,
        [FromQuery] DateTimeOffset? fromDateUtc,
        [FromQuery] DateTimeOffset? toDateUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _ledger.SearchMovementsAsync(
            new MovementSearchInput(
                productId,
                locationId,
                movementType,
                null,
                fromDateUtc,
                toDateUtc,
                page,
                pageSize),
            cancellationToken);

        return Ok(new MovementSearchResponse
        {
            Items = result.Items.Select(ToMovementResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpPost("receive")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockReceive)]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive([FromBody] ReceiveStockRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var movements = await _ledger.ReceiveAsync(
            new ReceiveStockInput(
                request.LocationId,
                request.Lines
                    .Select(l => new ReceiveStockLineInput(l.ProductId, l.Quantity, l.UnitCost))
                    .ToList(),
                request.BusinessDateUtc,
                request.Reference),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Inventory.Stock.Receive",
            "StockReceipt",
            string.Join("|", movements.Select(m => m.Id)),
            string.Join("|", movements.Select(m => m.Reference)),
            cancellationToken: cancellationToken);

        return Ok(movements.Select(ToMovementResponse).ToList());
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static StockBalanceResponse ToBalanceResponse(StockBalanceDto b) => new()
    {
        ProductId = b.ProductId,
        ProductSku = b.ProductSku,
        ProductName = b.ProductName,
        LocationId = b.LocationId,
        LocationCode = b.LocationCode,
        LocationName = b.LocationName,
        OnHand = b.OnHand,
        Reserved = b.Reserved,
        Available = b.Available,
        ReorderLevel = b.ReorderLevel,
        Status = b.Status,
        UpdatedAtUtc = b.UpdatedAtUtc,
    };

    private static StockMovementResponse ToMovementResponse(StockMovementDto m) => new()
    {
        Id = m.Id,
        Reference = m.Reference,
        ProductId = m.ProductId,
        ProductSku = m.ProductSku,
        ProductName = m.ProductName,
        LocationId = m.LocationId,
        LocationCode = m.LocationCode,
        Quantity = m.Quantity,
        MovementType = m.MovementType,
        UnitCost = m.UnitCost,
        CurrencyCode = m.CurrencyCode,
        BusinessDateUtc = m.BusinessDateUtc,
        SourceType = m.SourceType,
        SourceId = m.SourceId,
        Reason = m.Reason,
        UserId = m.UserId,
        CreatedAtUtc = m.CreatedAtUtc,
    };
}