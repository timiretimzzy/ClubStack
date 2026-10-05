using ClubStack.Application.Auditing;
using ClubStack.Application.Accounts;
using ClubStack.Application.Bootstrap;
using ClubStack.Application.Correlation;
using ClubStack.Application.Credentials;
using ClubStack.Application.Finance;
using ClubStack.Application.Fiscalization;
using ClubStack.Application.Golf;
using ClubStack.Application.Idempotency;
using ClubStack.Application.Identity;
using ClubStack.Application.Inventory;
using ClubStack.Application.Membership;
using ClubStack.Application.Outbox;
using ClubStack.Application.Platform;
using ClubStack.Application.Pos;
using ClubStack.Application.References;
using ClubStack.Infrastructure.Auditing;
using ClubStack.Infrastructure.Accounts;
using ClubStack.Infrastructure.Bootstrap;
using ClubStack.Infrastructure.Credentials;
using ClubStack.Infrastructure.Finance;
using ClubStack.Infrastructure.Fiscalization;
using ClubStack.Infrastructure.Golf;
using ClubStack.Infrastructure.Idempotency;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Inventory;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Membership;
using ClubStack.Infrastructure.Options;
using ClubStack.Infrastructure.Outbox;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Platform;
using ClubStack.Infrastructure.Pos;
using ClubStack.Infrastructure.References;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;

