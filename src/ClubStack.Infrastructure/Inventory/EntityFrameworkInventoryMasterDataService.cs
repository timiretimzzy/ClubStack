using ClubStack.Application.Exceptions;
using ClubStack.Application.Inventory;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Inventory;

/// <inheritdoc />
public sealed class EntityFrameworkProductCategoryService : IProductCategoryService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkProductCategoryService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        IQueryable<ProductCategoryEntity> query = _context.ProductCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return categories.Select(InventoryMappings.ToCategoryDto).ToList();
    }

    public async Task<ProductCategoryDto> CreateAsync(CreateCategoryInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var code = InventoryServiceHelpers.NormalizeOptional(input.Code)?.ToUpperInvariant()
                   ?? throw new ValidationException(
                       "The category could not be created.",
                       InventoryServiceHelpers.FieldError("code", "A category code is required."));

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The category could not be created.",
                InventoryServiceHelpers.FieldError("name", "A category name is required."));
        }

        if (await _context.ProductCategories.AnyAsync(c => c.Code == code, cancellationToken))
        {
            throw new ConflictException($"A product category with code '{code}' already exists.");
        }

        var now = _clock.UtcNow;
        var category = new ProductCategoryEntity
        {
            Code = code,
            Name = input.Name.Trim(),
            Description = InventoryServiceHelpers.NormalizeOptional(input.Description),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);
        return InventoryMappings.ToCategoryDto(category);
    }

    public async Task<ProductCategoryDto> UpdateAsync(Guid id, UpdateCategoryInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var category = await _context.ProductCategories
                            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                        ?? throw new NotFoundException($"Product category '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The category could not be updated.",
                InventoryServiceHelpers.FieldError("name", "A category name is required."));
        }

        category.Name = input.Name.Trim();
        category.Description = InventoryServiceHelpers.NormalizeOptional(input.Description);
        category.IsActive = input.IsActive;
        category.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return InventoryMappings.ToCategoryDto(category);
    }
}

/// <inheritdoc />
public sealed class EntityFrameworkInventoryLocationService : IInventoryLocationService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkInventoryLocationService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<InventoryLocationDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        IQueryable<InventoryLocationEntity> query = _context.InventoryLocations.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(l => l.Status == StockLocationStatus.Active);
        }

        var locations = await query.OrderBy(l => l.Name).ToListAsync(cancellationToken);
        return locations.Select(InventoryMappings.ToLocationDto).ToList();
    }

    public async Task<InventoryLocationDto> CreateAsync(CreateInventoryLocationInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var code = InventoryServiceHelpers.NormalizeOptional(input.Code)?.ToUpperInvariant()
                   ?? throw new ValidationException(
                       "The location could not be created.",
                       InventoryServiceHelpers.FieldError("code", "A location code is required."));

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The location could not be created.",
                InventoryServiceHelpers.FieldError("name", "A location name is required."));
        }

        var type = InventoryServiceHelpers.ParseEnum<StockLocationType>(input.Type, "type");

        if (await _context.InventoryLocations.AnyAsync(l => l.Code == code, cancellationToken))
        {
            throw new ConflictException($"An inventory location with code '{code}' already exists.");
        }

        var now = _clock.UtcNow;
        var location = new InventoryLocationEntity
        {
            Code = code,
            Name = input.Name.Trim(),
            Type = type,
            Status = StockLocationStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);
        return InventoryMappings.ToLocationDto(location);
    }

    public async Task<InventoryLocationDto> UpdateAsync(Guid id, UpdateInventoryLocationInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var location = await _context.InventoryLocations
                           .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                       ?? throw new NotFoundException($"Inventory location '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The location could not be updated.",
                InventoryServiceHelpers.FieldError("name", "A location name is required."));
        }

        var type = InventoryServiceHelpers.ParseEnum<StockLocationType>(input.Type, "type");

        location.Name = input.Name.Trim();
        location.Type = type;
        location.Status = input.IsActive ? StockLocationStatus.Active : StockLocationStatus.Inactive;
        location.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return InventoryMappings.ToLocationDto(location);
    }
}

/// <inheritdoc />
public sealed class EntityFrameworkSupplierService : ISupplierService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkSupplierService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers.AsNoTracking()
                           .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                       ?? throw new NotFoundException($"Supplier '{id}' was not found.");
        return InventoryMappings.ToSupplierDto(supplier);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var supplierCode = InventoryServiceHelpers.NormalizeOptional(input.SupplierCode)?.ToUpperInvariant()
                           ?? throw new ValidationException(
                               "The supplier could not be created.",
                               InventoryServiceHelpers.FieldError("supplierCode", "A supplier code is required."));

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The supplier could not be created.",
                InventoryServiceHelpers.FieldError("name", "A supplier name is required."));
        }

        if (await _context.Suppliers.AnyAsync(s => s.SupplierCode == supplierCode, cancellationToken))
        {
            throw new ConflictException($"A supplier with code '{supplierCode}' already exists.");
        }

        var now = _clock.UtcNow;
        var supplier = new SupplierEntity
        {
            SupplierCode = supplierCode,
            Name = input.Name.Trim(),
            ContactPerson = InventoryServiceHelpers.NormalizeOptional(input.ContactPerson),
            Email = InventoryServiceHelpers.NormalizeOptional(input.Email),
            Phone = InventoryServiceHelpers.NormalizeOptional(input.Phone),
            PaymentTerms = InventoryServiceHelpers.NormalizeOptional(input.PaymentTerms),
            TaxId = InventoryServiceHelpers.NormalizeOptional(input.TaxId),
            Status = SupplierStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);
        return InventoryMappings.ToSupplierDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
                           .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                       ?? throw new NotFoundException($"Supplier '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The supplier could not be updated.",
                InventoryServiceHelpers.FieldError("name", "A supplier name is required."));
        }

        supplier.Name = input.Name.Trim();
        supplier.ContactPerson = InventoryServiceHelpers.NormalizeOptional(input.ContactPerson);
        supplier.Email = InventoryServiceHelpers.NormalizeOptional(input.Email);
        supplier.Phone = InventoryServiceHelpers.NormalizeOptional(input.Phone);
        supplier.PaymentTerms = InventoryServiceHelpers.NormalizeOptional(input.PaymentTerms);
        supplier.TaxId = InventoryServiceHelpers.NormalizeOptional(input.TaxId);
        supplier.Status = input.IsActive ? SupplierStatus.Active : SupplierStatus.Inactive;
        supplier.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return InventoryMappings.ToSupplierDto(supplier);
    }

    public async Task<SupplierSearchResult> SearchAsync(string? query = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = InventoryServiceHelpers.NormalizePage(page, pageSize);

        IQueryable<SupplierEntity> suppliers = _context.Suppliers.AsNoTracking();
        var queryText = InventoryServiceHelpers.NormalizeOptional(query);
        if (queryText is not null)
        {
            suppliers = suppliers.Where(s =>
                s.SupplierCode.ToLower().Contains(queryText.ToLower())
                || s.Name.ToLower().Contains(queryText.ToLower()));
        }

        var total = await suppliers.CountAsync(cancellationToken);
        var items = await suppliers
            .OrderBy(s => s.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new SupplierSearchResult(items.Select(InventoryMappings.ToSupplierDto).ToList(), total, safePage, safePageSize);
    }
}