using ClubStack.Domain.Inventory;

namespace ClubStack.Domain.Tests;

public class StockTransferStateTransitionTests
{
    [Theory]
    [InlineData(StockTransferStatus.Draft, StockTransferStatus.Completed, true)]
    [InlineData(StockTransferStatus.Draft, StockTransferStatus.Cancelled, true)]
    [InlineData(StockTransferStatus.Completed, StockTransferStatus.Completed, false)]
    [InlineData(StockTransferStatus.Completed, StockTransferStatus.Cancelled, false)]
    [InlineData(StockTransferStatus.Cancelled, StockTransferStatus.Completed, false)]
    [InlineData(StockTransferStatus.Cancelled, StockTransferStatus.Cancelled, false)]
    public void CanTransition_EnforcesLifecycle(StockTransferStatus from, StockTransferStatus to, bool expected)
    {
        Assert.Equal(expected, StockTransferStateTransitions.CanTransition(from, to));
    }
}

public class StockCountStateTransitionTests
{
    [Theory]
    [InlineData(StockCountStatus.InProgress, StockCountStatus.Completed, true)]
    [InlineData(StockCountStatus.Completed, StockCountStatus.Approved, true)]
    [InlineData(StockCountStatus.InProgress, StockCountStatus.Cancelled, true)]
    [InlineData(StockCountStatus.Completed, StockCountStatus.Cancelled, true)]
    [InlineData(StockCountStatus.Approved, StockCountStatus.Completed, false)]
    [InlineData(StockCountStatus.Approved, StockCountStatus.Cancelled, false)]
    [InlineData(StockCountStatus.InProgress, StockCountStatus.Approved, false)]
    public void CanTransition_EnforcesLifecycle(StockCountStatus from, StockCountStatus to, bool expected)
    {
        Assert.Equal(expected, StockCountStateTransitions.CanTransition(from, to));
    }
}

public class StockMovementNumberTests
{
    [Theory]
    [InlineData(1, "STM-000001")]
    [InlineData(124, "STM-000124")]
    [InlineData(123456, "STM-123456")]
    public void ToDisplay_FormatsWithFixedWidthPrefix(long sequence, string expected)
    {
        Assert.Equal(expected, StockMovementNumber.ToDisplay(sequence));
    }

    [Theory]
    [InlineData("STM-000124", true)]
    [InlineData("STM-000000", true)]
    [InlineData("STM-123456", true)]
    [InlineData("STM-124", false)]
    [InlineData("STM-0000124", false)]
    [InlineData("XTM-000124", false)]
    [InlineData("STF-000124", false)]
    [InlineData("STM-00012A", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, StockMovementNumber.IsValidFormat(value));
    }
}

public class StockTransferNumberTests
{
    [Theory]
    [InlineData(1, "STF-000001")]
    [InlineData(124, "STF-000124")]
    public void ToDisplay_FormatsWithFixedWidthPrefix(long sequence, string expected)
    {
        Assert.Equal(expected, StockTransferNumber.ToDisplay(sequence));
    }

    [Theory]
    [InlineData("STF-000124", true)]
    [InlineData("STM-000124", false)]
    [InlineData("STF-00012X", false)]
    [InlineData("STF-124", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, StockTransferNumber.IsValidFormat(value));
    }
}

public class StockCountNumberTests
{
    [Theory]
    [InlineData(1, "SCN-000001")]
    [InlineData(124, "SCN-000124")]
    public void ToDisplay_FormatsWithFixedWidthPrefix(long sequence, string expected)
    {
        Assert.Equal(expected, StockCountNumber.ToDisplay(sequence));
    }

    [Theory]
    [InlineData("SCN-000124", true)]
    [InlineData("SADJ-000124", false)]
    [InlineData("SCN-00012X", false)]
    [InlineData("SCN-124", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, StockCountNumber.IsValidFormat(value));
    }
}

public class StockAdjustmentNumberTests
{
    [Theory]
    [InlineData(1, "SADJ-000001")]
    [InlineData(124, "SADJ-000124")]
    public void ToDisplay_FormatsWithFixedWidthPrefix(long sequence, string expected)
    {
        Assert.Equal(expected, StockAdjustmentNumber.ToDisplay(sequence));
    }

