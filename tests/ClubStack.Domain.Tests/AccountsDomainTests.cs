using ClubStack.Domain.Accounts;

namespace ClubStack.Domain.Tests;

public class MemberAccountNumberTests
{
    [Theory]
    [InlineData(1, "ACC-000001")]
    [InlineData(124, "ACC-000124")]
    [InlineData(123456, "ACC-123456")]
    public void ToDisplay_FormatsWithFixedWidthPrefix(long sequence, string expected)
    {
        Assert.Equal(expected, MemberAccountNumber.ToDisplay(sequence));
    }

    [Theory]
    [InlineData("ACC-000124", true)]
    [InlineData("ACC-000001", true)]
    [InlineData("ACC-000000", true)]
    [InlineData("ACC-124", false)]
    [InlineData("ACC-0000124", false)]
    [InlineData("XCC-000124", false)]
    [InlineData("MEM-000124", false)]
    [InlineData("ACC-00012A", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, MemberAccountNumber.IsValidFormat(value));
    }
}

public class AccountLedgerTests
{
    [Fact]
    public void SignedAmount_ContributesAsExpected()
    {
        Assert.Equal(100.00m, AccountLedger.SignedAmount(AccountTransactionType.Charge, 100.00m));
        Assert.Equal(-50.00m, AccountLedger.SignedAmount(AccountTransactionType.Payment, 50.00m));
        Assert.Equal(-25.00m, AccountLedger.SignedAmount(AccountTransactionType.Credit, 25.00m));
        Assert.Equal(-10.00m, AccountLedger.SignedAmount(AccountTransactionType.Adjustment, -10.00m));
        Assert.Equal(10.00m, AccountLedger.SignedAmount(AccountTransactionType.Adjustment, 10.00m));
        Assert.Equal(50.00m, AccountLedger.SignedAmount(AccountTransactionType.Reversal, 50.00m));
    }

    [Fact]
    public void ComputeBalance_SumsPostedEntriesOnly()
    {
        (AccountTransactionType, AccountTransactionStatus, decimal)[] entries =
        {
            (AccountTransactionType.Charge, AccountTransactionStatus.Posted, 100m),
            (AccountTransactionType.Charge, AccountTransactionStatus.Reversed, 200m),
            (AccountTransactionType.Payment, AccountTransactionStatus.Posted, 30m),
            (AccountTransactionType.Credit, AccountTransactionStatus.Posted, 20m),
            (AccountTransactionType.Reversal, AccountTransactionStatus.Posted, 30m),
        };

        Assert.Equal(80m, AccountLedger.ComputeBalance(entries));
    }

    [Fact]
    public void ComputeBalance_EmptyLedger_IsZero()
    {
        Assert.Equal(0m, AccountLedger.ComputeBalance(Array.Empty<(AccountTransactionType, AccountTransactionStatus, decimal)>()));
    }

    [Fact]
    public void IsDebitSide_ReflectsSignedContribution()
    {
        Assert.True(AccountLedger.IsDebitSide(AccountTransactionType.Charge, 10m));
        Assert.False(AccountLedger.IsDebitSide(AccountTransactionType.Payment, 10m));
        Assert.False(AccountLedger.IsDebitSide(AccountTransactionType.Credit, 10m));
        Assert.False(AccountLedger.IsDebitSide(AccountTransactionType.Adjustment, -5m));
        Assert.True(AccountLedger.IsDebitSide(AccountTransactionType.Adjustment, 5m));
        Assert.True(AccountLedger.IsDebitSide(AccountTransactionType.Reversal, 10m));
    }
}

public class AccountAgingTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static (DateTimeOffset BusinessDate, decimal OutstandingAmount) Charge(int daysAgo, decimal amount)
        => (AsOf.AddDays(-daysAgo), amount);

    [Fact]
    public void ComputeBrackets_DefaultShape_HasFourBucketsWithCorrectLabels()
    {
        var buckets = AccountAging.ComputeBrackets(AsOf, 30, 4, Array.Empty<(DateTimeOffset, decimal)>());

        Assert.Equal(4, buckets.Count);
        Assert.Equal("Current", buckets[0].Label);
        Assert.Equal(0, buckets[0].DaysFrom);
        Assert.Equal(29, buckets[0].DaysTo);
        Assert.Equal("30+", buckets[1].Label);
        Assert.Equal("60+", buckets[2].Label);
        Assert.Equal("90+", buckets[3].Label);
        Assert.Null(buckets[3].DaysTo);
    }

    [Fact]
    public void ComputeBrackets_PlacesChargesByAge()
    {
        var buckets = AccountAging.ComputeBrackets(AsOf, 30, 4, new[]
        {
            Charge(0, 10m),    // current
            Charge(5, 20m),    // current
            Charge(30, 30m),   // 30+
            Charge(61, 40m),   // 60+
            Charge(120, 50m),  // clamps into final bucket
        });

        Assert.Equal(30m, buckets[0].OutstandingAmount);
        Assert.Equal(30m, buckets[1].OutstandingAmount);
        Assert.Equal(40m, buckets[2].OutstandingAmount);
        Assert.Equal(50m, buckets[3].OutstandingAmount);
        Assert.Equal(150m, AccountAging.TotalOutstanding(buckets));
    }

    [Fact]
    public void ComputeBrackets_TreatsFutureAsCurrent()
    {
        var buckets = AccountAging.ComputeBrackets(AsOf, 30, 4, new[] { Charge(-5, 25m) });
        Assert.Equal(25m, buckets[0].OutstandingAmount);
    }

    [Fact]
    public void ComputeBrackets_ZeroBucketDays_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccountAging.ComputeBrackets(AsOf, 0, 4, Array.Empty<(DateTimeOffset, decimal)>()));
    }

    [Fact]
    public void ComputeBrackets_ZeroBucketCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccountAging.ComputeBrackets(AsOf, 30, 0, Array.Empty<(DateTimeOffset, decimal)>()));
    }
}