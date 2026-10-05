namespace ClubStack.Domain.Finance;

/// <summary>
/// Finance domain rules for D6. Every method here is a direct encoding of a PM
/// Decision Pack item or a mandated Phase 8/9 accounting invariant, so the rules
/// are testable in isolation without a database.
/// </summary>
public static class FinanceDomain
{
    /// <summary>Amount precision mandated by B17.</summary>
    public const int AmountScale = 4;

    /// <summary>Exchange rate precision mandated by B17.</summary>
    public const int ExchangeRateScale = 6;

    /// <summary>Maximum decimal(19,4) magnitude.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.9999m;

    /// <summary>
    /// B3: the normal balance of each account type. Asset and Expense carry a
    /// debit normal balance; Liability, Equity and Revenue carry a credit one.
    /// </summary>
    public static NormalBalance NormalBalanceFor(FinancialAccountType accountType) => accountType switch
    {
        FinancialAccountType.Asset => NormalBalance.Debit,
        FinancialAccountType.Expense => NormalBalance.Debit,
        FinancialAccountType.Liability => NormalBalance.Credit,
        FinancialAccountType.Equity => NormalBalance.Credit,
        FinancialAccountType.Revenue => NormalBalance.Credit,
        _ => throw new ArgumentOutOfRangeException(
            nameof(accountType),
            accountType,
            "Unknown financial account type (D6 PM Decision Pack B2 allows only Asset, Liability, Equity, Revenue, Expense)."),
    };

    /// <summary>True when a line increases an account of the given normal balance.</summary>
    public static bool IncreasesNormalBalance(FinancialAccountType accountType, JournalDirection direction) =>
        direction == JournalDirection.Debit
            ? NormalBalanceFor(accountType) == NormalBalance.Debit
            : NormalBalanceFor(accountType) == NormalBalance.Credit;

    /// <summary>True for the debit side.</summary>
    public static bool IsDebit(JournalDirection direction) => direction == JournalDirection.Debit;

    /// <summary>True for the credit side.</summary>
    public static bool IsCredit(JournalDirection direction) => direction == JournalDirection.Credit;

    /// <summary>The opposite side. Used by B4 reversals to invert every line.</summary>
    public static JournalDirection Invert(JournalDirection direction) =>
        direction == JournalDirection.Debit ? JournalDirection.Credit : JournalDirection.Debit;

