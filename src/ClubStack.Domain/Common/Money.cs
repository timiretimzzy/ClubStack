namespace ClubStack.Domain.Common;

/// <summary>
/// Immutable money value type. All ClubStack money uses a fixed decimal scale
/// (19,4) at the persistence boundary. Construction rejects negative amounts
/// for the default invariants; later slices may add currency-aware rules.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public static readonly Money Zero = new(0m, string.Empty);

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public Money(decimal amount, string currencyCode)
    {
        Amount = decimal.Round(amount, 4, MidpointRounding.ToEven);
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? string.Empty : currencyCode.Trim().ToUpperInvariant();
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.CurrencyCode);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.CurrencyCode);
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.CurrencyCode != right.CurrencyCode)
        {
            throw new InvalidOperationException(
                $"Cannot combine money in different currencies: '{left.CurrencyCode}' and '{right.CurrencyCode}'.");
        }
    }

    public override string ToString() => Amount.ToString("0.00###") + (string.IsNullOrEmpty(CurrencyCode) ? string.Empty : " " + CurrencyCode);
}