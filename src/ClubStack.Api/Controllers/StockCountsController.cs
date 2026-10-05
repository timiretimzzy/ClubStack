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
/// Stock counts (Phase 2 INV-041..045, Phase 3 §34). Initiate snapshots expected
/// balances, record captures what was physically counted, complete freezes the
/// sheet and approve postings the variance adjustment. InProgress and Completed
/// sheets may be cancelled before approval.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/counts")]
[Authorize]
public sealed class StockCountsController : AuditedControllerBase
{
    private readonly IStockCountService _counts;

    public StockCountsController(
        IStockCountService counts,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _counts = counts;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockCount)]
    [ProducesResponseType(typeof(StockCountSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _counts.SearchAsync(page, pageSize, cancellationToken);
        return Ok(new StockCountSearchResponse
        {
            Items = result.Items.Select(ToCountResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockCount)]
    [ProducesResponseType(typeof(StockCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var count = await _counts.GetAsync(id, cancellationToken);
        return Ok(ToCountResponse(count));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockCount)]
    [ProducesResponseType(typeof(StockCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Initiate([FromBody] InitiateStockCountRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var count = await _counts.InitiateAsync(
            new InitiateStockCountInput(request.LocationId, request.ProductId),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Inventory.Stock.Count",
            "StockCount",
            count.Id.ToString(),
            count.CountNumber,
            cancellationToken: cancellationToken);

        return Ok(ToCountResponse(count));
    }

    [HttpPut("{id:guid}/lines")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockCount)]
    [ProducesResponseType(typeof(StockCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordLines(Guid id, [FromBody] RecordStockCountLinesRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var count = await _counts.RecordLinesAsync(
            id,
            new RecordStockCountLinesInput(
                request.Items.Select(i => new RecordStockCountLineInput(i.ProductId, i.CountedQuantity)).ToList()),
            ActorUserId(),
            cancellationToken);

        return Ok(ToCountResponse(count));
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockCount)]
    [ProducesResponseType(typeof(StockCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var count = await _counts.CompleteAsync(id, ActorUserId(), cancellationToken);
        return Ok(ToCountResponse(count));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockApproveAdjustment)]
    [ProducesResponseType(typeof(StockCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var count = await _counts.ApproveAsync(id, ActorUserId(), cancellationToken);
        return Ok(ToCountResponse(count));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryStockCount)]
    [ProducesResponseType(typeof(StockCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var count = await _counts.CancelAsync(id, ActorUserId(), cancellationToken);
        return Ok(ToCountResponse(count));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static StockCountResponse ToCountResponse(StockCountDto c) => new()
    {
        Id = c.Id,
        CountNumber = c.CountNumber,
        LocationId = c.LocationId,
        LocationCode = c.LocationCode,
        Status = c.Status,
        CreatedByUserId = c.CreatedByUserId,
        StartedAtUtc = c.StartedAtUtc,
        CompletedAtUtc = c.CompletedAtUtc,
        ApprovedAtUtc = c.ApprovedAtUtc,
        CreatedAtUtc = c.CreatedAtUtc,
        Lines = c.Lines.Select(l => new StockCountLineResponse
        {
            ProductId = l.ProductId,
            ProductSku = l.ProductSku,
            ProductName = l.ProductName,
            ExpectedQuantity = l.ExpectedQuantity,
            CountedQuantity = l.CountedQuantity,
            Variance = l.Variance,
        }).ToList(),
    };
}