using ClubStack.Application.Exceptions;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Infrastructure.Membership;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Inventory;

/// <summary>
/// Shared helpers for the inventory services: safe enum conversion, field-level
/// validation errors, currency resolution against the club default, unit-cost
/// fallback, and the operational guards every stock-changing path must satisfy.
/// </summary>
internal static class InventoryServiceHelpers
{
    public static TEnum ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
        => MembershipServiceHelpers.ParseEnum<TEnum>(value, fieldName);

    public static IDictionary<string, string[]> FieldError(string fieldName, string message)
        => MembershipServiceHelpers.FieldError(fieldName, message);

    public static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 250);
        return (safePage, safePageSize);
    }

    /// <summary>
    /// Resolves the club default currency; a caller-supplied currency (if any) must
    /// match it (products and stock are valued in the single default currency in D4).
    /// </summary>
    public static async Task<string> ResolveCurrencyCodeAsync(
        ClubStackDbContext context,
        string? requested,
        CancellationToken cancellationToken)
    {
        var defaultCode = await context.Currencies
            .Where(c => c.IsDefault)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No default currency is configured for the club.");

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var candidate = requested.Trim().ToUpperInvariant();
            if (!string.Equals(candidate, defaultCode, StringComparison.Ordinal))
            {
                throw new ValidationException(
                    "The value is invalid.",
                    FieldError("currencyCode", $"The currency must match the club default ('{defaultCode}')."));
            }
        }

        return defaultCode;
    }

    public static void EnsurePositive(decimal value, string fieldName)
    {
        if (value <= 0m)
        {
            throw new ValidationException(
                "The value is invalid.",
                FieldError(fieldName, "The quantity must be positive."));
        }
    }

    public static void EnsureNonNegative(decimal value, string fieldName)
    {
        if (value < 0m)
        {
            throw new ValidationException(
                "The value is invalid.",
                FieldError(fieldName, "The value must not be negative."));
        }
    }

    /// <summary>Stock operations require an <see cref="ProductStatus.Active"/>, stock-managed product (INV-010).</summary>
    public static void EnsureProductUsable(ProductEntity product)
    {
        if (product.Status != ProductStatus.Active)
        {
            throw new ConflictException($"Product '{product.Sku}' is not active.");
        }

        if (!product.StockManaged)
        {
            throw new ConflictException($"Product '{product.Sku}' is not stock-managed and cannot participate in stock movements.");
        }
    }

    /// <summary>Stock operations require an active location (Phase 3 §30 Status).</summary>
    public static void EnsureLocationActive(InventoryLocationEntity location)
    {
        if (location.Status != StockLocationStatus.Active)
        {
            throw new ConflictException($"Inventory location '{location.Code}' is not active.");
        }
    }
}

/// <summary>
/// Applies signed stock movements to the ledger (Phase 3 §31-32, Phase 9 §14). One
/// movement row is written per ledger effect and the cached stock balance is updated
/// in the same transaction; a decrease below zero is rejected. Calls must hold the
/// per (product, location) advisory lock for the duration of their transaction.
/// </summary>
internal sealed class StockLedgerWriter
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public StockLedgerWriter(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public ISystemClock Clock => _clock;

    /// <summary>
    /// Serializes writers for the given (product, location) pairs inside the caller's
    /// transaction. Keys are locked in sorted order so concurrent multi-item operations
    /// never deadlock (Phase 5 §54 loss prevention, Phase 10 §10 concurrent correctness).
    /// </summary>
    public static async Task AcquireLocksAsync(
        ClubStackDbContext context,
        IEnumerable<(Guid ProductId, Guid LocationId)> keys,
        CancellationToken cancellationToken)
    {
        foreach (var key in keys.Distinct().OrderBy(k => k.ProductId).ThenBy(k => k.LocationId))
        {
            var lockKey = $"{key.ProductId}|{key.LocationId}";
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        }
    }

    /// <summary>
    /// Writes a finalized movement and updates the cached balance. <paramref name="quantity"/>
    /// is the magnitude for in/out-flip movement types (Receipt, TransferIn, TransferOut,
    /// Wastage) and the signed ledger contribution for Adjustment/CountReconciliation.
    /// <paramref name="reference"/> must be allocated by the caller <em>before</em> the
    /// unit-of-work transaction starts, because the database-backed reference generator
    /// cannot run on a connection that already has an active transaction.
    /// </summary>
    public async Task<StockMovementEntity> ApplyAsync(
        StockMovementType type,
        Guid productId,
        Guid locationId,
        decimal quantity,
        decimal unitCost,
        string currencyCode,
        DateTimeOffset businessDateUtc,
        string userId,
        string? reason,
        string? sourceType,
        Guid? sourceId,
        string reference,
        CancellationToken cancellationToken)
    {
        var signed = StockLedger.SignedQuantity(type, quantity);
        var now = _clock.UtcNow;

        var balance = await _context.StockBalances
            .FirstOrDefaultAsync(b => b.ProductId == productId && b.LocationId == locationId, cancellationToken);

        var current = balance?.Quantity ?? 0m;
        var updated = current + signed;
        if (updated < 0m)
        {
            throw new ConflictException("The stock movement cannot be applied because it would take the available quantity below zero.");
        }

        if (balance is null)
        {
            _context.StockBalances.Add(new StockBalanceEntity
            {
                ProductId = productId,
                LocationId = locationId,
                Quantity = updated,
                UpdatedAtUtc = now,
            });
        }
        else
        {
            balance.Quantity = updated;
            balance.UpdatedAtUtc = now;
        }

        var movement = new StockMovementEntity
        {
            Reference = reference,
            ProductId = productId,
            LocationId = locationId,
            Quantity = signed,
            MovementType = type,
            UnitCost = unitCost,
            CurrencyCode = currencyCode,
            BusinessDateUtc = businessDateUtc,
            SourceType = InventoryServiceHelpers.NormalizeOptional(sourceType),
            SourceId = sourceId,
            Reason = InventoryServiceHelpers.NormalizeOptional(reason),
            UserId = userId,
            CreatedAtUtc = now,
        };

        _context.StockMovements.Add(movement);
        return movement;
    }
}