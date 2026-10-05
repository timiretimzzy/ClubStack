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
/// Stock transfers between locations (Phase 2 INV-025, Phase 3 §33). Draft is
/// editable; completing posts the TransferOut/TransferIn movement pair, cancelling
/// only works before completion. No InTransit state in D4.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/transfers")]
[Authorize]
public sealed class StockTransfersController : AuditedControllerBase
{
    private readonly IStockTransferService _transfers;

    public StockTransfersController(
        IStockTransferService transfers,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _transfers = transfers;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockTransfer)]
    [ProducesResponseType(typeof(StockTransferSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _transfers.SearchAsync(page, pageSize, cancellationToken);
        return Ok(new StockTransferSearchResponse
        {
            Items = result.Items.Select(ToTransferResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockTransfer)]
    [ProducesResponseType(typeof(StockTransferResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await _transfers.GetAsync(id, cancellationToken);
        return Ok(ToTransferResponse(transfer));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockTransfer)]
    [ProducesResponseType(typeof(StockTransferResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateStockTransferRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transfer = await _transfers.CreateAsync(
            new CreateStockTransferInput(
                request.FromLocationId,
                request.ToLocationId,
                request.Lines
                    .Select(l => new CreateStockTransferLineInput(l.ProductId, l.Quantity, l.UnitCost))
                    .ToList()),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Inventory.Stock.Transfer",
            "StockTransfer",
            transfer.Id.ToString(),
            transfer.TransferNumber,
            cancellationToken: cancellationToken);

        return Ok(ToTransferResponse(transfer));
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockTransfer)]
    [ProducesResponseType(typeof(StockTransferResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await _transfers.CompleteAsync(id, ActorUserId(), cancellationToken);
        return Ok(ToTransferResponse(transfer));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockTransfer)]
    [ProducesResponseType(typeof(StockTransferResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await _transfers.CancelAsync(id, ActorUserId(), cancellationToken);
        return Ok(ToTransferResponse(transfer));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static StockTransferResponse ToTransferResponse(StockTransferDto t) => new()
    {
        Id = t.Id,
        TransferNumber = t.TransferNumber,
        FromLocationId = t.FromLocationId,
        FromLocationCode = t.FromLocationCode,
        ToLocationId = t.ToLocationId,
        ToLocationCode = t.ToLocationCode,
        Status = t.Status,
        CreatedByUserId = t.CreatedByUserId,
        CompletedAtUtc = t.CompletedAtUtc,
        CancelledAtUtc = t.CancelledAtUtc,
        CreatedAtUtc = t.CreatedAtUtc,
        Lines = t.Lines.Select(l => new StockTransferLineResponse
        {
            ProductId = l.ProductId,
            ProductSku = l.ProductSku,
            ProductName = l.ProductName,
            Quantity = l.Quantity,
            UnitCost = l.UnitCost,
        }).ToList(),
    };
}