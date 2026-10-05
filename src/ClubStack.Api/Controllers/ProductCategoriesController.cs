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
/// Product categories (Phase 2 INV-004). Phase 8 §4.2 defines no category
/// permission, so categories are managed under the product permissions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/product-categories")]
[Authorize]
public sealed class ProductCategoriesController : AuditedControllerBase
{
    private readonly IProductCategoryService _categories;

    public ProductCategoriesController(
        IProductCategoryService categories,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _categories = categories;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductView)]
    [ProducesResponseType(typeof(IReadOnlyList<ProductCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(includeInactive, cancellationToken);
        return Ok(categories.Select(ToCategoryResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductCreate)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateProductCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var category = await _categories.CreateAsync(
            new CreateCategoryInput(request.Code, request.Name, request.Description),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Inventory.ProductCategory.Create",
            "ProductCategory",
            category.Id.ToString(),
            category.Code,
            cancellationToken: cancellationToken);

        return Ok(ToCategoryResponse(category));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.InventoryProductEdit)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var category = await _categories.UpdateAsync(
            id,
            new UpdateCategoryInput(request.Name, request.Description, request.IsActive),
            ActorUserId(),
            cancellationToken);

        return Ok(ToCategoryResponse(category));
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static ProductCategoryResponse ToCategoryResponse(ProductCategoryDto c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        IsActive = c.IsActive,
        CreatedAtUtc = c.CreatedAtUtc,
        UpdatedAtUtc = c.UpdatedAtUtc,
    };
}