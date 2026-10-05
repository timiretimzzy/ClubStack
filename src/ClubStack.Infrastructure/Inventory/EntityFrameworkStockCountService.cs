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
public sealed class EntityFrameworkStockCountService : IStockCountService
{
    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;
    private readonly StockLedgerWriter _ledger;

    public EntityFrameworkStockCountService(
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

    public async Task<StockCountSearchResult> SearchAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = InventoryServiceHelpers.NormalizePage(page, pageSize);

        var total = await _context.StockCounts.AsNoTracking().CountAsync(cancellationToken);
        var items = await _context.StockCounts
            .AsNoTracking()
            .Include(c => c.Location)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var dtos = new List<StockCountDto>();
        foreach (var item in items)
        {
            var lines = await LoadCountLinesAsync(item.Id, tracked: false, cancellationToken);
            dtos.Add(InventoryMappings.ToCountDto(item, lines));
        }

        return new StockCountSearchResult(dtos, total, safePage, safePageSize);
    }

    public async Task<StockCountDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var count = await LoadCountAsync(id, tracked: false, cancellationToken);
        var lines = await LoadCountLinesAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToCountDto(count, lines);
    }

    public async Task<StockCountDto> InitiateAsync(InitiateStockCountInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var location = await LoadLocationAsync(input.LocationId, cancellationToken);

        // A count covers the stock-managed products that currently have a ledger
        // balance at the location (INV-040), optionally restricted to one product.
        IQueryable<StockBalanceEntity> balances = _context.StockBalances
            .AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.LocationId == input.LocationId && b.Product.StockManaged && b.Product.Status == ProductStatus.Active);

        if (input.ProductId is { } productId)
        {
            balances = balances.Where(b => b.ProductId == productId);
        }

        var balanceRows = await balances.ToListAsync(cancellationToken);
        if (balanceRows.Count == 0)
        {
            throw new ConflictException("There is no stock to count at this location.");
        }

        var now = _clock.UtcNow;
        var count = new StockCountEntity
        {
            CountNumber = await _referenceGenerator.GenerateAsync(StockCountNumber.Prefix, StockCountNumber.Width, cancellationToken),
            LocationId = location.Id,
            Status = StockCountStatus.InProgress,
            CreatedByUserId = actorUserId,
            StartedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        foreach (var balance in balanceRows)
        {
            count.Lines.Add(new StockCountLineEntity
            {
                ProductId = balance.ProductId,
                ExpectedQuantity = balance.Quantity,
                CountedQuantity = null,
                Variance = 0m,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        _context.StockCounts.Add(count);
        await _context.SaveChangesAsync(cancellationToken);

        var created = await LoadCountAsync(count.Id, tracked: false, cancellationToken);
        var createdLines = await LoadCountLinesAsync(count.Id, tracked: false, cancellationToken);
        return InventoryMappings.ToCountDto(created, createdLines);
    }

    public async Task<StockCountDto> RecordLinesAsync(Guid id, RecordStockCountLinesInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var count = await LoadCountAsync(id, tracked: true, cancellationToken);
        if (count.Status != StockCountStatus.InProgress)
        {
            throw new ConflictException($"Count '{count.CountNumber}' cannot be updated in its '{count.Status}' state.");
        }

        if (input.Items.Count == 0)
        {
            throw new ValidationException(
                "The count could not be updated.",
                InventoryServiceHelpers.FieldError("items", "At least one counted line is required."));
        }

        var lines = await LoadCountLinesAsync(id, tracked: true, cancellationToken);
        var linesByProduct = lines.ToDictionary(l => l.ProductId);

        var now = _clock.UtcNow;
        foreach (var item in input.Items)
        {
            if (!linesByProduct.TryGetValue(item.ProductId, out var line))
            {
                throw new ValidationException(
                    "The count could not be updated.",
                    InventoryServiceHelpers.FieldError("items", $"Product '{item.ProductId}' is not part of this count."));
            }

            InventoryServiceHelpers.EnsureNonNegative(item.CountedQuantity, "items");

            line.CountedQuantity = item.CountedQuantity;
            line.Variance = item.CountedQuantity - line.ExpectedQuantity;
            line.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await LoadCountAsync(id, tracked: false, cancellationToken);
        var updatedLines = await LoadCountLinesAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToCountDto(updated, updatedLines);
    }

    public async Task<StockCountDto> CompleteAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var count = await LoadCountAsync(id, tracked: true, cancellationToken);
        if (!StockCountStateTransitions.CanTransition(count.Status, StockCountStatus.Completed))
        {
            throw new ConflictException($"Count '{count.CountNumber}' cannot be completed from its '{count.Status}' state.");
        }

        count.Status = StockCountStatus.Completed;
        count.CompletedAtUtc = _clock.UtcNow;
        count.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var completed = await LoadCountAsync(id, tracked: false, cancellationToken);
        var lines = await LoadCountLinesAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToCountDto(completed, lines);
    }

    public async Task<StockCountDto> ApproveAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var count = await LoadCountAsync(id, tracked: true, cancellationToken);
        if (!StockCountStateTransitions.CanTransition(count.Status, StockCountStatus.Approved))
        {
            throw new ConflictException($"Count '{count.CountNumber}' cannot be approved from its '{count.Status}' state.");
        }

        var lines = await LoadCountLinesAsync(id, tracked: false, cancellationToken);
        var varianceLines = lines.Where(l => l.Variance != 0m).ToList();

        Dictionary<Guid, ProductEntity> products = new();
        if (varianceLines.Count > 0)
        {
            products = await _context.Products.AsNoTracking()
                .Where(p => varianceLines.Select(v => v.ProductId).Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);
        }

        // One reconciliation movement per variance line; references must be
        // allocated before the transaction starts (see StockLedgerWriter).
        var movementReferences = new List<string>(varianceLines.Count);
        for (var i = 0; i < varianceLines.Count; i++)
        {
            movementReferences.Add(await _referenceGenerator.GenerateAsync(StockMovementNumber.Prefix, StockMovementNumber.Width, cancellationToken));
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            await StockLedgerWriter.AcquireLocksAsync(
                _context,
                varianceLines.Select(l => (l.ProductId, count.LocationId)),
                cancellationToken);

            for (var i = 0; i < varianceLines.Count; i++)
            {
                var line = varianceLines[i];
                var product = products[line.ProductId];

                // Variance = counted - expected; signed by nature. Applied as a
                // CountReconciliation movement so the ledger reflects the count.
                await _ledger.ApplyAsync(
                    StockMovementType.CountReconciliation,
                    line.ProductId,
                    count.LocationId,
                    line.Variance,
                    product.Cost,
                    product.CurrencyCode,
                    _clock.UtcNow,
                    actorUserId,
                    reason: null,
                    "StockCount",
                    count.Id,
                    movementReferences[i],
                    cancellationToken);
            }

            count.Status = StockCountStatus.Approved;
            count.ApprovedAtUtc = _clock.UtcNow;
            count.UpdatedAtUtc = _clock.UtcNow;

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var approved = await LoadCountAsync(id, tracked: false, cancellationToken);
        var approvedLines = await LoadCountLinesAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToCountDto(approved, approvedLines);
    }

    public async Task<StockCountDto> CancelAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var count = await LoadCountAsync(id, tracked: true, cancellationToken);
        if (count.Status == StockCountStatus.Approved)
        {
            throw new ConflictException($"Count '{count.CountNumber}' cannot be cancelled; it is already approved.");
        }

        count.Status = StockCountStatus.Cancelled;
        count.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var cancelled = await LoadCountAsync(id, tracked: false, cancellationToken);
        var lines = await LoadCountLinesAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToCountDto(cancelled, lines);
    }

    private async Task<StockCountEntity> LoadCountAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<StockCountEntity> query = _context.StockCounts.Include(c => c.Location);
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Stock count '{id}' was not found.");
    }

    private Task<List<StockCountLineEntity>> LoadCountLinesAsync(Guid countId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<StockCountLineEntity> query = _context.StockCountLines
            .Include(l => l.Product);
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return query
            .Where(l => l.CountId == countId)
            .OrderBy(l => l.Product.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task<InventoryLocationEntity> LoadLocationAsync(Guid id, CancellationToken cancellationToken)
    {
        var location = await _context.InventoryLocations
                           .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                       ?? throw new NotFoundException($"Inventory location '{id}' was not found.");
        InventoryServiceHelpers.EnsureLocationActive(location);
        return location;
    }
}