namespace ClubStack.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .Validate(o => o.ContainsValidConfiguration());

        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName));

        var connectionString = ConfigurationSecretExpander.Expand(
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. See docs/local-setup.md and scripts/Setup-LocalSecrets.ps1."));

        var developmentTools = bool.TryParse(
            configuration.GetSection(AppOptions.SectionName)["DevelopmentTools"],
            out var enabled) && enabled;

        // NOTE: EnableRetryOnFailure is intentionally NOT enabled. EfUnitOfWork starts
        // ambient, user-initiated transactions (Begin/Commit/Rollback), and EF Core
        // forbids a retrying execution strategy together with user-initiated
        // transactions. Revisit when transactions move to a strategy-wrapped,
        // lambda-based pattern (see EF Core "explicit transactions" docs).
        // DbContextOptions is registered as a singleton so the singleton
        // IDbContextFactory (used for startup migrations and background workers)
        // can consume it without violating scope validation. EF's TryAdd keeps this
        // registration for the scoped AddDbContext registrations below.
        services.AddSingleton(sp =>
        {
            var builder = new DbContextOptionsBuilder<ClubStackDbContext>();
            builder.UseNpgsql(connectionString);
            if (developmentTools)
            {
                builder.EnableDetailedErrors();
                builder.EnableSensitiveDataLogging();
            }
            return builder.Options;
        });

        services.AddDbContext<ClubStackDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            if (developmentTools)
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddDbContextFactory<ClubStackDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<CorrelationContextV1>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContextV1>());
        services.AddScoped<ICorrelationContextWriter>(sp => sp.GetRequiredService<CorrelationContextV1>());

        services.AddScoped<IAuditRecorder, EfAuditRecorder>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IEventDispatcher, OutboxEventDispatcher>();
        services.AddScoped<IReferenceGenerator, EfReferenceGenerator>();

        services.AddScoped<IPlatformConfigurationStore, EntityFrameworkPlatformConfigurationStore>();
        services.AddScoped<IIdentityAdministrationService, EntityFrameworkIdentityAdministrationService>();
        services.AddScoped<IUserSessionStore, EntityFrameworkUserSessionStore>();
        services.AddScoped<IReferenceDataSeeder, ReferenceDataSeeder>();
        services.AddScoped<IBootstrapService, BootstrapService>();

        services.AddScoped<IMembershipDirectoryService, EntityFrameworkMembershipDirectoryService>();
        services.AddScoped<IMembershipCategoryService, EntityFrameworkMembershipCategoryService>();
        services.AddScoped<IMembershipLifecycleService, EntityFrameworkMembershipLifecycleService>();
        services.AddScoped<IMembershipFeeService, EntityFrameworkMembershipFeeService>();

        services.AddOptions<AccountOptions>()
            .Bind(configuration.GetSection(AccountOptions.SectionName));

        services.AddScoped<ICredentialService, EntityFrameworkCredentialService>();
        services.AddScoped<IMemberAccountService, EntityFrameworkMemberAccountService>();

        services.AddScoped<IProductService, EntityFrameworkProductService>();
        services.AddScoped<IProductCategoryService, EntityFrameworkProductCategoryService>();
        services.AddScoped<IInventoryLocationService, EntityFrameworkInventoryLocationService>();
        services.AddScoped<ISupplierService, EntityFrameworkSupplierService>();
        services.AddScoped<IStockLedgerService, EntityFrameworkStockLedgerService>();
        services.AddScoped<IStockTransferService, EntityFrameworkStockTransferService>();
        services.AddScoped<IStockCountService, EntityFrameworkStockCountService>();
        services.AddScoped<IStockAdjustmentService, EntityFrameworkStockAdjustmentService>();

        services.AddScoped<IPosProductService, EntityFrameworkPosProductService>();
        services.AddScoped<IPosConfigurationService, EntityFrameworkPosConfigurationService>();
        services.AddScoped<ICashierSessionService, EntityFrameworkCashierSessionService>();
        services.AddScoped<ISaleService, EntityFrameworkSaleService>();

        // D5.5 fiscalisation. The gateway is a singleton because the FiscalStackZW
        // facade is process-wide mutable state and serialises its own calls behind one
        // gate; the services around it are scoped per request/unit of work.
        services.AddOptions<FiscalisationDeviceOptions>()
            .Bind(configuration.GetSection(FiscalisationDeviceOptions.SectionName));
        services.AddSingleton<IFiscalisationGateway, FiscalStackZWDllGateway>();
        services.AddScoped<FiscalisationProcessor>();
        services.AddScoped<IFiscalisationService, FiscalisationService>();
        services.AddScoped<IFiscalisationRequirementWriter, FiscalisationRequirementWriter>();
        services.AddScoped<IOutboxMessageHandler, FiscalisationOutboxHandler>();
        services.AddScoped<IOutboxMessageHandler, FiscalisationCreditNoteHandler>();

        // D6 Finance. Every automated posting goes through IFinancePostingService, which
        // reads only the configured posting-rule mappings, so no account identifier is
        // hard-coded in a posting path.
        services.AddScoped<IFinancialAccountService, EntityFrameworkFinancialAccountService>();
        services.AddScoped<IFinancialPeriodService, EntityFrameworkFinancialPeriodService>();
        services.AddScoped<IJournalService, EntityFrameworkJournalService>();
        services.AddScoped<IFinancePostingService, EntityFrameworkFinancePostingService>();
        services.AddScoped<IReceivableService, EntityFrameworkReceivableService>();
        services.AddScoped<IPayableService, EntityFrameworkPayableService>();
        services.AddScoped<IFinancePaymentService, EntityFrameworkFinancePaymentService>();
        services.AddScoped<IFinancialReconciliationService, EntityFrameworkFinancialReconciliationService>();
        services.AddScoped<IFinanceReportService, EntityFrameworkFinanceReportService>();
        services.AddScoped<FinanceReferenceSeeder>();
        services.AddScoped<IFinanceRequirementWriter, FinanceRequirementWriter>();

        // D6 operational handlers. Each one turns an existing D3/D4/D5 operational
        // record into exactly one journal. The supplier invoice and supplier payment
        // rules (§14.6, §14.7) are D6-native documents created through the finance API,
        // so they post synchronously and need no outbox handler.
        services.AddScoped<IOutboxMessageHandler, FinanceMemberPaymentOutboxHandler>();
        services.AddScoped<IOutboxMessageHandler, FinanceMembershipFeeOutboxHandler>();
        services.AddScoped<IOutboxMessageHandler, PosSaleFinancialOutboxHandler>();
        services.AddScoped<IOutboxMessageHandler, PosRefundFinancialOutboxHandler>();

        // D7 Golf. The booking service takes the concrete guest service rather than
        // IGolfGuestService because completing a booking updates the guest's visit
        // history, which is an internal step of the lifecycle rather than a
        // separately authorized command.
        services.AddScoped<EntityFrameworkGolfCourseService>();
        services.AddScoped<EntityFrameworkGolfTeeSlotService>();
        services.AddScoped<EntityFrameworkGolfGuestService>();
        services.AddScoped<EntityFrameworkGolfBookingService>();
        services.AddScoped<EntityFrameworkGolfChargeService>();
        services.AddScoped<IGolfCourseService>(sp => sp.GetRequiredService<EntityFrameworkGolfCourseService>());
        services.AddScoped<IGolfTeeSlotService>(sp => sp.GetRequiredService<EntityFrameworkGolfTeeSlotService>());
        services.AddScoped<IGolfGuestService>(sp => sp.GetRequiredService<EntityFrameworkGolfGuestService>());
        services.AddScoped<IGolfBookingService>(sp => sp.GetRequiredService<EntityFrameworkGolfBookingService>());
        services.AddScoped<IGolfChargeService>(sp => sp.GetRequiredService<EntityFrameworkGolfChargeService>());
        services.AddScoped<GolfConfigurationSeeder>();

        services.AddModuleIdentity(options =>
        {
            var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
            options.Password.RequiredLength = security.RequiredLength;
            options.Password.RequireDigit = security.RequireDigits;
            options.Password.RequireLowercase = security.RequireLowercase;
            options.Password.RequireUppercase = security.RequireUppercase;
            options.Password.RequireNonAlphanumeric = security.RequireNonAlphanumeric;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
            options.Lockout.MaxFailedAccessAttempts = security.LockoutMaxFailedAttempts;
            options.Lockout.DefaultLockoutTimeSpan = security.LockoutDefaultTime;
        });

        services.AddDevelopmentSeeder();
        services.AddQuartzInfrastructure();

        return services;
    }

    private static bool ContainsValidConfiguration(this AppOptions options)
    {
        if (options.MigrateOnStartup && options.EnableDevelopmentSeed &&
            (string.IsNullOrWhiteSpace(options.DevelopmentAdminUserName) ||
             string.IsNullOrWhiteSpace(options.DevelopmentAdminPassword)))
        {
            return false;
        }

        return true;
    }

    private static IServiceCollection AddModuleIdentity(this IServiceCollection services, Action<IdentityOptions>? configureOptions)
    {
        // Required by DataProtectorTokenProvider (password-reset tokens).
        services.AddDataProtection();

        var builder = services.AddIdentityCore<ClubStackUser>(configureOptions ?? (_ => { }))
            .AddRoles<ClubStackRole>()
            .AddEntityFrameworkStores<ClubStackDbContext>() // ClusterStackDbContext stores identity rows.
            .AddTokenProvider<DataProtectorTokenProvider<ClubStackUser>>(TokenOptions.DefaultProvider); // Password-reset tokens (Phase 8 §8.2).

        _ = builder;

        services.Replace(
            ServiceDescriptor.Scoped<IUserClaimsPrincipalFactory<ClubStackUser>, ClubStackUserClaimsPrincipalFactory>());

        return services;
    }

    private static IServiceCollection AddDevelopmentSeeder(this IServiceCollection services)
    {
        services.AddScoped<IDevelopmentDataSeeder, DevelopmentDataSeeder>();
        return services;
    }

    private static IServiceCollection AddQuartzInfrastructure(this IServiceCollection services)
    {
        services.AddQuartz(quartz =>
        {
            var jobKey = new JobKey("OutboxProcessor");
            quartz.AddJob<OutboxProcessorJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("OutboxProcessor-Trigger")
                .StartNow()
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(TimeSpan.FromSeconds(5))
                    .RepeatForever()));
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }
}

