namespace ClubStack.Infrastructure.Options;

/// <summary>
/// Member account options from the <c>Accounts</c> configuration section.
/// Aging bracket boundaries (ACC-012/AR-004) are configuration rather than a
/// hard-coded financial policy; defaults are 30-day brackets.
/// </summary>
public sealed class AccountOptions
{
    public const string SectionName = "Accounts";

    public int AgingBucketDays { get; set; } = 30;

    public int AgingBucketCount { get; set; } = 4;
}