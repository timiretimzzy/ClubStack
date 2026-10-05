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
/// Product catalogue and selling prices (Phase 2 INV-001..010, Phase 3 §21-22,
/// Phase 8 §7.4). SKUs and barcodes are unique; prices are operational rows so a
/// price change never rewrites history (Phase 3 §22).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/products")]
[Authorize]
public sealed class ProductsController : AuditedControllerBase
{
    private readonly IProductService _products;

    public ProductsController(
        IProductService products,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _products = products;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductView)]
    [ProducesResponseType(typeof(ProductSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _products.SearchAsync(
            new ProductSearchInput(
                request.Query,
                request.CategoryId,
                request.StockManagedOnly,
                request.Status,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new ProductSearchResponse
        {
            Items = result.Items.Select(ToProductResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductView)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(id, cancellationToken);
        return Ok(ToProductResponse(product));
    }

    [HttpGet("{id:guid}/prices")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductView)]
    [ProducesResponseType(typeof(IReadOnlyList<ProductPriceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Prices(Guid id, CancellationToken cancellationToken)
    {
        var prices = await _products.GetPricesAsync(id, cancellationToken);
        return Ok(prices.Select(ToPriceResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductCreate)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var product = await _products.CreateAsync(
            new CreateProductInput(
                request.Sku,
                request.Name,
                request.CategoryId,
                request.UnitOfMeasure,
                request.Barcode,
                request.StockManaged,
                request.Cost,
                request.PriceAmount,
                request.ReorderLevel,
                request.SupplierId,
                request.CurrencyCode,
                request.TaxRateId,
                request.TaxTreatment),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Inventory.Product.Create",
            "Product",
            product.Id.ToString(),
            product.Sku,
            cancellationToken: cancellationToken);

        return Ok(ToProductResponse(product));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductEdit)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var product = await _products.UpdateAsync(
            id,
            new UpdateProductInput(
                request.Name,
                request.CategoryId,
                request.UnitOfMeasure,
                request.Barcode,
                request.StockManaged,
                request.Cost,
                request.ReorderLevel,
                request.SupplierId,
                request.TaxRateId,
                request.TaxTreatment),
            ActorUserId(),
            cancellationToken);

        return Ok(ToProductResponse(product));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductEdit)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetProductStatusRequest request, CancellationToken cancellationToken)
    {
        var product = await _products.SetStatusAsync(id, request.Active, ActorUserId(), cancellationToken);
        return Ok(ToProductResponse(product));
    }

    [HttpPost("{id:guid}/prices")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductEdit)]
    [ProducesResponseType(typeof(ProductPriceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPrice(Guid id, [FromBody] SetProductPriceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var price = await _products.SetPriceAsync(
            id,
            new SetProductPriceInput(request.Amount, request.CurrencyCode),
            ActorUserId(),
            cancellationToken);

        return Ok(ToPriceResponse(price));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static ProductResponse ToProductResponse(ProductDto p) => new()
    {
        Id = p.Id,
        Sku = p.Sku,
        Name = p.Name,
        CategoryId = p.CategoryId,
        CategoryName = p.CategoryName,
        UnitOfMeasure = p.UnitOfMeasure,
        Barcode = p.Barcode,
        StockManaged = p.StockManaged,
        Cost = p.Cost,
        CurrencyCode = p.CurrencyCode,
        ReorderLevel = p.ReorderLevel,
        SupplierId = p.SupplierId,
        SupplierName = p.SupplierName,
        Status = p.Status,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc,
        TaxTreatment = p.TaxTreatment,
        TaxRateId = p.TaxRateId,
        TaxRateName = p.TaxRateName,
        TaxRate = p.TaxRate,
    };

    internal static ProductPriceResponse ToPriceResponse(ProductPriceDto p) => new()
    {
        Id = p.Id,
        ProductId = p.ProductId,
        PriceType = p.PriceType,
        CurrencyCode = p.CurrencyCode,
        Amount = p.Amount,
        EffectiveFromUtc = p.EffectiveFromUtc,
        CreatedAtUtc = p.CreatedAtUtc,
    };
}