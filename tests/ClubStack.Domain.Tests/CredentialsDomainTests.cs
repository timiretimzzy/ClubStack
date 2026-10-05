using ClubStack.Domain.Credentials;

namespace ClubStack.Domain.Tests;

public class CredentialStateTransitionsTests
{
    // ---- Phase 8 §7.4 credential state machine ----

    [Theory]
    [InlineData(CredentialStatus.Issued, CredentialStatus.Active)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Suspended)]
    [InlineData(CredentialStatus.Suspended, CredentialStatus.Active)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Lost)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Stolen)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Expired)]
    [InlineData(CredentialStatus.Lost, CredentialStatus.Replaced)]
    [InlineData(CredentialStatus.Stolen, CredentialStatus.Replaced)]
    public void ValidTransitions_AreAllowed(CredentialStatus from, CredentialStatus to)
    {
        Assert.True(CredentialStateTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(CredentialStatus.Issued, CredentialStatus.Suspended)]
    [InlineData(CredentialStatus.Issued, CredentialStatus.Lost)]
    [InlineData(CredentialStatus.Issued, CredentialStatus.Stolen)]
    [InlineData(CredentialStatus.Issued, CredentialStatus.Expired)]
    [InlineData(CredentialStatus.Issued, CredentialStatus.Replaced)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Active)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Issued)]
    [InlineData(CredentialStatus.Active, CredentialStatus.Replaced)]
    [InlineData(CredentialStatus.Suspended, CredentialStatus.Suspended)]
    [InlineData(CredentialStatus.Suspended, CredentialStatus.Lost)]
    [InlineData(CredentialStatus.Suspended, CredentialStatus.Stolen)]
    [InlineData(CredentialStatus.Suspended, CredentialStatus.Expired)]
    [InlineData(CredentialStatus.Suspended, CredentialStatus.Replaced)]
    [InlineData(CredentialStatus.Lost, CredentialStatus.Active)]
    [InlineData(CredentialStatus.Lost, CredentialStatus.Suspended)]
    [InlineData(CredentialStatus.Lost, CredentialStatus.Expired)]
    [InlineData(CredentialStatus.Lost, CredentialStatus.Stolen)]
    [InlineData(CredentialStatus.Stolen, CredentialStatus.Active)]
    [InlineData(CredentialStatus.Stolen, CredentialStatus.Suspended)]
    [InlineData(CredentialStatus.Stolen, CredentialStatus.Lost)]
    [InlineData(CredentialStatus.Expired, CredentialStatus.Active)]
    [InlineData(CredentialStatus.Expired, CredentialStatus.Replaced)]
    [InlineData(CredentialStatus.Replaced, CredentialStatus.Active)]
    public void InvalidTransitions_AreRejected(CredentialStatus from, CredentialStatus to)
    {
        Assert.False(CredentialStateTransitions.CanTransition(from, to));
    }

    // ---- Expiry (CRED-010) ----

    [Fact]
    public void ActiveCredentialWithPassedExpiry_IsExpiredDue()
    {
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        Assert.True(CredentialStateTransitions.IsExpiredDue(CredentialStatus.Active, now.AddSeconds(-1), now));
    }

    [Fact]
    public void ActiveCredentialWithFutureExpiry_IsNotExpiredDue()
    {
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        Assert.False(CredentialStateTransitions.IsExpiredDue(CredentialStatus.Active, now.AddSeconds(1), now));
    }

    [Fact]
    public void ActiveCredentialWithoutExpiry_IsNotExpiredDue()
    {
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        Assert.False(CredentialStateTransitions.IsExpiredDue(CredentialStatus.Active, null, now));
    }

    [Fact]
    public void NonActiveCredentialIsNeverExpired()
    {
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        Assert.False(CredentialStateTransitions.IsExpiredDue(CredentialStatus.Suspended, now.AddSeconds(-1), now));
        Assert.False(CredentialStateTransitions.IsExpiredDue(CredentialStatus.Expired, now.AddSeconds(-1), now));
    }
}

public class CredentialNumberTests
{
    [Theory]
    [InlineData(1, "CRD-000001")]
    [InlineData(124, "CRD-000124")]
    [InlineData(123456, "CRD-123456")]
    public void ToDisplay_FormatsWithFixedWidthPrefix(long sequence, string expected)
    {
        Assert.Equal(expected, CredentialNumber.ToDisplay(sequence));
    }

    [Theory]
    [InlineData("CRD-000124", true)]
    [InlineData("CRD-000001", true)]
    [InlineData("CRD-000000", true)]
    [InlineData("CRD-124", false)]
    [InlineData("CRD-0000124", false)]
    [InlineData("XRD-000124", false)]
    [InlineData("MEM-000124", false)]
    [InlineData("124", false)]
    [InlineData("CRD-", false)]
    [InlineData("CRD-00012A", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidFormat_ValidatesShape(string? value, bool expected)
    {
        Assert.Equal(expected, CredentialNumber.IsValidFormat(value));
    }
}

public class CardIdentifierTests
{
    [Fact]
    public void Normalize_TrimsAndUppercases()
    {
        Assert.Equal("ABC-123-DEF", CardIdentifier.Normalize("  abc-123-def  "));
    }

    [Fact]
    public void Normalize_EmptyInput_YieldsEmpty()
    {
        Assert.Equal(string.Empty, CardIdentifier.Normalize(null));
        Assert.Equal(string.Empty, CardIdentifier.Normalize("   "));
    }

    [Theory]
    [InlineData("ABC123", true)]
    [InlineData("abc123", true)]
    [InlineData("ABC-123", true)]
    [InlineData("ABC_123", true)]
    [InlineData("A1B2C3D4E5F6", true)]
    [InlineData("AB12", false)]
    [InlineData("ABC 123", false)]
    [InlineData("ABC.123", false)]
    [InlineData("ABC/123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidFormat_AcceptsAlnumAndSeparatorsOnly(string? value, bool expected)
    {
        Assert.Equal(expected, CardIdentifier.IsValidFormat(value));
    }

    [Fact]
    public void IsValidFormat_RejectsBeyondSixtyFourCharacters()
    {
        Assert.False(CardIdentifier.IsValidFormat(new string('A', 65)));
        Assert.True(CardIdentifier.IsValidFormat(new string('A', 6)));
        Assert.True(CardIdentifier.IsValidFormat(new string('A', 64)));
    }
}