using ClubStack.Application.Exceptions;
using ClubStack.Application.Inventory;
using ClubStack.Domain.Inventory;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Ledger integrity and concurrency guarantees of the D4 stock services
/// (Phase 3 §31-32, Phase 5 §54-55, Phase 9 §14): the cached balance always
/// equals the sum of signed movements, transfers/counts/adjustments leave a
/// consistent trace, an overdraw is rejected atomically, and concurrent
/// stock-changing operations run serially per (product, location).
/// </summary>
[Collection("Database")]
public sealed class InventoryLedgerIntegrityTests : DatabaseTestBase
{
    public InventoryLedgerIntegrityTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Concurrent_receipts_never_lose_movements_and_match_derived_balance()
    {
        var (productId, locationA, locationB) = await CreateFixtureAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => WriteReceiptAsync(productId, locationA, 10m)));

        Assert.All(results, r => Assert.Equal("Receipt", r[0].MovementType));

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        var movementRows = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId && m.LocationId == locationA)
            .Select(m => new StockLedgerRow(m.MovementType, m.Quantity))
            .ToListAsync();
        var derived = StockLedger.ComputeBalance(movementRows.Select(r => (r.Type, r.Quantity)));
        var cached = await verify.StockBalances
            .AsNoTracking()
            .Where(b => b.ProductId == productId && b.LocationId == locationA)
            .Select(b => b.Quantity)
            .SingleAsync();

        Assert.Equal(80m, derived);
        Assert.Equal(derived, cached);
        Assert.Equal(8, movementRows.Count);
        Assert.Equal(8, await verify.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId && m.LocationId == locationA && m.SourceType == null).CountAsync());

        // Location B for this product stays at zero: the advisories are isolated by key.
        Assert.Equal(0m, await verify.StockBalances.AsNoTracking()
            .Where(b => b.ProductId == productId && b.LocationId == locationB)
            .Select(b => b.Quantity)
            .SingleOrDefaultAsync());
    }

    [Fact]
    public async Task Transfer_creates_linked_pair_and_keeps_balances_reconciled()
    {
        var (productId, locationA, locationB) = await CreateFixtureAsync();
        await WriteReceiptAsync(productId, locationA, 50m);

        using var scope = Services.CreateScope();
        var transfers = scope.ServiceProvider.GetRequiredService<IStockTransferService>();

        var draft = await transfers.CreateAsync(
            new CreateStockTransferInput(locationA, locationB,
            [new CreateStockTransferLineInput(productId, 20m, null)]),
            "user-1");
        Assert.Equal(StockTransferStatus.Draft, ParseTransfer(draft.Status));

        var completed = await transfers.CompleteAsync(draft.Id, "user-1");
        Assert.Equal(StockTransferStatus.Completed, ParseTransfer(completed.Status));
        Assert.Single(completed.Lines);

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        var linked = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SourceType == "StockTransfer" && m.SourceId == draft.Id)
            .ToListAsync();
        Assert.Equal(2, linked.Count);
        Assert.All(linked, m => Assert.Equal(20m, Math.Abs(m.Quantity)));
        Assert.Contains(linked, m => m.MovementType == StockMovementType.TransferOut);
        Assert.Contains(linked, m => m.MovementType == StockMovementType.TransferIn);

        Assert.Equal(30m, await QuantityAsync(productId, locationA));
        Assert.Equal(20m, await QuantityAsync(productId, locationB));

        // Cancel after completion is rejected; nothing is written.
        var ex = await Assert.ThrowsAsync<ConflictException>(() => transfers.CancelAsync(draft.Id, "user-1"));
        Assert.Contains("cannot be cancelled", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Complete a cancelled draft is rejected and writes no movements.
        var cancelled = await transfers.CreateAsync(
            new CreateStockTransferInput(locationB, locationA,
            [new CreateStockTransferLineInput(productId, 5m, null)]),
            "user-1");
        await transfers.CancelAsync(cancelled.Id, "user-1");
        await Assert.ThrowsAsync<ConflictException>(() => transfers.CompleteAsync(cancelled.Id, "user-1"));
        Assert.Equal(0, await verify.StockMovements.AsNoTracking()
            .CountAsync(m => m.SourceType == "StockTransfer" && m.SourceId == cancelled.Id));
    }

    [Fact]
    public async Task Count_approval_posts_reconciliation_variance_once()
    {
        var (productId, locationA, _) = await CreateFixtureAsync();
        await WriteReceiptAsync(productId, locationA, 40m);

        using var scope = Services.CreateScope();
        var counts = scope.ServiceProvider.GetRequiredService<IStockCountService>();

        var count = await counts.InitiateAsync(
            new InitiateStockCountInput(locationA, productId), "user-1");
        Assert.Single(count.Lines);
        Assert.Equal(40m, count.Lines[0].ExpectedQuantity);

        await counts.RecordLinesAsync(count.Id,
            new RecordStockCountLinesInput([new RecordStockCountLineInput(productId, 35m)]), "user-1");

        var completed = await counts.CompleteAsync(count.Id, "user-1");
        Assert.Equal(StockCountStatus.Completed, ParseCount(completed.Status));

        await Assert.ThrowsAsync<ConflictException>(() => counts.CompleteAsync(count.Id, "user-1"));

        var approved = await counts.ApproveAsync(count.Id, "user-1");
        Assert.Equal(StockCountStatus.Approved, ParseCount(approved.Status));
        Assert.Equal(-5m, approved.Lines[0].Variance);

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        var reconciliations = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SourceType == "StockCount" && m.SourceId == count.Id)
            .ToListAsync();
        Assert.Single(reconciliations);
        Assert.Equal(StockMovementType.CountReconciliation, reconciliations[0].MovementType);
        Assert.Equal(-5m, reconciliations[0].Quantity);

        Assert.Equal(35m, await QuantityAsync(productId, locationA));
    }

    [Fact]
    public async Task Overdraft_and_wastage_rules_reject_with_no_partial_writes()
    {
        var (productId, locationA, _) = await CreateFixtureAsync();
        await WriteReceiptAsync(productId, locationA, 10m);
        var before = await MovementCountAsync();

        using var scope = Services.CreateScope();
        var adjustments = scope.ServiceProvider.GetRequiredService<IStockAdjustmentService>();

        // A decrease below zero is rejected atomically.
        var ex = await Assert.ThrowsAsync<ConflictException>(() => adjustments.CreateAsync(
            new CreateStockAdjustmentInput("Adjustment", locationA, "write off",
            [new CreateStockAdjustmentLineInput(productId, -11m, null)], null),
            "user-1"));
        Assert.Contains("below zero", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Wastage with a negative magnitude is rejected atomically.
        await Assert.ThrowsAsync<ValidationException>(() => adjustments.CreateAsync(
            new CreateStockAdjustmentInput("Wastage", locationA, "damage",
            [new CreateStockAdjustmentLineInput(productId, -5m, null)], null),
            "user-1"));

        // A zero-quantity adjustment line is rejected atomically.
        await Assert.ThrowsAsync<ValidationException>(() => adjustments.CreateAsync(
            new CreateStockAdjustmentInput("Adjustment", locationA, "no-op",
            [new CreateStockAdjustmentLineInput(productId, 0m, null)], null),
            "user-1"));

        await using (var verify = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            // Nothing persisted from the rejected attempts (no header, no movements).
            Assert.Equal(before, await MovementCountAsync());
            Assert.Equal(0, await verify.StockAdjustments.AsNoTracking().CountAsync(a => a.CreatedByUserId == "user-1"));
            Assert.Equal(0, await verify.StockMovements.AsNoTracking().CountAsync(m => m.SourceType == "StockAdjustment"));
        }

        Assert.Equal(10m, await QuantityAsync(productId, locationA));

        // A legitimate value still works.
        var ok = await adjustments.CreateAsync(
            new CreateStockAdjustmentInput("Adjustment", locationA, "cycle count",
            [new CreateStockAdjustmentLineInput(productId, 2m, null)], null),
            "user-1");
        Assert.Equal(12m, await QuantityAsync(productId, locationA));
        Assert.StartsWith("SADJ-", ok.AdjustmentNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overdraft_concurrently_is_rejected_for_loser_writer()
    {
        var (productId, locationA, locationB) = await CreateFixtureAsync();
        await WriteReceiptAsync(productId, locationA, 10m);

        // Two writers each transfer 8 units out of a 10-unit balance to the same
        // destination. The (product, location) advisory lock serialises them, so
        // exactly one succeeds; the other must be rejected and the balance can
        // never go negative or double-spend.
        var attempted = new[]
        {
            WriteTransferOutAsync(productId, locationA, locationB, 8m),
            WriteTransferOutAsync(productId, locationA, locationB, 8m),
        };
        var outcomes = await Task.WhenAll(attempted);

        Assert.Single(outcomes, o => o == TransferOutcome.Completed);
        Assert.Single(outcomes, o => o == TransferOutcome.Rejected);
        Assert.Equal(2m, await QuantityAsync(productId, locationA));
        Assert.Equal(8m, await QuantityAsync(productId, locationB));
    }

    private sealed record FixtureRow(Guid ProductId, Guid LocationA, Guid LocationB);

    private sealed record StockLedgerRow(StockMovementType Type, decimal Quantity);

    private async Task<FixtureRow> CreateFixtureAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var suffix = tag[..6].ToUpperInvariant();

        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
        await seeder.SeedAsync();

        var categories = scope.ServiceProvider.GetRequiredService<IProductCategoryService>();
        var locations = scope.ServiceProvider.GetRequiredService<IInventoryLocationService>();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();

        var category = await categories.CreateAsync(
            new CreateCategoryInput($"CAT{suffix}", "Ledger fixture", null), "user-d4test");
        var locationA = await locations.CreateAsync(
            new CreateInventoryLocationInput($"LCA{suffix}", "Fixture A", "Warehouse"), "user-d4test");
        var locationB = await locations.CreateAsync(
            new CreateInventoryLocationInput($"LCB{suffix}", "Fixture B", "Store"), "user-d4test");
        var product = await products.CreateAsync(new CreateProductInput(
            $"SKU{suffix}", "Ledger fixture product", category.Id, "Each", null,
            StockManaged: true, Cost: 5m, PriceAmount: 10m, ReorderLevel: null,
            SupplierId: null, CurrencyCode: null),
            "user-d4test");

        return new FixtureRow(product.Id, locationA.Id, locationB.Id);
    }

    private async Task<IReadOnlyList<StockMovementDto>> WriteReceiptAsync(Guid productId, Guid locationId, decimal quantity)
    {
        using var scope = Services.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IStockLedgerService>();
        return await ledger.ReceiveAsync(
            new ReceiveStockInput(locationId,
            [new ReceiveStockLineInput(productId, quantity, null)], null, null),
            $"receiver-{Guid.NewGuid():N}");
    }

    private async Task<bool> TryCompleteAsync(IStockTransferService transfers, Guid id, string actor)
    {
        try
        {
            await transfers.CompleteAsync(id, actor);
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }

    private async Task<TransferOutcome> WriteTransferOutAsync(Guid productId, Guid fromLocation, Guid toLocation, decimal quantity)
    {
        var actor = $"transfer-{Guid.NewGuid():N}";
        using var scope = Services.CreateScope();
        var transfers = scope.ServiceProvider.GetRequiredService<IStockTransferService>();

        var draft = await transfers.CreateAsync(
            new CreateStockTransferInput(fromLocation, toLocation,
            [new CreateStockTransferLineInput(productId, quantity, null)]),
            actor);

        var completed = await TryCompleteAsync(transfers, draft.Id, actor);
        return completed ? TransferOutcome.Completed : TransferOutcome.Rejected;
    }

    private async Task<decimal> QuantityAsync(Guid productId, Guid locationId)
    {
        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        var balance = await verify.StockBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.ProductId == productId && b.LocationId == locationId);
        return balance?.Quantity ?? 0m;
    }

    private async Task<int> MovementCountAsync()
    {
        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        return await verify.StockMovements.AsNoTracking().CountAsync();
    }

    private static StockTransferStatus ParseTransfer(string value) => Enum.Parse<StockTransferStatus>(value);

    private static StockCountStatus ParseCount(string value) => Enum.Parse<StockCountStatus>(value);

    private enum TransferOutcome
    {
        None,
        Completed,
        Rejected,
    }
}