public static class ConfigurationSecretExpander
{
    /// <summary>
    /// Expands <c>${VAR}</c> tokens found in configuration values using the process
    /// environment, falling back to the supplied <paramref name="secretValues"/>
    /// lookup (e.g. user-secrets). Secrets are never required to be committed.
    /// </summary>
    public static string Expand(string? value, Func<string, string?>? secretValues = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("A required configuration value is empty.");
        }

        var output = value;
        var tokenIndex = output.IndexOf("${", StringComparison.Ordinal);

        while (tokenIndex >= 0)
        {
            var endIndex = output.IndexOf('}', tokenIndex + 2);
            if (endIndex < 0)
            {
                break;
            }

            var variable = output[(tokenIndex + 2)..endIndex];
            var resolved = Environment.GetEnvironmentVariable(variable) ?? secretValues?.Invoke(variable);

            if (resolved is null)
            {
                throw new InvalidOperationException(
                    $"Configuration references the secret '${variable}' but no value was supplied via environment or user-secrets. " +
                    "See scripts/Setup-LocalSecrets.ps1 in the repository.");
            }

            output = string.Concat(output.AsSpan(0, tokenIndex), resolved, output.AsSpan(endIndex + 1));
            tokenIndex = output.IndexOf("${", StringComparison.Ordinal);
        }

        return output;
    }
}