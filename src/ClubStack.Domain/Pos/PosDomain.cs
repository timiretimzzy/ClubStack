using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;

namespace ClubStack.Domain.Pos;

// ---- Human-readable references (Phase 4 §17, Phase 8 §3) ----

/// <summary>Sale reference <c>SALE-000124</c> (D5 POS).</summary>
public static class SaleNumber
{
    public const string Prefix = "SALE";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => PosReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

/// <summary>Receipt reference <c>RCT-000124</c> (D5 POS receipts).</summary>
public static class ReceiptNumber
{
    public const string Prefix = "RCT";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => PosReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

/// <summary>Cashier session reference <c>CSN-000124</c> (D5 Cashiering).</summary>
public static class CashierSessionNumber
{
    public const string Prefix = "CSN";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => PosReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

/// <summary>Sale refund reference <c>RFD-000124</c> (D5 POS refunds).</summary>
public static class SaleRefundNumber
{
    public const string Prefix = "RFD";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value) => PosReferenceHelpers.IsValidFormat(value, Prefix, Width);
}

internal static class PosReferenceHelpers
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

// ---- Pricing, tax and cash arithmetic (pure, Phase 3 §22, D5 POS) ----

/// <summary>
/// Price resolution outcomes for a POS line. <see cref="IsOverride"/> is true when
/// an authorised operator override supplied the effective unit price (D5 POS
/// pricing hierarchy: 1 override, 2 member/category price, 3 outlet/channel price,
/// 4 standard price). The effective price is <see cref="EffectiveUnitPrice"/>;
/// the original price is the price the hierarchy resolved before any override.
/// </summary>
public sealed record ResolvedLinePricing(
    decimal OriginalUnitPrice,
    decimal EffectiveUnitPrice,
    bool IsOverride);

/// <summary>Pure provisioning arithmetic used by the production sale service.</summary>
public static class PosPricing
{
    /// <summary>Rounds to the agreed money precision used across the ledger (Phase 8 §money).</summary>
    public static decimal Money(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Applies a line discount to a unit price. Amount subtracts directly; Percent
    /// reduces by the fraction. A discounted price never goes below zero.
    /// </summary>
    public static decimal ApplyDiscount(decimal unitPrice, SaleDiscountType type, decimal value)
    {
        var discounted = type switch
        {
            SaleDiscountType.Amount => unitPrice - value,
            SaleDiscountType.Percent => unitPrice * (1m - value / 100m),
            _ => unitPrice,
        };
        return Math.Max(0m, Money(discounted));
    }

    /// <summary>
    /// Produces the line tax figures for a quantity at a discounted unit price.
    /// <paramref name="taxRate"/> is the fractional rate (e.g. 0.15). Inclusive tax
    /// is embedded in the price, so the pre-tax base is derived by removing the rate;
    /// exclusive tax is added on top; zero-rated/exempt lines carry no tax.
    /// </summary>
    public static (decimal TaxableBase, decimal TaxAmount, decimal LineTotal) ComputeTaxedLine(
        decimal quantity,
        decimal discountedUnitPrice,
        ProductTaxTreatment treatment,
        decimal taxRate)
    {
        var gross = Money(discountedUnitPrice * quantity);
        return treatment switch
        {
            ProductTaxTreatment.Exclusive when taxRate > 0m =>
                (gross, Money(gross * taxRate), Money(gross * (1m + taxRate))),
            ProductTaxTreatment.Inclusive when taxRate > 0m =>
                (Money(gross / (1m + taxRate)), Money(gross - gross / (1m + taxRate)), gross),
            _ => (gross, 0m, gross),
        };
    }

    /// <summary>Money value of a percentage discount against a pre-tax quantity value.</summary>
    public static decimal PercentValue(decimal quantityValue, decimal percent) => Money(quantityValue * percent / 100m);
}

/// <summary>
/// Expected-cash computation for a cashier session (D5 Cashiering). Expected cash
/// on hand = opening float + cash sales − cash refunds + cash pay-ins − cash
/// pay-outs. The variance at close is declared − expected.
/// </summary>
public static class PosCashMath
{
    public static decimal ExpectedCashOnHand(
        decimal openingFloat,
        decimal cashSales,
        decimal cashRefunds,
        decimal payIns,
        decimal payOuts)
        => PosPricing.Money(openingFloat + cashSales - cashRefunds + payIns - payOuts);

    public static decimal Variance(decimal declaredCash, decimal expectedCash)
        => PosPricing.Money(declaredCash - expectedCash);
}

/// <summary>Session movement value reasons (reference the movement catalogue).</summary>
public static class PosMovementSources
{
    /// <summary>Stock movement source family recorded for sale movements.</summary>
    public const string Sale = "Sale";

    /// <summary>Stock movement source family recorded for refund movements.</summary>
    public const string SaleRefund = "SaleRefund";
}