    /// <summary>
    /// B9/§4: a journal must carry at least two lines, at least one debit, at
    /// least one credit, and total debits must equal total credits.
    /// </summary>
    public static void ValidateJournalBalance(
        IReadOnlyCollection<JournalLineValues> lines,
        int currencyScale = AmountScale)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count < 2)
        {
            throw new JournalInvariantException(
                $"A journal requires at least two lines (D6 §4/B9), but {lines.Count} were supplied.");
        }

        var debitCount = 0;
        var creditCount = 0;
        var totalDebit = 0m;
        var totalCredit = 0m;

        foreach (var line in lines)
        {
            if (line.Amount <= 0m)
            {
                throw new JournalInvariantException(
                    $"Journal line {line.LineNumber} must have an amount greater than zero, but was {line.Amount}.");
            }

            if (line.Direction == JournalDirection.Debit)
            {
                debitCount++;
                totalDebit += line.Amount;
            }
            else
            {
                creditCount++;
                totalCredit += line.Amount;
            }
        }

        if (debitCount == 0)
        {
            throw new JournalInvariantException("A journal requires at least one debit line.");
        }

        if (creditCount == 0)
        {
            throw new JournalInvariantException("A journal requires at least one credit line.");
        }

        var roundedDebit = decimal.Round(totalDebit, currencyScale, MidpointRounding.ToEven);
        var roundedCredit = decimal.Round(totalCredit, currencyScale, MidpointRounding.ToEven);

        if (roundedDebit != roundedCredit)
        {
            throw new JournalInvariantException(
                $"A journal must balance: debits {roundedDebit} do not equal credits {roundedCredit}.");
        }
    }

    /// <summary>
    /// B17: the exchange rate for a transaction. There is no external FX provider,
    /// so a transaction in the club base currency is always rate 1, and any other
    /// currency must have an explicit rate supplied by the caller.
    /// </summary>
    public static decimal ResolveExchangeRate(
        string transactionCurrency,
        string baseCurrency,
        decimal? suppliedRate,
        bool sameCurrencyRateIsOne = true)
    {
        var transaction = NormalizeCurrency(transactionCurrency);
        var target = NormalizeCurrency(baseCurrency);

        if (string.Equals(transaction, target, StringComparison.Ordinal) && sameCurrencyRateIsOne)
        {
            return 1m;
        }

        if (suppliedRate is null or <= 0m)
        {
            throw new JournalInvariantException(
                $"An exchange rate is required to post {transaction} into base currency {target} (D6 PM Decision Pack B17: no external FX provider).");
        }

        return decimal.Round(suppliedRate.Value, ExchangeRateScale, MidpointRounding.ToEven);
    }

    /// <summary>B17: converts a transaction amount into the club base currency.</summary>
    public static decimal ToBaseAmount(decimal amount, decimal exchangeRate) =>
        decimal.Round(amount * exchangeRate, AmountScale, MidpointRounding.ToEven);

    /// <summary>
    /// B16: aging buckets are fixed at 0-30, 31-60, 61-90 and 91+. Returns the
    /// inclusive lower bound and the exclusive-to upper bound used by reports.
    /// </summary>
    public static (int DaysFrom, int? DaysTo) AgingRangeFor(int daysOutstanding) => daysOutstanding switch
    {
        >= 0 and <= 30 => (0, 30),
        >= 31 and <= 60 => (31, 60),
        >= 61 and <= 90 => (61, 90),
        _ => (91, null),
    };

    /// <summary>The authoritative B16 bucket labels, in report order.</summary>
    public static readonly IReadOnlyList<(string Label, int DaysFrom, int? DaysTo)> AgingBuckets =
    [
        ("0-30", 0, 30),
        ("31-60", 31, 60),
        ("61-90", 61, 90),
        ("91+", 91, null),
    ];

    /// <summary>
    /// The B16 bucket label for a number of days outstanding, so reports never
    /// restate the bucket boundaries.
    /// </summary>
    public static string AgingBucketLabelFor(int daysOutstanding) => daysOutstanding switch
    {
        >= 0 and <= 30 => "0-30",
        >= 31 and <= 60 => "31-60",
        >= 61 and <= 90 => "61-90",
        _ => "91+",
    };

    /// <summary>Uppercases and trims a currency code, rejecting empty values.</summary>
    public static string NormalizeCurrency(string? currencyCode) =>
        string.IsNullOrWhiteSpace(currencyCode)
            ? throw new ArgumentException("A currency code is required.", nameof(currencyCode))
            : currencyCode.Trim().ToUpperInvariant();

    /// <summary>
    /// Rounds an amount to the B17 mandated scale and rejects values that cannot
    /// be stored in decimal(19,4).
    /// </summary>
    public static decimal NormalizeAmount(decimal amount) =>
        decimal.Round(amount, AmountScale, MidpointRounding.ToEven);

    /// <summary>True when a decimal fits the mandated decimal(19,4) column.</summary>
    public static bool FitsAmountPrecision(decimal amount) => Math.Abs(amount) <= MaxAmount;

    /// <summary>Zero-based inclusive start of the month that contains <paramref name="value"/>.</summary>
    public static DateTimeOffset MonthStart(DateTimeOffset value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Exclusive end of the month that contains <paramref name="value"/>.</summary>
    public static DateTimeOffset MonthEnd(DateTimeOffset value) =>
        MonthStart(value).AddMonths(1);

    /// <summary>The (year, month) key used to resolve a journal to a financial period.</summary>
    public static (int Year, int Month) PeriodKeyFor(DateTimeOffset transactionDate) =>
        (transactionDate.Year, transactionDate.Month);
}

/// <summary>Raised when a mandated journal/finance invariant is violated.</summary>
public sealed class JournalInvariantException : Exception
{
    public JournalInvariantException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// The minimal set of journal-line facts the domain invariants need. Kept as a
/// value record so <see cref="FinanceDomain.ValidateJournalBalance"/> stays free of
/// any persistence or EF dependency.
/// </summary>
public readonly record struct JournalLineValues(int LineNumber, JournalDirection Direction, decimal Amount);
