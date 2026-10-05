using ClubStack.Domain.Common;

namespace ClubStack.Domain.Finance;

/// <summary>
/// Finance business references. B12 fixes the prefixes and the six-digit width:
/// FA, JRN, AR, AP, PAY, REC, PER. All values are produced by the existing
/// reference-generation foundation.
/// </summary>
public static class FinancialAccountReference
{
    public const string Prefix = "FA";

    public const int Width = 6;
}

public static class JournalReference
{
    public const string Prefix = "JRN";

    public const int Width = 6;
}

public static class ReceivableReference
{
    public const string Prefix = "AR";

    public const int Width = 6;
}

public static class PayableReference
{
    public const string Prefix = "AP";

    public const int Width = 6;
}

public static class PaymentReference
{
    public const string Prefix = "PAY";

    public const int Width = 6;
}

public static class ReconciliationReference
{
    public const string Prefix = "REC";

    public const int Width = 6;
}

public static class FinancialPeriodReference
{
    public const string Prefix = "PER";

    public const int Width = 6;
}

/// <summary>Shared finance reference helpers.</summary>
public static class FinanceReferenceHelpers
{
    public static string Format(string prefix, long sequence) =>
        new BusinessReference(prefix, sequence).ToFormattedString(width: 6);

    /// <summary>True when a value matches <c>PREFIX-000000</c> for one of the finance prefixes.</summary>
    public static bool IsValidFormat(string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var expected = $"{prefix}-";
        if (!value.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = value[expected.Length..];
        return digits.Length == 6 && digits.All(char.IsAsciiDigit);
    }
}
