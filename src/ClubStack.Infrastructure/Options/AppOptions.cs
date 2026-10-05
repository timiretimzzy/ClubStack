namespace ClubStack.Infrastructure.Options;

/// <summary>Runtime bootstrap options from the <c>App</c> configuration section.</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    public bool MigrateOnStartup { get; set; } = true;

    public bool StopOnMigrationFailure { get; set; } = true;

    public bool EnableDevelopmentSeed { get; set; }

    /// <summary>
    /// Enables the controlled first-run bootstrap endpoints (Phase 9 §27). When
    /// false (the default) the endpoints refuse to run, protecting the instance
    /// from accidental/uncontrolled bootstrap.
    /// </summary>
    public bool EnableBootstrap { get; set; }

    public string DevelopmentAdminUserName { get; set; } = string.Empty;

    public string DevelopmentAdminPassword { get; set; } = string.Empty;
}

/// <summary>Technical identity/security bootstrap options.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Minimum length enforced for user passwords. Defaults meet secure baselines.</summary>
    public int RequiredLength { get; set; } = 12;

    public bool RequireDigits { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; } = true;

    public int LockoutMaxFailedAttempts { get; set; } = 5;

    public TimeSpan LockoutDefaultTime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Absolute server-side session lifetime (Phase 8 §23). Defaults to 12 hours.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>Idle timeout; sessions with no activity beyond this are treated as expired (Phase 8 §23).</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(60);
}