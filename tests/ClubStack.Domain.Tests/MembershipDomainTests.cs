using ClubStack.Domain.Membership;

namespace ClubStack.Domain.Tests;

public class MembershipStateTransitionsTests
{
    // ---- Application state machine (Phase 8 §7.2) ----

    [Theory]
    [InlineData(MembershipApplicationStatus.Draft, MembershipApplicationStatus.Submitted)]
    [InlineData(MembershipApplicationStatus.Submitted, MembershipApplicationStatus.UnderReview)]
    [InlineData(MembershipApplicationStatus.Submitted, MembershipApplicationStatus.Rejected)]
    [InlineData(MembershipApplicationStatus.UnderReview, MembershipApplicationStatus.Approved)]
    [InlineData(MembershipApplicationStatus.UnderReview, MembershipApplicationStatus.Rejected)]
    [InlineData(MembershipApplicationStatus.Approved, MembershipApplicationStatus.Activated)]
    public void Application_ValidTransitions_AreAllowed(MembershipApplicationStatus from, MembershipApplicationStatus to)
    {
        Assert.True(MembershipStateTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(MembershipApplicationStatus.Draft, MembershipApplicationStatus.UnderReview)]
    [InlineData(MembershipApplicationStatus.Draft, MembershipApplicationStatus.Approved)]
    [InlineData(MembershipApplicationStatus.Draft, MembershipApplicationStatus.Activated)]
    [InlineData(MembershipApplicationStatus.Draft, MembershipApplicationStatus.Rejected)]
    [InlineData(MembershipApplicationStatus.Approved, MembershipApplicationStatus.Submitted)]
    [InlineData(MembershipApplicationStatus.Approved, MembershipApplicationStatus.Rejected)]
    [InlineData(MembershipApplicationStatus.Rejected, MembershipApplicationStatus.Activated)]
    [InlineData(MembershipApplicationStatus.Rejected, MembershipApplicationStatus.Submitted)]
    [InlineData(MembershipApplicationStatus.Rejected, MembershipApplicationStatus.Approved)]
    [InlineData(MembershipApplicationStatus.Activated, MembershipApplicationStatus.Approved)]
    public void Application_InvalidTransitions_AreRejected(MembershipApplicationStatus from, MembershipApplicationStatus to)
    {
        Assert.False(MembershipStateTransitions.CanTransition(from, to));
    }

    // ---- Member state machine (Phase 8 §7.1) ----

    [Theory]
    [InlineData(MemberStatus.Prospect, MemberStatus.Active)]
    [InlineData(MemberStatus.Active, MemberStatus.Suspended)]
    [InlineData(MemberStatus.Suspended, MemberStatus.Active)]
    [InlineData(MemberStatus.Active, MemberStatus.Cancelled)]
    public void Member_ValidTransitions_AreAllowed(MemberStatus from, MemberStatus to)
    {
        Assert.True(MembershipStateTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(MemberStatus.Prospect, MemberStatus.Cancelled)]
    [InlineData(MemberStatus.Prospect, MemberStatus.Suspended)]
    [InlineData(MemberStatus.Cancelled, MemberStatus.Active)]
    [InlineData(MemberStatus.Cancelled, MemberStatus.Suspended)]
    [InlineData(MemberStatus.Suspended, MemberStatus.Cancelled)]
    [InlineData(MemberStatus.Active, MemberStatus.Prospect)]
    public void Member_InvalidTransitions_AreRejected(MemberStatus from, MemberStatus to)
    {
        Assert.False(MembershipStateTransitions.CanTransition(from, to));
    }

    // ---- Membership state machine (Phase 8 §7.3) ----

    [Theory]
    [InlineData(MembershipStatus.Pending, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Active, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Suspended, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Active, MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Expired, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Active, MembershipStatus.Cancelled)]
    [InlineData(MembershipStatus.Pending, MembershipStatus.Transferred)]
    [InlineData(MembershipStatus.Active, MembershipStatus.Transferred)]
    public void Membership_ValidTransitions_AreAllowed(MembershipStatus from, MembershipStatus to)
    {
        Assert.True(MembershipStateTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(MembershipStatus.Pending, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Pending, MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Pending, MembershipStatus.Cancelled)]
    [InlineData(MembershipStatus.Active, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended, MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Suspended, MembershipStatus.Cancelled)]
    [InlineData(MembershipStatus.Expired, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Expired, MembershipStatus.Cancelled)]
    [InlineData(MembershipStatus.Cancelled, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Cancelled, MembershipStatus.Transferred)]
    [InlineData(MembershipStatus.Transferred, MembershipStatus.Active)]
    public void Membership_InvalidTransitions_AreRejected(MembershipStatus from, MembershipStatus to)
    {
        Assert.False(MembershipStateTransitions.CanTransition(from, to));
    }
}

public class MembershipNumberTests
{
    [Fact]
    public void ToDisplay_FormatsWithFixedWidthPrefix()
    {
        Assert.Equal("MEM-000001", MembershipNumber.ToDisplay(1));
    }

    [Fact]
    public void ToDisplay_ExampleMatchesPhase4()
    {
        Assert.Equal("MEM-000124", MembershipNumber.ToDisplay(124));
    }

    [Fact]
    public void ToDisplay_NeverTruncatesWidth()
    {
        Assert.Equal("MEM-123456", MembershipNumber.ToDisplay(123456));
    }

    [Theory]
    [InlineData("MEM-000124", true)]
    [InlineData("MEM-000001", true)]
    [InlineData("MEM-000000", true)]
    [InlineData("MEM-124", false)]
    [InlineData("MEM-0000124", false)]
    [InlineData("PO-000124", false)]
    [InlineData("124", false)]
    [InlineData("MEM-", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("MEM-00012A", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, MembershipNumber.IsValidFormat(value));
    }
}

public class FeeProrationTests
{
    [Fact]
    public void Apply_FullPeriodRemaining_ReturnsFullAmount()
    {
        Assert.Equal(12000m, FeeProration.Apply(12000m, 365, 365));
    }

    [Fact]
    public void Apply_MoreThanFullPeriodRemaining_ReturnsFullAmount()
    {
        Assert.Equal(12000m, FeeProration.Apply(12000m, 365, 400));
    }

    [Fact]
    public void Apply_PartialPeriodRemaining_ProratesByRemainingFraction()
    {
        // 12000 * 183 / 365 = 6016.438356(..) -> 6016.4384
        Assert.Equal(6016.4384m, FeeProration.Apply(12000m, 365, 183));
    }

    [Fact]
    public void Apply_NoPeriodRemaining_Zeroes()
    {
        Assert.Equal(0m, FeeProration.Apply(12000m, 365, 0));
    }

    [Fact]
    public void Apply_NonDivisible_RoundsToFourDecimals()
    {
        // 10000 * 10 / 12 = 8333.33333(..) -> 8333.3333
        Assert.Equal(8333.3333m, FeeProration.Apply(10000m, 12, 10));
    }

    [Fact]
    public void Apply_MillionsScaleAllocation_IsAccurate()
    {
        var prorated = FeeProration.Apply(1_000_000m, 30, 29);
        Assert.Equal(decimal.Round(1_000_000m * 29 / 30, 4, MidpointRounding.ToEven), prorated);
    }

    [Fact]
    public void Apply_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeeProration.Apply(-1m, 365, 100));
    }

    [Fact]
    public void Apply_NonPositivePeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeeProration.Apply(100m, 0, 0));
    }

    [Fact]
    public void Apply_NegativeRemaining_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeeProration.Apply(100m, 365, -1));
    }
}