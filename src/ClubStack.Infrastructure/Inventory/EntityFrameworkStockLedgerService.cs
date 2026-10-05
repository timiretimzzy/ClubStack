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
public sealed class EntityFrameworkStockLedgerService : IStockLedgerService
{
    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceGenerator _references;
    private readonly StockLedgerWriter _ledger;

    public EntityFrameworkStockLedgerService(
        ClubStackDbContext context,
        ISystemClock clock,
        IReferenceGenerator references,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _references = references;
        _ledger = new StockLedgerWriter(context, clock);
    }

    public async Task<StockSearchResult> GetStockAsync(StockSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = InventoryServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<StockBalanceEntity> query = _context.StockBalances
            .AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Location);

        if (input.ProductId is { } productId)
        {
            query = query.Where(b => b.ProductId == productId);
        }

        if (input.LocationId is { } locationId)
        {
            query = query.Where(b => b.LocationId == locationId);
        }

        var rows = await query
            .OrderBy(b => b.Product.Name)
            .ThenBy(b => b.Location.Name)
            .ToListAsync(cancellationToken);

        // Reorder alerts are computed on the product's total on-hand across all
        // locations (Phase 2 INV-030 re-order level at product level in D4).
        var totalsByProduct = await _context.StockBalances
            .AsNoTracking()
            .Where(b => b.Product.StockManaged && b.Product.ReorderLevel != null)
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(b => b.Quantity) })
            .ToListAsync(cancellationToken);

        var items = new List<StockBalanceDto>();
        foreach (var row in rows)
        {
            var totalOnHand = totalsByProduct.FirstOrDefault(t => t.ProductId == row.ProductId)?.Total ?? row.Quantity;
            var reorderLevel = row.Product.ReorderLevel;
            var status = reorderLevel is { } level && totalOnHand <= level ? "Low" : "Ok";

            if (input.BelowReorderOnly is { } lowOnly && lowOnly && status != "Low")
            {
                continue;
            }

            items.Add(new StockBalanceDto(
                row.ProductId,
                row.Product.Sku,
                row.Product.Name,
                row.LocationId,
                row.Location.Code,
                row.Location.Name,
                row.Quantity,
                0m,
                row.Quantity,
                reorderLevel,
                status,
                row.UpdatedAtUtc));
        }

        var totalCount = items.Count;
        return new StockSearchResult(
            items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(Guid productId, Guid? locationId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        IQueryable<StockMovementEntity> query = _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Location)
            .Where(m => m.ProductId == productId);

        if (locationId is { } location)
        {
            query = query.Where(m => m.LocationId == location);
        }

        var movements = await query
            .OrderByDescending(m => m.BusinessDateUtc)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return movements.Select(InventoryMappings.ToMovementDto).ToList();
    }

    public async Task<MovementSearchResult> SearchMovementsAsync(MovementSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = InventoryServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<StockMovementEntity> query = _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Location);

        if (input.ProductId is { } productId)
        {
            query = query.Where(m => m.ProductId == productId);
        }

        if (input.LocationId is { } locationId)
        {
            query = query.Where(m => m.LocationId == locationId);
        }

        if (input.MovementType is { } typeText)
        {
            var type = InventoryServiceHelpers.ParseEnum<StockMovementType>(typeText, "movementType");
            query = query.Where(m => m.MovementType == type);
        }

        var queryText = InventoryServiceHelpers.NormalizeOptional(input.Query);
        if (queryText is not null)
        {
            query = query.Where(m =>
                m.Reference.ToLower().Contains(queryText.ToLower())
                || m.Product.Sku.ToLower().Contains(queryText.ToLower())
                || m.Product.Name.ToLower().Contains(queryText.ToLower()));
        }

        if (input.FromDateUtc is { } from)
        {
            query = query.Where(m => m.BusinessDateUtc >= from);
        }

        if (input.ToDateUtc is { } to)
        {
            query = query.Where(m => m.BusinessDateUtc <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.BusinessDateUtc)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MovementSearchResult(
            items.Select(InventoryMappings.ToMovementDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<IReadOnlyList<StockMovementDto>> ReceiveAsync(ReceiveStockInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (input.Lines.Count == 0)
        {
            throw new ValidationException(
                "The stock receipt could not be recorded.",
                InventoryServiceHelpers.FieldError("lines", "At least one product line is required."));
        }

        var location = await _context.InventoryLocations
                            .FirstOrDefaultAsync(l => l.Id == input.LocationId, cancellationToken)
                        ?? throw new NotFoundException($"Inventory location '{input.LocationId}' was not found.");
        InventoryServiceHelpers.EnsureLocationActive(location);

        var duplicateProduct = input.Lines.GroupBy(l => l.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicateProduct is not null)
        {
            throw new ValidationException(
                "The stock receipt could not be recorded.",
                InventoryServiceHelpers.FieldError("lines", "A product may appear only once in a receipt."));
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
            InventoryServiceHelpers.EnsurePositive(line.Quantity, "lines");
        }

        var businessDateUtc = input.BusinessDateUtc ?? _clock.UtcNow;
        var externalReference = InventoryServiceHelpers.NormalizeOptional(input.Reference);

        // Movement references are allocated before the transaction starts; the
        // reference generator uses the raw connection and cannot run inside one.
        var movementReferences = new List<string>(input.Lines.Count);
        for (var i = 0; i < input.Lines.Count; i++)
        {
            movementReferences.Add(await _references.GenerateAsync(StockMovementNumber.Prefix, StockMovementNumber.Width, cancellationToken));
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            await StockLedgerWriter.AcquireLocksAsync(
                _context,
                input.Lines.Select(l => (l.ProductId, input.LocationId)),
                cancellationToken);

            var movements = new List<StockMovementEntity>();
            for (var i = 0; i < input.Lines.Count; i++)
            {
                var line = input.Lines[i];
                var product = products[line.ProductId];
                var unitCost = line.UnitCost ?? product.Cost;
                if (unitCost < 0m)
                {
                    throw new ValidationException(
                        "The stock receipt could not be recorded.",
                        InventoryServiceHelpers.FieldError("lines", "The unit cost must not be negative."));
                }

                movements.Add(await _ledger.ApplyAsync(
                    StockMovementType.Receipt,
                    line.ProductId,
                    input.LocationId,
                    line.Quantity,
                    unitCost,
                    product.CurrencyCode,
                    businessDateUtc,
                    actorUserId,
                    externalReference,
                    sourceType: null,
                    sourceId: null,
                    movementReferences[i],
                    cancellationToken));
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            return movements.Select(InventoryMappings.ToMovementDto).ToList();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<decimal> GetQuantityAsync(Guid productId, Guid locationId, CancellationToken cancellationToken = default)
    {
        var quantity = await _context.StockBalances
            .AsNoTracking()
            .Where(b => b.ProductId == productId && b.LocationId == locationId)
            .Select(b => b.Quantity)
            .FirstOrDefaultAsync(cancellationToken);

        return quantity;
    }

    private ISystemClock _clock => _ledger.Clock;
}