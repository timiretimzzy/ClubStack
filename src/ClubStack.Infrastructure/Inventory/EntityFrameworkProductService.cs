using ClubStack.Application.Exceptions;
using ClubStack.Application.Inventory;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Infrastructure.Membership;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Inventory;

/// <inheritdoc />
public sealed class EntityFrameworkProductService : IProductService
{
    private const string StandardPriceType = "Standard";

    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkProductService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = InventoryServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<ProductEntity> query = _context.Products.AsNoTracking();

        var queryText = InventoryServiceHelpers.NormalizeOptional(input.Query);
        if (queryText is not null)
        {
            query = query.Where(p =>
                p.Sku.ToLower().Contains(queryText.ToLower())
                || p.Name.ToLower().Contains(queryText.ToLower())
                || (p.Barcode != null && p.Barcode.ToLower().Contains(queryText.ToLower())));
        }

        if (input.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (input.StockManagedOnly is { } stockManaged)
        {
            query = query.Where(p => p.StockManaged == stockManaged);
        }

        if (input.Status is { } statusText)
        {
            var status = InventoryServiceHelpers.ParseEnum<ProductStatus>(statusText, "status");
            query = query.Where(p => p.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.TaxRate)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ProductSearchResult(
            items.Select(InventoryMappings.ToProductDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToProductDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var sku = StockIdentifier.Normalize(input.Sku);
        if (!StockIdentifier.IsValidSku(sku))
        {
            throw new ValidationException(
                "The product could not be created.",
                InventoryServiceHelpers.FieldError("sku", $"{StockIdentifier.SkuMinLength} to {StockIdentifier.SkuMaxLength} letters, digits, dashes, dots or underscores."));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The product could not be created.",
                InventoryServiceHelpers.FieldError("name", "A product name is required."));
        }

        if (string.IsNullOrWhiteSpace(input.UnitOfMeasure))
        {
            throw new ValidationException(
                "The product could not be created.",
                InventoryServiceHelpers.FieldError("unitOfMeasure", "A unit of measure is required."));
        }

        if (input.StockManaged)
        {
            InventoryServiceHelpers.EnsureNonNegative(input.Cost, "cost");
        }
        else
        {
            input = input with { Cost = 0m };
        }

        if (input.ReorderLevel is { } reorderLevel)
        {
            InventoryServiceHelpers.EnsureNonNegative(reorderLevel, "reorderLevel");
        }

        if (await _context.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
        {
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
        }

        var category = await _context.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == input.CategoryId, cancellationToken)
            ?? throw new NotFoundException($"Product category '{input.CategoryId}' was not found.");

        if (!category.IsActive)
        {
            throw new ConflictException($"Product category '{category.Code}' is inactive.");
        }

        SupplierEntity? supplier = null;
        if (input.SupplierId is { } supplierId)
        {
            supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
                ?? throw new NotFoundException($"Supplier '{supplierId}' was not found.");
        }

        var (taxRate, taxTreatment) = await ResolveTaxAsync(input.TaxRateId, input.TaxTreatment, cancellationToken);

        var barcode = InventoryServiceHelpers.NormalizeOptional(input.Barcode);
        if (barcode is not null && await _context.Products.AnyAsync(p => p.Barcode == barcode, cancellationToken))
        {
            throw new ConflictException($"A product with barcode '{barcode}' already exists.");
        }

        var currencyCode = await InventoryServiceHelpers.ResolveCurrencyCodeAsync(_context, input.CurrencyCode, cancellationToken);

        var now = _clock.UtcNow;
        var product = new ProductEntity
        {
            Sku = sku,
            Name = input.Name.Trim(),
            CategoryId = category.Id,
            UnitOfMeasure = input.UnitOfMeasure.Trim(),
            Barcode = barcode,
            StockManaged = input.StockManaged,
            Cost = input.Cost,
            CurrencyCode = currencyCode,
            ReorderLevel = input.StockManaged ? input.ReorderLevel : null,
            SupplierId = supplier?.Id,
            TaxRateId = taxRate?.Id,
            TaxTreatment = taxTreatment,
            Status = ProductStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.Products.Add(product);

        // A product is catalogued for selling once it has a current price
        // (Phase 2 INV-008, Phase 3 §22 Operational Price). Setting a price on
        // creation is convenient but optional; SetPrice can add it later.
        if (input.PriceAmount is { } priceAmount && priceAmount > 0m)
        {
            _context.ProductPrices.Add(new ProductPriceEntity
            {
                ProductId = product.Id,
                PriceType = StandardPriceType,
                CurrencyCode = currencyCode,
                Amount = priceAmount,
                EffectiveFromUtc = now,
                CreatedAtUtc = now,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var created = await LoadProductAsync(product.Id, tracked: false, cancellationToken);
        return InventoryMappings.ToProductDto(created);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(id, tracked: true, cancellationToken);

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The product could not be updated.",
                InventoryServiceHelpers.FieldError("name", "A product name is required."));
        }

        if (string.IsNullOrWhiteSpace(input.UnitOfMeasure))
        {
            throw new ValidationException(
                "The product could not be updated.",
                InventoryServiceHelpers.FieldError("unitOfMeasure", "A unit of measure is required."));
        }

        if (input.StockManaged)
        {
            InventoryServiceHelpers.EnsureNonNegative(input.Cost, "cost");
        }
        else
        {
            // Switching to non-managed clears the cost and reorder level.
            input = input with { Cost = 0m, ReorderLevel = null };
        }

        if (input.ReorderLevel is { } reorderLevel)
        {
            InventoryServiceHelpers.EnsureNonNegative(reorderLevel, "reorderLevel");
        }

        var category = await _context.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == input.CategoryId, cancellationToken)
            ?? throw new NotFoundException($"Product category '{input.CategoryId}' was not found.");

        if (!category.IsActive)
        {
            throw new ConflictException($"Product category '{category.Code}' is inactive.");
        }

        if (input.SupplierId is { } supplierId)
        {
            var supplierExists = await _context.Suppliers.AnyAsync(s => s.Id == supplierId, cancellationToken);
            if (!supplierExists)
            {
                throw new NotFoundException($"Supplier '{supplierId}' was not found.");
            }
        }

        var (taxRate, taxTreatment) = await ResolveTaxAsync(input.TaxRateId, input.TaxTreatment, cancellationToken);

        var barcode = InventoryServiceHelpers.NormalizeOptional(input.Barcode);
        if (barcode is not null && await _context.Products.AnyAsync(p => p.Barcode == barcode && p.Id != id, cancellationToken))
        {
            throw new ConflictException($"A product with barcode '{barcode}' already exists.");
        }

        var now = _clock.UtcNow;
        product.Name = input.Name.Trim();
        product.CategoryId = category.Id;
        product.UnitOfMeasure = input.UnitOfMeasure.Trim();
        product.Barcode = barcode;
        product.StockManaged = input.StockManaged;
        product.Cost = input.Cost;
        product.ReorderLevel = input.StockManaged ? input.ReorderLevel : null;
        product.SupplierId = input.SupplierId;
        product.TaxRateId = taxRate?.Id;
        product.TaxTreatment = taxTreatment;
        product.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await LoadProductAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToProductDto(updated);
    }

    public async Task<ProductDto> SetStatusAsync(Guid id, bool active, string actorUserId, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(id, tracked: true, cancellationToken);
        var target = active ? ProductStatus.Active : ProductStatus.Inactive;
        if (product.Status == target)
        {
            return InventoryMappings.ToProductDto(product);
        }

        product.Status = target;
        product.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var updated = await LoadProductAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToProductDto(updated);
    }

    public async Task<IReadOnlyList<ProductPriceDto>> GetPricesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await LoadProductAsync(id, tracked: false, cancellationToken);

        var prices = await _context.ProductPrices
            .AsNoTracking()
            .Where(p => p.ProductId == id)
            .OrderByDescending(p => p.EffectiveFromUtc)
            .ThenByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return prices.Select(InventoryMappings.ToPriceDto).ToList();
    }

    public async Task<ProductPriceDto> SetPriceAsync(Guid id, SetProductPriceInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (input.Amount <= 0m)
        {
            throw new ValidationException(
                "The price could not be set.",
                InventoryServiceHelpers.FieldError("amount", "The price must be positive."));
        }

        var product = await LoadProductAsync(id, tracked: false, cancellationToken);
        var currencyCode = await InventoryServiceHelpers.ResolveCurrencyCodeAsync(_context, input.CurrencyCode, cancellationToken);

        var now = _clock.UtcNow;
        var price = new ProductPriceEntity
        {
            ProductId = product.Id,
            PriceType = StandardPriceType,
            CurrencyCode = currencyCode,
            Amount = input.Amount,
            EffectiveFromUtc = now,
            CreatedAtUtc = now,
        };

        _context.ProductPrices.Add(price);
        await _context.SaveChangesAsync(cancellationToken);

        return InventoryMappings.ToPriceDto(price);
    }

    private async Task<ProductEntity> LoadProductAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ProductEntity> query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.TaxRate);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Product '{id}' was not found.");
    }

