using ClubStack.Application.Exceptions;
using ClubStack.Application.Inventory;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Inventory;

/// <inheritdoc />
public sealed class EntityFrameworkStockAdjustmentService : IStockAdjustmentService
{
    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;
    private readonly StockLedgerWriter _ledger;

    public EntityFrameworkStockAdjustmentService(
        ClubStackDbContext context,
        ISystemClock clock,
        IReferenceGenerator references,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _referenceGenerator = references;
        _clock = clock;
        _ledger = new StockLedgerWriter(context, clock);
    }

    public async Task<StockAdjustmentSearchResult> SearchAsync(string? adjustmentType = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = InventoryServiceHelpers.NormalizePage(page, pageSize);

        IQueryable<StockAdjustmentEntity> query = _context.StockAdjustments.AsNoTracking();

        if (adjustmentType is { } typeText)
        {
            var type = InventoryServiceHelpers.ParseEnum<StockAdjustmentType>(typeText, "adjustmentType");
            query = query.Where(a => a.AdjustmentType == type);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(a => a.Location)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var dtos = new List<StockAdjustmentDto>();
        foreach (var item in items)
        {
            var lines = await LoadAdjustmentLinesAsync(item.Id, cancellationToken);
            dtos.Add(InventoryMappings.ToAdjustmentDto(item, lines));
        }

        return new StockAdjustmentSearchResult(dtos, total, safePage, safePageSize);
    }

    public async Task<StockAdjustmentDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var adjustment = await _context.StockAdjustments
            .AsNoTracking()
            .Include(a => a.Location)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Stock adjustment '{id}' was not found.");

        var lines = await LoadAdjustmentLinesAsync(id, cancellationToken);
        return InventoryMappings.ToAdjustmentDto(adjustment, lines);
    }

    public async Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var adjustmentType = InventoryServiceHelpers.ParseEnum<StockAdjustmentType>(input.AdjustmentType, "adjustmentType");

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The adjustment could not be recorded.",
                InventoryServiceHelpers.FieldError("reason", "A reason is required for a stock adjustment."));
        }

        if (input.Lines.Count == 0)
        {
            throw new ValidationException(
                "The adjustment could not be recorded.",
                InventoryServiceHelpers.FieldError("lines", "At least one product line is required."));
        }

        var location = await _context.InventoryLocations
                           .FirstOrDefaultAsync(l => l.Id == input.LocationId, cancellationToken)
                       ?? throw new NotFoundException($"Inventory location '{input.LocationId}' was not found.");
        InventoryServiceHelpers.EnsureLocationActive(location);

        if (input.Lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
        {
            throw new ValidationException(
                "The adjustment could not be recorded.",
                InventoryServiceHelpers.FieldError("lines", "A product may appear only once in an adjustment."));
        }

        var productIds = input.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in input.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new NotFoundException($"Product '{line.ProductId}' was not found.");
            }

            InventoryServiceHelpers.EnsureProductUsable(product);
            if (line.Quantity == 0m)
            {
                throw new ValidationException(
                    "The adjustment could not be recorded.",
                    InventoryServiceHelpers.FieldError("lines", "The adjustment quantity must not be zero."));
            }

            if (adjustmentType == StockAdjustmentType.Wastage && line.Quantity < 0m)
            {
                throw new ValidationException(
                    "The adjustment could not be recorded.",
                    InventoryServiceHelpers.FieldError("lines", "Wastage quantities must be positive."));
            }
        }

        var now = _clock.UtcNow;
        var businessDateUtc = input.BusinessDateUtc ?? now;

        var adjustment = new StockAdjustmentEntity
        {
            AdjustmentNumber = await _referenceGenerator.GenerateAsync(StockAdjustmentNumber.Prefix, StockAdjustmentNumber.Width, cancellationToken),
            AdjustmentType = adjustmentType,
            LocationId = location.Id,
            Reason = input.Reason.Trim(),
            BusinessDateUtc = businessDateUtc,
            CreatedByUserId = actorUserId,
            PostedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        foreach (var line in input.Lines)
        {
            var product = products[line.ProductId];
            adjustment.Lines.Add(new StockAdjustmentLineEntity
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost ?? product.Cost,
            });
        }

        var movementType = adjustmentType == StockAdjustmentType.Wastage ? StockMovementType.Wastage : StockMovementType.Adjustment;

        // References must be allocated before the transaction; the generator
        // uses the raw connection and cannot run inside one.
        var movementReferences = new List<string>(input.Lines.Count);
        for (var i = 0; i < input.Lines.Count; i++)
        {
            movementReferences.Add(await _referenceGenerator.GenerateAsync(StockMovementNumber.Prefix, StockMovementNumber.Width, cancellationToken));
        }

        // The header, lines and ledger movements are all persisted in the same
        // transaction so a failure leaves nothing behind.
        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            await StockLedgerWriter.AcquireLocksAsync(
                _context,
                input.Lines.Select(l => (l.ProductId, input.LocationId)),
                cancellationToken);

            _context.StockAdjustments.Add(adjustment);
            await _context.SaveChangesAsync(cancellationToken);

            for (var i = 0; i < input.Lines.Count; i++)
            {
                var line = input.Lines[i];
                var product = products[line.ProductId];
                await _ledger.ApplyAsync(
                    movementType,
                    line.ProductId,
                    input.LocationId,
                    line.Quantity,
                    line.UnitCost ?? product.Cost,
                    product.CurrencyCode,
                    businessDateUtc,
                    actorUserId,
                    input.Reason.Trim(),
                    "StockAdjustment",
                    adjustment.Id,
                    movementReferences[i],
                    cancellationToken);
            }

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var created = await GetAsync(adjustment.Id, cancellationToken);
        return created;
    }

    private Task<List<StockAdjustmentLineEntity>> LoadAdjustmentLinesAsync(Guid adjustmentId, CancellationToken cancellationToken)
        => _context.StockAdjustmentLines
            .AsNoTracking()
            .Include(l => l.Product)
            .Where(l => l.AdjustmentId == adjustmentId)
            .OrderBy(l => l.Product.Name)
            .ToListAsync(cancellationToken);
}