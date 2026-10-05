using ClubStack.Domain.Common;

namespace ClubStack.Domain.Accounts;

/// <summary>
/// Member account numbering (Phase 4 §17, ACC-001): reference <c>ACC-000124</c>
/// separate from the UUIDv7 primary key, produced by the reference generator.
/// </summary>
public static class MemberAccountNumber
{
    public const string Prefix = "ACC";

    public const int Width = 6;

    public static string ToDisplay(long sequence) => new BusinessReference(Prefix, sequence).ToFormattedString(Width);

    public static bool IsValidFormat(string? value)
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

        if (!string.Equals(value[..separatorIndex], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = value[(separatorIndex + 1)..];
        return digits.Length == Width && digits.All(char.IsAsciiDigit);
    }
}

/// <summary>
/// Pure ledger arithmetic (Phase 3 §19, ACC-010). The account balance is always
/// <em>derived</em> from posted transactions and never stored as a mutable field.
/// A transaction's ledger contribution is its signed amount; the balance is the
/// sum of signed amounts over Posted entries. Reversed originals are excluded,
/// their compensating Reversal entries carrying the stored signed offset.
/// </summary>
public static class AccountLedger
{
    /// <summary>
    /// Signed ledger contribution of a transaction with the given stored amount.
    /// Charges increase the amount due; payments, credits and credits-like
    /// entries reduce it. Adjustments and reversals carry their signed value.
    /// </summary>
    public static decimal SignedAmount(AccountTransactionType type, decimal amount)
    {
        return type switch
        {
            AccountTransactionType.Charge => amount,
            AccountTransactionType.Payment => -amount,
            AccountTransactionType.Credit => -amount,
            AccountTransactionType.Adjustment => amount,
            AccountTransactionType.Reversal => amount,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown transaction type."),
        };
    }

    /// <summary>Derived balance. Only Posted entries contribute.</summary>
    public static decimal ComputeBalance(IEnumerable<(AccountTransactionType Type, AccountTransactionStatus Status, decimal Amount)> entries)
    {
        return entries
            .Where(e => e.Status == AccountTransactionStatus.Posted)
            .Sum(e => SignedAmount(e.Type, e.Amount));
    }

    /// <summary>Debit-side vs credit-side display (Phase 5 §33 statement columns).</summary>
    public static bool IsDebitSide(AccountTransactionType type, decimal amount) => SignedAmount(type, amount) > 0;
}

/// <summary>One aging bracket produced by <see cref="AccountAging.ComputeBrackets"/>.</summary>
public sealed record AgingBucket(string Label, int DaysFrom, int? DaysTo, decimal OutstandingAmount);

/// <summary>
/// Aging of outstanding balances (ACC-012, AR-004). Outstanding = the portion of
/// a charge that payments/credits have not yet settled; age is measured from the
/// charge's accounting (business) date to the as-of date. Bracket boundaries are
/// configuration (default 30-day brackets) rather than a hard-coded policy.
/// </summary>
public static class AccountAging
{
    public static IReadOnlyList<AgingBucket> ComputeBrackets(
        DateTimeOffset asOfUtc,
        int bucketDays,
        int bucketCount,
        IEnumerable<(DateTimeOffset BusinessDate, decimal OutstandingAmount)> outstandingCharges)
    {
        if (bucketDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketDays), "Bucket length must be positive.");
        }

        if (bucketCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), "At least one bucket is required.");
        }

        var brackets = new AgingBucket[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            var daysFrom = i * bucketDays;
            brackets[i] = new AgingBucket(
                Label: i == 0 ? "Current" : $"{daysFrom}+",
                DaysFrom: daysFrom,
                DaysTo: i == bucketCount - 1 ? null : daysFrom + bucketDays - 1,
                OutstandingAmount: 0m);
        }

        var asOfDate = asOfUtc.UtcDateTime.Date;
        foreach (var item in outstandingCharges)
        {
            var ageDays = Math.Max(0, (asOfDate - item.BusinessDate.UtcDateTime.Date).Days);
            var index = Math.Min(ageDays / bucketDays, bucketCount - 1);
            brackets[index] = brackets[index] with { OutstandingAmount = brackets[index].OutstandingAmount + item.OutstandingAmount };
        }

        return brackets;
    }

    /// <summary>Total outstanding balance across all brackets.</summary>
    public static decimal TotalOutstanding(IEnumerable<AgingBucket> brackets) => brackets.Sum(b => b.OutstandingAmount);
}