    /// <summary>
    /// Resolves the selling tax configuration (D5 POS §7). A tax rate without an
    /// explicit treatment defaults to Inclusive; no tax rate behaves exempt. The
    /// treatment is validated against the <see cref="ProductTaxTreatment"/> values.
    /// </summary>
    private async Task<(TaxRateEntity? Rate, ProductTaxTreatment Treatment)> ResolveTaxAsync(
        Guid? taxRateId,
        string? taxTreatment,
        CancellationToken cancellationToken)
    {
        TaxRateEntity? rate = null;
        if (taxRateId is { } id)
        {
            rate = await _context.TaxRates
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                ?? throw new NotFoundException($"Tax rate '{id}' was not found.");
            if (!rate.IsActive)
            {
                throw new ConflictException($"Tax rate '{rate.Name}' is inactive.");
            }
        }

        ProductTaxTreatment treatment;
        if (string.IsNullOrWhiteSpace(taxTreatment))
        {
            treatment = ProductTaxTreatment.Inclusive;
        }
        else if (!Enum.TryParse<ProductTaxTreatment>(taxTreatment.Trim(), ignoreCase: true, out treatment))
        {
            throw new ValidationException(
                "The product tax configuration is invalid.",
                InventoryServiceHelpers.FieldError("taxTreatment", "The tax treatment must be Inclusive, Exclusive or ZeroRated."));
        }

        return (rate, treatment);
    }
}