    [Theory]
    [InlineData("SADJ-000124", true)]
    [InlineData("ADJ-000124", false)]
    [InlineData("STM-000124", false)]
    [InlineData("SADJ-00012X", false)]
    [InlineData("SADJ-124", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, StockAdjustmentNumber.IsValidFormat(value));
    }
}

public class StockIdentifierTests
{
    [Theory]
    [InlineData("SKU-001", true)]
    [InlineData("sku_001", true)]
    [InlineData("A B.C-1", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("SKU$!", false)]
    public void IsValidSku_ValidatesAllowedCharsAndLength(string? value, bool expected)
    {
        Assert.Equal(expected, StockIdentifier.IsValidSku(value));
    }

    [Theory]
    [InlineData("123456789012", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    public void IsValidBarcode_AllowsEmptyOrShortValues(string? value, bool expected)
    {
        Assert.Equal(expected, StockIdentifier.IsValidBarcode(value));
    }
}

public class StockLedgerTests
{
    [Fact]
    public void SignedQuantity_FlipsMagnitudeByType()
    {
        Assert.Equal(10m, StockLedger.SignedQuantity(StockMovementType.Receipt, 10m));
        Assert.Equal(15m, StockLedger.SignedQuantity(StockMovementType.TransferIn, 15m));
        Assert.Equal(-20m, StockLedger.SignedQuantity(StockMovementType.TransferOut, 20m));
        Assert.Equal(-8m, StockLedger.SignedQuantity(StockMovementType.Wastage, 8m));
        Assert.Equal(-12m, StockLedger.SignedQuantity(StockMovementType.Sale, 12m));
        Assert.Equal(7m, StockLedger.SignedQuantity(StockMovementType.Return, 7m));
        Assert.Equal(-3m, StockLedger.SignedQuantity(StockMovementType.Damage, 3m));
        Assert.Equal(-5m, StockLedger.SignedQuantity(StockMovementType.Consumption, 5m));
    }

    [Fact]
    public void SignedQuantity_AdjustmentAndReconciliationPassSignedValue()
    {
        Assert.Equal(-4m, StockLedger.SignedQuantity(StockMovementType.Adjustment, -4m));
        Assert.Equal(4m, StockLedger.SignedQuantity(StockMovementType.Adjustment, 4m));
        Assert.Equal(-6m, StockLedger.SignedQuantity(StockMovementType.CountReconciliation, -6m));
        Assert.Equal(6m, StockLedger.SignedQuantity(StockMovementType.CountReconciliation, 6m));
    }

    [Fact]
    public void DecreasesStock_IdentifiesOutboundTypes()
    {
        Assert.False(StockLedger.DecreasesStock(StockMovementType.Receipt));
        Assert.False(StockLedger.DecreasesStock(StockMovementType.TransferIn));
        Assert.True(StockLedger.DecreasesStock(StockMovementType.TransferOut));
        Assert.True(StockLedger.DecreasesStock(StockMovementType.Wastage));
        Assert.True(StockLedger.DecreasesStock(StockMovementType.Sale));
        Assert.True(StockLedger.DecreasesStock(StockMovementType.Damage));
        Assert.True(StockLedger.DecreasesStock(StockMovementType.Consumption));
        Assert.False(StockLedger.DecreasesStock(StockMovementType.Return));
        Assert.False(StockLedger.DecreasesStock(StockMovementType.Adjustment));
        Assert.False(StockLedger.DecreasesStock(StockMovementType.CountReconciliation));
    }

    [Fact]
    public void ComputeBalance_SumsSignedMovements()
    {
        (StockMovementType, decimal)[] movements =
        {
            (StockMovementType.Receipt, 100m),
            (StockMovementType.Receipt, 50m),
            (StockMovementType.TransferOut, 30m),
            (StockMovementType.TransferIn, 20m),
            (StockMovementType.Adjustment, -5m),
            (StockMovementType.Wastage, 10m),
        };

        Assert.Equal(125m, StockLedger.ComputeBalance(movements));
    }

    [Fact]
    public void ComputeBalance_EmptyLedger_IsZero()
    {
        Assert.Equal(0m, StockLedger.ComputeBalance(Array.Empty<(StockMovementType, decimal)>()));
    }
}