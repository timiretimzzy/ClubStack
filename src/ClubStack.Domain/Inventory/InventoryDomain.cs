using ClubStack.Domain.Common;

namespace ClubStack.Domain.Inventory;

/// <summary>
/// Authoritative stock-transfer rules (Phase 3 §33). A transfer starts as a
/// Draft and either Completions (creating the linked transfer movements) or is
/// Cancelled before any ledger effect. There is no editing of a completed
/// transfer; corrections must be cancelled (pre-completion) or handled with a
/// compensating adjustment (Phase 10 §10 immutable records).
/// </summary>
public static class StockTransferStateTransitions
{
    public static bool CanTransition(StockTransferStatus from, StockTransferStatus to)
    {
        return (from, to) switch
        {
            (StockTransferStatus.Draft, StockTransferStatus.Completed) => true,
            (StockTransferStatus.Draft, StockTransferStatus.Cancelled) => true,
            _ => false,
        };
    }
}

/// <summary>
/// Authoritative stock-count rules (Phase 3 §34, INV-040..045). A count moves
/// InProgress → Completed → Approved; Approval materialises the count variances
/// as CountReconciliation movements and is the terminal step. A count that is
/// not yet Approved may be Cancelled to discard it without ledger effect.
/// </summary>
public static class StockCountStateTransitions
{
    public static bool CanTransition(StockCountStatus from, StockCountStatus to)
    {
        return (from, to) switch
        {
            (StockCountStatus.InProgress, StockCountStatus.Completed) => true,
            (StockCountStatus.Completed, StockCountStatus.Approved) => true,
            (StockCountStatus.InProgress, StockCountStatus.Cancelled) => true,
            (StockCountStatus.Completed, StockCountStatus.Cancelled) => true,
            _ => false,
        };
    }
}

// ---- Human-readable references (Phase 4 §17, Phase 8 §3) ----

/// <summary>Stock movement reference <c>STM-000124</c>.</summary>
public static class StockMovementNumber
{
    public const string Prefix = "STM";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => BusinessReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

/// <summary>Stock transfer reference <c>STF-000124</c>.</summary>
public static class StockTransferNumber
{
    public const string Prefix = "STF";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => BusinessReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

/// <summary>Stock count reference <c>SCN-000124</c>.</summary>
public static class StockCountNumber
{
    public const string Prefix = "SCN";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => BusinessReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

/// <summary>Stock adjustment / wastage reference <c>SADJ-000124</c>. Accounts already
/// reserve <c>ADJ</c>, so stock adjustments use their own prefix.</summary>
public static class StockAdjustmentNumber
{
    public const string Prefix = "SADJ";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => BusinessReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

internal static class BusinessReferenceHelpers
{
    public static bool IsValidFormat(string? value, string prefix, int width)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separatorIndex = value.IndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        if (!string.Equals(value[..separatorIndex], prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = value[(separatorIndex + 1)..];
        return digits.Length == width && digits.All(char.IsAsciiDigit);
    }
}

/// <summary>
/// Stock identifier/format rules (Phase 2 INV-002 SKU, INV-003 barcode,
/// phase 3 §21). The SKU is the user-supplied, unique product key; a barcode is
/// an optional additional identifier that must also resolve uniquely.
/// </summary>
/// <summary>
/// Selling tax treatment of a product (D5 POS §7). Pricing may be tax-inclusive,
/// tax-exclusive or zero-rated/exempt; the treatment is applied and preserved on
/// every sale line. A product without a configured tax rate behaves as exempt.
/// </summary>
public enum ProductTaxTreatment
{
    Inclusive,
    Exclusive,
    ZeroRated,
}

public static class StockIdentifier
{
    public const int SkuMinLength = 1;

    public const int SkuMaxLength = 32;

    public const int BarcodeMaxLength = 64;

    public static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized;
    }

    public static bool IsValidSku(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var sku = Normalize(value);
        return sku.Length is >= SkuMinLength and <= SkuMaxLength
               && sku.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ' ' or '.');
    }

    public static bool IsValidBarcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var barcode = Normalize(value);
        return barcode.Length <= BarcodeMaxLength && barcode.All(c => !char.IsControl(c));
    }
}

/// <summary>
/// Pure stock-ledger arithmetic (Phase 3 §32, Phase 9 §14). The quantity on a
/// finalized stock movement is its signed ledger contribution: positive for
/// inbound, negative for outbound. The current quantity of a product at a
/// location is always the sum of signed quantities over the movement ledger;
/// a cached stock balance may exist for performance but is never authoritative.
/// </summary>
public static class StockLedger
{
    /// <summary>
    /// Signed ledger contribution for a quantity entered as a magnitude (receipts,
    /// transfers, wastage, sale, return, damage, consumption) or as an already-signed
    /// value (adjustment, count reconciliation).
    /// </summary>
    public static decimal SignedQuantity(StockMovementType type, decimal quantity)
    {
        var magnitude = Math.Abs(quantity);
        return type switch
        {
            StockMovementType.Receipt => magnitude,
            StockMovementType.TransferIn => magnitude,
            StockMovementType.Adjustment => quantity,
            StockMovementType.CountReconciliation => quantity,
            StockMovementType.TransferOut => -magnitude,
            StockMovementType.Wastage => -magnitude,
            StockMovementType.Sale => -magnitude,
            StockMovementType.Damage => -magnitude,
            StockMovementType.Return => magnitude,
            StockMovementType.Consumption => -magnitude,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown movement type."),
        };
    }

    /// <summary>True when the movement type reduces stock.</summary>
    public static bool DecreasesStock(StockMovementType type) => SignedQuantity(type, 1m) < 0m;

    /// <summary>Derived quantity from finalized movements (signed sum).</summary>
    public static decimal ComputeBalance(IEnumerable<(StockMovementType Type, decimal Quantity)> movements)
        => movements.Sum(m => SignedQuantity(m.Type, m.Quantity));
}