/// Local mapping helpers used by the product service.
internal static class InventoryMappings
{
    public static ProductDto ToProductDto(ProductEntity p) => new(
        p.Id,
        p.Sku,
        p.Name,
        p.CategoryId,
        p.Category?.Name ?? string.Empty,
        p.UnitOfMeasure,
        p.Barcode,
        p.StockManaged,
        p.Cost,
        p.CurrencyCode,
        p.ReorderLevel,
        p.SupplierId,
        p.Supplier?.Name,
        p.Status.ToString(),
        p.CreatedAtUtc,
        p.UpdatedAtUtc,
        p.TaxTreatment.ToString(),
        p.TaxRateId,
        p.TaxRate?.Name,
        p.TaxRate?.Rate);

    public static ProductPriceDto ToPriceDto(ProductPriceEntity p) => new(
        p.Id,
        p.ProductId,
        p.PriceType,
        p.CurrencyCode,
        p.Amount,
        p.EffectiveFromUtc,
        p.CreatedAtUtc);

    public static ProductCategoryDto ToCategoryDto(ProductCategoryEntity c) => new(
        c.Id,
        c.Code,
        c.Name,
        c.Description,
        c.IsActive,
        c.CreatedAtUtc,
        c.UpdatedAtUtc);

    public static InventoryLocationDto ToLocationDto(InventoryLocationEntity l) => new(
        l.Id,
        l.Code,
        l.Name,
        l.Type.ToString(),
        l.Status.ToString(),
        l.CreatedAtUtc,
        l.UpdatedAtUtc);

    public static SupplierDto ToSupplierDto(SupplierEntity s) => new(
        s.Id,
        s.SupplierCode,
        s.Name,
        s.ContactPerson,
        s.Email,
        s.Phone,
        s.PaymentTerms,
        s.TaxId,
        s.Status.ToString(),
        s.CreatedAtUtc,
        s.UpdatedAtUtc);

    public static StockMovementDto ToMovementDto(StockMovementEntity m) => new(
        m.Id,
        m.Reference,
        m.ProductId,
        m.Product.Sku,
        m.Product.Name,
        m.LocationId,
        m.Location.Code,
        m.Quantity,
        m.MovementType.ToString(),
        m.UnitCost,
        m.CurrencyCode,
        m.BusinessDateUtc,
        m.SourceType,
        m.SourceId,
        m.Reason,
        m.UserId,
        m.CreatedAtUtc);

    public static StockTransferDto ToTransferDto(StockTransferEntity t) => new(
        t.Id,
        t.TransferNumber,
        t.FromLocationId,
        t.FromLocation.Code,
        t.ToLocationId,
        t.ToLocation.Code,
        t.Status.ToString(),
        t.CreatedByUserId,
        t.CompletedAtUtc,
        t.CancelledAtUtc,
        t.CreatedAtUtc,
        t.Lines.Select(l => new StockTransferLineDto(
            l.ProductId,
            l.Product.Sku,
            l.Product.Name,
            l.Quantity,
            l.UnitCost)).ToList());

    public static StockCountDto ToCountDto(StockCountEntity c, IReadOnlyList<StockCountLineEntity> lines) => new(
        c.Id,
        c.CountNumber,
        c.LocationId,
        c.Location.Code,
        c.Status.ToString(),
        c.CreatedByUserId,
        c.StartedAtUtc,
        c.CompletedAtUtc,
        c.ApprovedAtUtc,
        c.CreatedAtUtc,
        lines.Select(l => new StockCountLineDto(
            l.ProductId,
            l.Product.Sku,
            l.Product.Name,
            l.ExpectedQuantity,
            l.CountedQuantity,
            l.Variance)).ToList());

    public static StockAdjustmentDto ToAdjustmentDto(StockAdjustmentEntity a, IReadOnlyList<StockAdjustmentLineEntity> lines) => new(
        a.Id,
        a.AdjustmentNumber,
        a.AdjustmentType.ToString(),
        a.LocationId,
        a.Location.Code,
        a.Reason,
        a.BusinessDateUtc,
        a.CreatedByUserId,
        a.PostedAtUtc,
        a.CreatedAtUtc,
        lines.Select(l => new StockAdjustmentLineDto(
            l.ProductId,
            l.Product.Sku,
            l.Product.Name,
            l.Quantity,
            l.UnitCost)).ToList());
}