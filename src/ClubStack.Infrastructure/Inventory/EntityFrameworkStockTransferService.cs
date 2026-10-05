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
public sealed class EntityFrameworkStockTransferService : IStockTransferService
{
    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;
    private readonly StockLedgerWriter _ledger;

    public EntityFrameworkStockTransferService(
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

    public async Task<StockTransferSearchResult> SearchAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = InventoryServiceHelpers.NormalizePage(page, pageSize);

        var total = await _context.StockTransfers.AsNoTracking().CountAsync(cancellationToken);
        var items = await _context.StockTransfers
            .AsNoTracking()
            .Include(t => t.FromLocation)
            .Include(t => t.ToLocation)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new StockTransferSearchResult(
            items.Select(InventoryMappings.ToTransferDto).ToList(),
            total,
            safePage,
            safePageSize);
    }

    public async Task<StockTransferDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await LoadTransferAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToTransferDto(transfer);
    }

    public async Task<StockTransferDto> CreateAsync(CreateStockTransferInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (input.FromLocationId == input.ToLocationId)
        {
            throw new ConflictException("A transfer must have different source and destination locations.");
        }

        if (input.Lines.Count == 0)
        {
            throw new ValidationException(
                "The transfer could not be created.",
                InventoryServiceHelpers.FieldError("lines", "At least one product line is required."));
        }

        await LoadLocationAsync(input.FromLocationId, "fromLocationId", cancellationToken);
        await LoadLocationAsync(input.ToLocationId, "toLocationId", cancellationToken);

        if (input.Lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
        {
            throw new ValidationException(
                "The transfer could not be created.",
                InventoryServiceHelpers.FieldError("lines", "A product may appear only once in a transfer."));
        }

        var products = await LoadProductsAsync(input.Lines.Select(l => l.ProductId).Distinct(), cancellationToken);
        foreach (var line in input.Lines)
        {
            var product = products[line.ProductId];
            InventoryServiceHelpers.EnsureProductUsable(product);
            InventoryServiceHelpers.EnsurePositive(line.Quantity, "lines");
        }

        var now = _clock.UtcNow;
        var transfer = new StockTransferEntity
        {
            TransferNumber = await _referenceGenerator.GenerateAsync(StockTransferNumber.Prefix, StockTransferNumber.Width, cancellationToken),
            FromLocationId = input.FromLocationId,
            ToLocationId = input.ToLocationId,
            Status = StockTransferStatus.Draft,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        foreach (var line in input.Lines)
        {
            var product = products[line.ProductId];
            transfer.Lines.Add(new StockTransferLineEntity
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost ?? product.Cost,
            });
        }

        _context.StockTransfers.Add(transfer);
        await _context.SaveChangesAsync(cancellationToken);

        var created = await LoadTransferAsync(transfer.Id, tracked: false, cancellationToken);
        return InventoryMappings.ToTransferDto(created);
    }

    public async Task<StockTransferDto> CompleteAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var transfer = await LoadTransferAsync(id, tracked: true, cancellationToken);
        if (!StockTransferStateTransitions.CanTransition(transfer.Status, StockTransferStatus.Completed))
        {
            throw new ConflictException($"Transfer '{transfer.TransferNumber}' cannot be completed from its '{transfer.Status}' state.");
        }

        var fromLocation = await LoadLocationAsync(transfer.FromLocationId, "fromLocationId", cancellationToken);
        var toLocation = await LoadLocationAsync(transfer.ToLocationId, "toLocationId", cancellationToken);

        var productIds = transfer.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await LoadProductsAsync(productIds, cancellationToken);
        foreach (var line in transfer.Lines)
        {
            InventoryServiceHelpers.EnsureProductUsable(products[line.ProductId]);
        }

        var businessDateUtc = _clock.UtcNow;

        // Two movements per line (TransferOut + TransferIn). References are
        // allocated before the transaction because the generator cannot run inside one.
        var movementReferences = new List<string>(transfer.Lines.Count * 2);
        for (var i = 0; i < transfer.Lines.Count * 2; i++)
        {
            movementReferences.Add(await _referenceGenerator.GenerateAsync(StockMovementNumber.Prefix, StockMovementNumber.Width, cancellationToken));
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var lockKeys = new List<(Guid ProductId, Guid LocationId)>();
            foreach (var line in transfer.Lines)
            {
                lockKeys.Add((line.ProductId, transfer.FromLocationId));
                lockKeys.Add((line.ProductId, transfer.ToLocationId));
            }

            await StockLedgerWriter.AcquireLocksAsync(_context, lockKeys, cancellationToken);

            for (var i = 0; i < transfer.Lines.Count; i++)
            {
                var line = transfer.Lines[i];
                var product = products[line.ProductId];

                await _ledger.ApplyAsync(
                    StockMovementType.TransferOut,
                    line.ProductId,
                    transfer.FromLocationId,
                    line.Quantity,
                    line.UnitCost,
                    product.CurrencyCode,
                    businessDateUtc,
                    actorUserId,
                    reason: null,
                    "StockTransfer",
                    transfer.Id,
                    movementReferences[(i * 2) + 0],
                    cancellationToken);

                await _ledger.ApplyAsync(
                    StockMovementType.TransferIn,
                    line.ProductId,
                    transfer.ToLocationId,
                    line.Quantity,
                    line.UnitCost,
                    product.CurrencyCode,
                    businessDateUtc,
                    actorUserId,
                    reason: null,
                    "StockTransfer",
                    transfer.Id,
                    movementReferences[(i * 2) + 1],
                    cancellationToken);
            }

            transfer.Status = StockTransferStatus.Completed;
            transfer.CompletedAtUtc = businessDateUtc;
            transfer.UpdatedAtUtc = businessDateUtc;

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var completed = await LoadTransferAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToTransferDto(completed);
    }

    public async Task<StockTransferDto> CancelAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var transfer = await LoadTransferAsync(id, tracked: true, cancellationToken);
        if (!StockTransferStateTransitions.CanTransition(transfer.Status, StockTransferStatus.Cancelled))
        {
            throw new ConflictException($"Transfer '{transfer.TransferNumber}' cannot be cancelled from its '{transfer.Status}' state.");
        }

        var now = _clock.UtcNow;
        transfer.Status = StockTransferStatus.Cancelled;
        transfer.CancelledAtUtc = now;
        transfer.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        var cancelled = await LoadTransferAsync(id, tracked: false, cancellationToken);
        return InventoryMappings.ToTransferDto(cancelled);
    }

    private async Task<StockTransferEntity> LoadTransferAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<StockTransferEntity> query = _context.StockTransfers
            .Include(t => t.FromLocation)
            .Include(t => t.ToLocation)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Stock transfer '{id}' was not found.");
    }

    private async Task<InventoryLocationEntity> LoadLocationAsync(Guid id, string fieldName, CancellationToken cancellationToken)
    {
        var location = await _context.InventoryLocations
                           .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                       ?? throw new ValidationException(
                           "The transfer could not be created.",
                           InventoryServiceHelpers.FieldError(fieldName, $"Inventory location '{id}' was not found."));
        InventoryServiceHelpers.EnsureLocationActive(location);
        return location;
    }

    private async Task<Dictionary<Guid, ProductEntity>> LoadProductsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().ToList();
        var products = await _context.Products
            .Where(p => distinctIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = distinctIds.FirstOrDefault(id => !products.ContainsKey(id));
        if (missing != Guid.Empty)
        {
            throw new NotFoundException($"Product '{missing}' was not found.");
        }

        return products;
    }
}