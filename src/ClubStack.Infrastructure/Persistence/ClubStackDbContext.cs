using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubStack.Infrastructure.Persistence;

public sealed class ClubStackDbContext : IdentityDbContext<ClubStackUser, ClubStackRole, Guid>
{
    public ClubStackDbContext(DbContextOptions<ClubStackDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    public DbSet<ReferenceSequenceEntity> ReferenceSequences => Set<ReferenceSequenceEntity>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<ClubEntity> Clubs => Set<ClubEntity>();

    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();

    public DbSet<LocationEntity> Locations => Set<LocationEntity>();

    public DbSet<OutletEntity> Outlets => Set<OutletEntity>();

    public DbSet<FacilityEntity> Facilities => Set<FacilityEntity>();

    public DbSet<PaymentMethodEntity> PaymentMethods => Set<PaymentMethodEntity>();

    public DbSet<CurrencyEntity> Currencies => Set<CurrencyEntity>();

    public DbSet<TaxRateEntity> TaxRates => Set<TaxRateEntity>();

    public DbSet<SystemConfigurationEntity> SystemConfigurations => Set<SystemConfigurationEntity>();

    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();

    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();

    public DbSet<PersonEntity> Persons => Set<PersonEntity>();

    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    public DbSet<MembershipCategoryEntity> MembershipCategories => Set<MembershipCategoryEntity>();

    public DbSet<MembershipApplicationEntity> MembershipApplications => Set<MembershipApplicationEntity>();

    public DbSet<MembershipEntity> Memberships => Set<MembershipEntity>();

    public DbSet<MembershipStatusHistoryEntity> MembershipStatusHistory => Set<MembershipStatusHistoryEntity>();

    public DbSet<HouseholdEntity> Households => Set<HouseholdEntity>();

    public DbSet<HouseholdMemberEntity> HouseholdMembers => Set<HouseholdMemberEntity>();

    public DbSet<MembershipDependantEntity> MembershipDependants => Set<MembershipDependantEntity>();

    public DbSet<MembershipFeeEntity> MembershipFees => Set<MembershipFeeEntity>();

    public DbSet<MembershipFeeAssessmentEntity> MembershipFeeAssessments => Set<MembershipFeeAssessmentEntity>();

    public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();

    public DbSet<CredentialHistoryEntity> CredentialHistory => Set<CredentialHistoryEntity>();

    public DbSet<MemberAccountEntity> MemberAccounts => Set<MemberAccountEntity>();

    public DbSet<AccountTransactionEntity> AccountTransactions => Set<AccountTransactionEntity>();

    public DbSet<PaymentAllocationEntity> PaymentAllocations => Set<PaymentAllocationEntity>();

    public DbSet<AccountStatementEntity> AccountStatements => Set<AccountStatementEntity>();

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    public DbSet<ProductPriceEntity> ProductPrices => Set<ProductPriceEntity>();

    public DbSet<ProductCategoryEntity> ProductCategories => Set<ProductCategoryEntity>();

    public DbSet<InventoryLocationEntity> InventoryLocations => Set<InventoryLocationEntity>();

    public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();

    public DbSet<StockBalanceEntity> StockBalances => Set<StockBalanceEntity>();

    public DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();

    public DbSet<StockTransferEntity> StockTransfers => Set<StockTransferEntity>();

    public DbSet<StockTransferLineEntity> StockTransferLines => Set<StockTransferLineEntity>();

    public DbSet<StockCountEntity> StockCounts => Set<StockCountEntity>();

    public DbSet<StockCountLineEntity> StockCountLines => Set<StockCountLineEntity>();

    public DbSet<StockAdjustmentEntity> StockAdjustments => Set<StockAdjustmentEntity>();

    public DbSet<StockAdjustmentLineEntity> StockAdjustmentLines => Set<StockAdjustmentLineEntity>();

    public DbSet<PosOutletEntity> PosOutlets => Set<PosOutletEntity>();

    public DbSet<PosRegisterEntity> PosRegisters => Set<PosRegisterEntity>();

    public DbSet<CashierSessionEntity> CashierSessions => Set<CashierSessionEntity>();

    public DbSet<CashMovementEntity> CashMovements => Set<CashMovementEntity>();

    public DbSet<SaleEntity> Sales => Set<SaleEntity>();

    public DbSet<SaleLineEntity> SaleLines => Set<SaleLineEntity>();

    public DbSet<SalePaymentEntity> SalePayments => Set<SalePaymentEntity>();

    public DbSet<SaleDiscountEntity> SaleDiscounts => Set<SaleDiscountEntity>();

    public DbSet<SaleRefundEntity> SaleRefunds => Set<SaleRefundEntity>();

    public DbSet<ReceiptEntity> Receipts => Set<ReceiptEntity>();

    public DbSet<FiscalisationEntity> Fiscalisations => Set<FiscalisationEntity>();

    public DbSet<FinancialAccountEntity> FinancialAccounts => Set<FinancialAccountEntity>();

    public DbSet<FinancialPostingRuleEntity> FinancialPostingRules => Set<FinancialPostingRuleEntity>();

    public DbSet<FinancialPostingRuleMappingEntity> FinancialPostingRuleMappings => Set<FinancialPostingRuleMappingEntity>();

    public DbSet<FinancialPeriodEntity> FinancialPeriods => Set<FinancialPeriodEntity>();

    public DbSet<JournalEntity> Journals => Set<JournalEntity>();

    public DbSet<JournalLineEntity> JournalLines => Set<JournalLineEntity>();

    public DbSet<AccountsReceivableEntity> AccountsReceivable => Set<AccountsReceivableEntity>();

    public DbSet<AccountsReceivableAllocationEntity> AccountsReceivableAllocations => Set<AccountsReceivableAllocationEntity>();

    public DbSet<AccountsPayableEntity> AccountsPayable => Set<AccountsPayableEntity>();

    public DbSet<AccountsPayableAllocationEntity> AccountsPayableAllocations => Set<AccountsPayableAllocationEntity>();

    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    public DbSet<PaymentAllocationEntity> PaymentAllocationEntries => Set<PaymentAllocationEntity>();

    public DbSet<FinancialReconciliationEntity> FinancialReconciliations => Set<FinancialReconciliationEntity>();

    public DbSet<FinancialReconciliationMatchEntity> FinancialReconciliationMatches => Set<FinancialReconciliationMatchEntity>();

    public DbSet<GolfCourseEntity> GolfCourses => Set<GolfCourseEntity>();

    public DbSet<GolfHoleEntity> GolfHoles => Set<GolfHoleEntity>();

    public DbSet<GolfTeeEntity> GolfTees => Set<GolfTeeEntity>();

    public DbSet<GolfTeeSlotEntity> GolfTeeSlots => Set<GolfTeeSlotEntity>();

    public DbSet<GolfGuestRecordEntity> GolfGuestRecords => Set<GolfGuestRecordEntity>();

    public DbSet<GolfBookingEntity> GolfBookings => Set<GolfBookingEntity>();

    public DbSet<GolfBookingPlayerEntity> GolfBookingPlayers => Set<GolfBookingPlayerEntity>();

    public DbSet<GolfChargeEntity> GolfCharges => Set<GolfChargeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAuditLogs(modelBuilder);
        ConfigureReferenceSequences(modelBuilder);
        ConfigureIdempotencyRecords(modelBuilder);
        ConfigureOutboxMessages(modelBuilder);
        ConfigureClubs(modelBuilder);
        ConfigureDepartments(modelBuilder);
        ConfigureLocations(modelBuilder);
        ConfigureOutlets(modelBuilder);
        ConfigureFacilities(modelBuilder);
        ConfigurePaymentMethods(modelBuilder);
        ConfigureCurrencies(modelBuilder);
        ConfigureTaxRates(modelBuilder);
        ConfigureSystemConfigurations(modelBuilder);
        ConfigurePermissions(modelBuilder);
        ConfigureRolePermissions(modelBuilder);
        ConfigureUserSessions(modelBuilder);
        ConfigurePersons(modelBuilder);
        ConfigureMembers(modelBuilder);
        ConfigureMembershipCategories(modelBuilder);
        ConfigureMembershipApplications(modelBuilder);
        ConfigureMemberships(modelBuilder);
        ConfigureMembershipStatusHistory(modelBuilder);
        ConfigureHouseholds(modelBuilder);
        ConfigureHouseholdMembers(modelBuilder);
        ConfigureMembershipDependants(modelBuilder);
        ConfigureMembershipFees(modelBuilder);
        ConfigureMembershipFeeAssessments(modelBuilder);
        ConfigureCredentials(modelBuilder);
        ConfigureCredentialHistory(modelBuilder);
        ConfigureMemberAccounts(modelBuilder);
        ConfigureAccountTransactions(modelBuilder);
        ConfigurePaymentAllocations(modelBuilder);
        ConfigureAccountStatements(modelBuilder);
        ConfigureProducts(modelBuilder);
        ConfigureProductPrices(modelBuilder);
        ConfigureProductCategories(modelBuilder);
        ConfigureInventoryLocations(modelBuilder);
        ConfigureSuppliers(modelBuilder);
        ConfigureStockBalances(modelBuilder);
        ConfigureStockMovements(modelBuilder);
        ConfigureStockTransfers(modelBuilder);
        ConfigureStockTransferLines(modelBuilder);
        ConfigureStockCounts(modelBuilder);
        ConfigureStockCountLines(modelBuilder);
        ConfigureStockAdjustments(modelBuilder);
        ConfigureStockAdjustmentLines(modelBuilder);
        ConfigurePosOutlets(modelBuilder);
        ConfigurePosRegisters(modelBuilder);
        ConfigureCashierSessions(modelBuilder);
        ConfigureCashMovements(modelBuilder);
        ConfigureSales(modelBuilder);
        ConfigureSaleLines(modelBuilder);
        ConfigureSalePayments(modelBuilder);
        ConfigureSaleDiscounts(modelBuilder);
        ConfigureSaleRefunds(modelBuilder);
        ConfigureReceipts(modelBuilder);
        ConfigureFiscalisations(modelBuilder);

        // D6 Finance
        ConfigureFinancialAccounts(modelBuilder);
        ConfigureFinancialPostingRules(modelBuilder);
        ConfigureFinancialPostingRuleMappings(modelBuilder);
        ConfigureFinancialPeriods(modelBuilder);
        ConfigureJournals(modelBuilder);
        ConfigureJournalLines(modelBuilder);
        ConfigureAccountsReceivable(modelBuilder);
        ConfigureAccountsReceivableAllocations(modelBuilder);
        ConfigureAccountsPayable(modelBuilder);
        ConfigureAccountsPayableAllocations(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigureFinancePaymentAllocations(modelBuilder);
        ConfigureFinancialReconciliations(modelBuilder);
        ConfigureFinancialReconciliationMatches(modelBuilder);

        // D7 Golf
        ConfigureGolfCourses(modelBuilder);
        ConfigureGolfHoles(modelBuilder);
        ConfigureGolfTees(modelBuilder);
        ConfigureGolfTeeSlots(modelBuilder);
        ConfigureGolfGuestRecords(modelBuilder);
        ConfigureGolfBookings(modelBuilder);
        ConfigureGolfBookingPlayers(modelBuilder);
        ConfigureGolfCharges(modelBuilder);
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditLogEntity>();
        entity.ToTable("audit_logs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TimestampUtc).IsRequired();
        entity.Property(e => e.ActorUserId).HasMaxLength(64);
        entity.Property(e => e.ActorName).HasMaxLength(256);
        entity.Property(e => e.Action).HasMaxLength(128).IsRequired();
        entity.Property(e => e.EntityType).HasMaxLength(128);
        entity.Property(e => e.EntityId).HasMaxLength(128);
        entity.Property(e => e.Reference).HasMaxLength(128);
        entity.Property(e => e.Result).HasMaxLength(64);
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.MetadataJson);
        entity.HasIndex(e => new { e.TimestampUtc });
        entity.HasIndex(e => e.Action);
        entity.HasIndex(e => e.CorrelationId);
    }

    private static void ConfigureReferenceSequences(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ReferenceSequenceEntity>();
        entity.ToTable("reference_sequences");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Prefix).HasMaxLength(16).IsRequired();
        entity.Property(e => e.LastNumber).IsRequired();
        entity.Property(e => e.FormatWidth).IsRequired();
        entity.HasIndex(e => e.Prefix).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureIdempotencyRecords(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IdempotencyRecordEntity>();
        entity.ToTable("idempotency_records");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();
        entity.Property(e => e.Operation).HasMaxLength(128).IsRequired();
        entity.Property(e => e.ActorUserId).HasMaxLength(128).IsRequired();
        entity.Property(e => e.RequestHash).HasMaxLength(64);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.ResultJson);
        entity.Property(e => e.ErrorInfo);
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.CompletedAtUtc);
        // The key namespace is scoped to the authenticated actor and the operation,
        // so the same key can never replay an unrelated operation's result.
        entity.HasIndex(e => new { e.IdempotencyKey, e.Operation, e.ActorUserId }).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureOutboxMessages(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutboxMessageEntity>();
        entity.ToTable("outbox_messages");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.EventId).IsRequired();
        entity.Property(e => e.EventType).HasMaxLength(256).IsRequired();
        entity.Property(e => e.PayloadJson).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.RetryCount).IsRequired();
        entity.Property(e => e.ProcessedAtUtc);
        entity.Property(e => e.ErrorInfo);
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureConcurrencyToken(EntityTypeBuilder entity)
    {
        // xmin is PostgreSQL's system column; the server generates its value on
        // every insert/update. ValueGeneratedOnAddOrUpdate keeps EF Core from
        // ever writing it, only comparing/reading it back for conflict detection.
        entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid")
            .IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
    }

    private static void ConfigureClubs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ClubEntity>();
        entity.ToTable("clubs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
        entity.Property(e => e.AddressLine1).HasMaxLength(256);
        entity.Property(e => e.AddressLine2).HasMaxLength(256);
        entity.Property(e => e.City).HasMaxLength(128);
        entity.Property(e => e.Region).HasMaxLength(128);
        entity.Property(e => e.PostalCode).HasMaxLength(32);
        entity.Property(e => e.Country).HasMaxLength(64);
        entity.Property(e => e.Phone).HasMaxLength(32);
        entity.Property(e => e.Email).HasMaxLength(256);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
    }

    private static void ConfigureDepartments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DepartmentEntity>();
        entity.ToTable("departments");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => new { e.ClubId, e.Code }).IsUnique();
    }

    private static void ConfigureLocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LocationEntity>();
        entity.ToTable("locations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
        entity.Property(e => e.AddressLine1).HasMaxLength(256);
        entity.Property(e => e.AddressLine2).HasMaxLength(256);
        entity.Property(e => e.City).HasMaxLength(128);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => new { e.ClubId, e.Code }).IsUnique();
    }

    private static void ConfigureOutlets(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutletEntity>();
        entity.ToTable("outlets");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => new { e.ClubId, e.Code }).IsUnique();
    }

    private static void ConfigureFacilities(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FacilityEntity>();
        entity.ToTable("facilities");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => new { e.ClubId, e.Code }).IsUnique();
    }

    private static void ConfigurePaymentMethods(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentMethodEntity>();
        entity.ToTable("payment_methods");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Kind).HasMaxLength(24).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
    }

    private static void ConfigureCurrencies(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CurrencyEntity>();
        entity.ToTable("currencies");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Symbol).HasMaxLength(8).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
    }

    private static void ConfigureTaxRates(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TaxRateEntity>();
        entity.ToTable("tax_rates");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Rate).HasPrecision(9, 4).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Name).IsUnique();
    }

    private static void ConfigureSystemConfigurations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SystemConfigurationEntity>();
        entity.ToTable("system_configurations");
        entity.HasKey(e => e.Key);
        entity.Property(e => e.Key).HasMaxLength(128);
        entity.Property(e => e.Value).IsRequired();
        entity.Property(e => e.ValueType).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.UpdatedByUserId).HasMaxLength(64);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
    }

    private static void ConfigurePermissions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PermissionEntity>();
        entity.ToTable("permissions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
        entity.Property(e => e.Module).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Key).IsUnique();
    }

    private static void ConfigureRolePermissions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RolePermissionEntity>();
        entity.ToTable("role_permissions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
        entity.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Permission)
            .WithMany()
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureUserSessions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserSessionEntity>();
        entity.ToTable("user_sessions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UserName).HasMaxLength(256);
        entity.Property(e => e.RevocationReason).HasMaxLength(256);
        entity.Property(e => e.IssuedAtUtc).IsRequired();
        entity.Property(e => e.ExpiresAtUtc).IsRequired();
        entity.Property(e => e.LastSeenAtUtc).IsRequired();
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.UserId, e.IsTerminated });
        entity.HasIndex(e => new { e.Id, e.UserId });
    }

    private static void ConfigurePersons(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PersonEntity>();
        entity.ToTable("persons");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.LegalName).HasMaxLength(160).IsRequired();
        entity.Property(e => e.PreferredName).HasMaxLength(160);
        entity.Property(e => e.IdNumber).HasMaxLength(64);
        entity.Property(e => e.Email).HasMaxLength(256);
        entity.Property(e => e.Phone).HasMaxLength(32);
        entity.Property(e => e.AddressLine1).HasMaxLength(256);
        entity.Property(e => e.AddressLine2).HasMaxLength(256);
        entity.Property(e => e.City).HasMaxLength(128);
        entity.Property(e => e.Region).HasMaxLength(128);
        entity.Property(e => e.PostalCode).HasMaxLength(32);
        entity.Property(e => e.Country).HasMaxLength(64);
        entity.Property(e => e.Notes).HasMaxLength(1024);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => new { e.LegalName });
    }

    private static void ConfigureMembers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MemberEntity>();
        entity.ToTable("members");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.MemberNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Person)
            .WithOne(e => e.Member)
            .HasForeignKey<MemberEntity>(e => e.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => e.MemberNumber).IsUnique();
        entity.HasIndex(e => e.PersonId).IsUnique();
    }

    private static void ConfigureMembershipCategories(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipCategoryEntity>();
        entity.ToTable("membership_categories");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.EligibilityRules).HasMaxLength(1024);
        entity.Property(e => e.PrivilegeConfiguration).HasMaxLength(4096);
        entity.Property(e => e.GuestRules).HasMaxLength(1024);
        entity.Property(e => e.AccountRules).HasMaxLength(1024);
        entity.Property(e => e.CredentialRules).HasMaxLength(1024);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
    }

    private static void ConfigureMembershipApplications(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipApplicationEntity>();
        entity.ToTable("membership_applications");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.SponsorName).HasMaxLength(160);
        entity.Property(e => e.ReferrerName).HasMaxLength(160);
        entity.Property(e => e.Notes).HasMaxLength(2048);
        entity.Property(e => e.RequiredDocuments).HasMaxLength(2048);
        entity.Property(e => e.ApproverUserId).HasMaxLength(64);
        entity.Property(e => e.RejectionReason).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Person)
            .WithMany()
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => new { e.PersonId, e.CategoryId });
    }

    private static void ConfigureMemberships(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipEntity>();
        entity.ToTable("memberships");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.ApprovedByUserId).HasMaxLength(64);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Member)
            .WithMany(e => e.Memberships)
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Category)
            .WithMany(e => e.Memberships)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PreviousMembership)
            .WithMany()
            .HasForeignKey(e => e.PreviousMembershipId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.MemberId });
        entity.HasIndex(e => e.Status);
    }

    private static void ConfigureMembershipStatusHistory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipStatusHistoryEntity>();
        entity.ToTable("membership_status_history");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ChangedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(512).IsRequired();
        entity.Property(e => e.ChangedAtUtc).IsRequired();
        entity.HasOne(e => e.Membership)
            .WithMany(e => e.StatusHistory)
            .HasForeignKey(e => e.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.MembershipId, e.ChangedAtUtc });
    }

    private static void ConfigureHouseholds(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HouseholdEntity>();
        entity.ToTable("households");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Reference).HasMaxLength(32).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.PrimaryMember)
            .WithMany()
            .HasForeignKey(e => e.PrimaryMemberId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(e => e.Reference).IsUnique();
    }

    private static void ConfigureHouseholdMembers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HouseholdMemberEntity>();
        entity.ToTable("household_members");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Role).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Household)
            .WithMany(e => e.Members)
            .HasForeignKey(e => e.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Person)
            .WithMany()
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.HouseholdId, e.PersonId }).IsUnique();
    }

    private static void ConfigureMembershipDependants(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipDependantEntity>();
        entity.ToTable("membership_dependants");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Notes).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.DependantPerson)
            .WithMany()
            .HasForeignKey(e => e.DependantPersonId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.MemberId, e.DependantPersonId }).IsUnique();
    }

    private static void ConfigureMembershipFees(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipFeeEntity>();
        entity.ToTable("membership_fees");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Description).HasMaxLength(256);
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Category)
            .WithMany(e => e.Fees)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.CategoryId, e.FeeType }).IsUnique();
    }

    private static void ConfigureMembershipFeeAssessments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MembershipFeeAssessmentEntity>();
        entity.ToTable("membership_fee_assessments");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Description).HasMaxLength(256);
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Source).HasMaxLength(256);
        entity.Property(e => e.AdjustmentReason).HasMaxLength(512);
        entity.Property(e => e.VoidReason).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Membership)
            .WithMany(e => e.FeeAssessments)
            .HasForeignKey(e => e.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.MembershipId });
        entity.HasIndex(e => e.Status);
    }

    private static void ConfigureCredentials(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CredentialEntity>();
        entity.ToTable("credentials");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CredentialNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.CredentialType).IsRequired();
        entity.Property(e => e.CredentialValue).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReplacedByCredential)
            .WithMany()
            .HasForeignKey(e => e.ReplacedByCredentialId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.CredentialNumber).IsUnique();
        entity.HasIndex(e => e.MemberId);
        entity.HasIndex(e => e.Status);
        // A credential value must resolve to exactly one active credential
        // (Phase 4 database rule "unique active credentials", CRED-003).
        entity.HasIndex(e => e.CredentialValue)
            .IsUnique()
            .HasFilter("\"Status\" = 1");
    }

    private static void ConfigureCredentialHistory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CredentialHistoryEntity>();
        entity.ToTable("credential_history");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PreviousState).IsRequired();
        entity.Property(e => e.NewState).IsRequired();
        entity.Property(e => e.ChangedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(512).IsRequired();
        entity.Property(e => e.ChangedAtUtc).IsRequired();
        entity.HasOne(e => e.Credential)
            .WithMany(e => e.History)
            .HasForeignKey(e => e.CredentialId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.CredentialId, e.ChangedAtUtc });
    }

    private static void ConfigureMemberAccounts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MemberAccountEntity>();
        entity.ToTable("member_accounts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AccountNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.OpenedAtUtc).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.MemberId).IsUnique();
        entity.HasIndex(e => e.AccountNumber).IsUnique();
    }

    private static void ConfigureAccountTransactions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountTransactionEntity>();
        entity.ToTable("account_transactions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TransactionType).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(256).IsRequired();
        entity.Property(e => e.Reference).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ExternalReference).HasMaxLength(128);
        entity.Property(e => e.Reason).HasMaxLength(512);
        entity.Property(e => e.SourceType).HasMaxLength(64);
        entity.Property(e => e.TransactionDateUtc).IsRequired();
        entity.Property(e => e.BusinessDateUtc).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Account)
            .WithMany(e => e.Transactions)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ReverseOfTransaction)
            .WithMany()
            .HasForeignKey(e => e.ReverseOfTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PaymentMethod)
            .WithMany()
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(e => new { e.AccountId, e.BusinessDateUtc });
        entity.HasIndex(e => new { e.AccountId, e.Status });
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => e.ReverseOfTransactionId);
    }

    private static void ConfigurePaymentAllocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentAllocationEntity>();
        entity.ToTable("payment_allocations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AllocatedAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.FromTransaction)
            .WithMany(e => e.AllocationsSent)
            .HasForeignKey(e => e.FromTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ToChargeTransaction)
            .WithMany(e => e.AllocationsReceived)
            .HasForeignKey(e => e.ToChargeTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.FromTransactionId, e.ToChargeTransactionId });
    }

    private static void ConfigureAccountStatements(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountStatementEntity>();
        entity.ToTable("account_statements");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.StatementNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.OpeningBalance).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.ClosingBalance).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.GeneratedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.GeneratedAtUtc).IsRequired();
        entity.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => e.StatementNumber).IsUnique();
        entity.HasIndex(e => new { e.AccountId, e.GeneratedAtUtc });
    }

    private static void ConfigureProducts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductEntity>();
        entity.ToTable("products");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Sku).HasMaxLength(32).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.UnitOfMeasure).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Barcode).HasMaxLength(64);
        entity.Property(e => e.StockManaged).IsRequired();
        entity.Property(e => e.Cost).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.ReorderLevel).HasPrecision(19, 4);
        entity.Property(e => e.TaxTreatment).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Category)
            .WithMany(e => e.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.TaxRate)
            .WithMany()
            .HasForeignKey(e => e.TaxRateId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(e => e.Sku).IsUnique();
        entity.HasIndex(e => e.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        entity.HasIndex(e => new { e.Status });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureProductPrices(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductPriceEntity>();
        entity.ToTable("product_prices");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PriceType).HasMaxLength(24).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.EffectiveFromUtc).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Product)
            .WithMany(e => e.Prices)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.ProductId, e.EffectiveFromUtc });
    }

    private static void ConfigureProductCategories(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductCategoryEntity>();
        entity.ToTable("product_categories");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.IsActive).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureInventoryLocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InventoryLocationEntity>();
        entity.ToTable("inventory_locations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Type).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureSuppliers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SupplierEntity>();
        entity.ToTable("suppliers");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SupplierCode).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.ContactPerson).HasMaxLength(160);
        entity.Property(e => e.Email).HasMaxLength(256);
        entity.Property(e => e.Phone).HasMaxLength(32);
        entity.Property(e => e.PaymentTerms).HasMaxLength(128);
        entity.Property(e => e.TaxId).HasMaxLength(64);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.SupplierCode).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureStockBalances(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockBalanceEntity>();
        entity.ToTable("stock_balances");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Quantity).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        // One cached position per (product, location); the ledger stays authoritative.
        entity.HasIndex(e => new { e.ProductId, e.LocationId }).IsUnique();
        entity.HasIndex(e => new { e.LocationId });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureStockMovements(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockMovementEntity>();
        entity.ToTable("stock_movements");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Quantity).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.MovementType).IsRequired();
        entity.Property(e => e.UnitCost).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.BusinessDateUtc).IsRequired();
        entity.Property(e => e.SourceType).HasMaxLength(64);
        entity.Property(e => e.Reason).HasMaxLength(512);
        entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => new { e.ProductId, e.LocationId, e.BusinessDateUtc });
        entity.HasIndex(e => new { e.LocationId, e.BusinessDateUtc });
        entity.HasIndex(e => e.MovementType);
        entity.HasIndex(e => e.SourceId);
    }

    private static void ConfigureStockTransfers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockTransferEntity>();
        entity.ToTable("stock_transfers");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TransferNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.FromLocation)
            .WithMany()
            .HasForeignKey(e => e.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ToLocation)
            .WithMany()
            .HasForeignKey(e => e.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.TransferNumber).IsUnique();
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureStockTransferLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockTransferLineEntity>();
        entity.ToTable("stock_transfer_lines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Quantity).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.UnitCost).HasPrecision(19, 4).IsRequired();
        entity.HasOne(e => e.Transfer)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.TransferId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.TransferId });
    }

    private static void ConfigureStockCounts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockCountEntity>();
        entity.ToTable("stock_counts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CountNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.StartedAtUtc).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.CountNumber).IsUnique();
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureStockCountLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockCountLineEntity>();
        entity.ToTable("stock_count_lines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ExpectedQuantity).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CountedQuantity).HasPrecision(19, 4);
        entity.Property(e => e.Variance).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Count)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.CountId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.CountId });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureStockAdjustments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockAdjustmentEntity>();
        entity.ToTable("stock_adjustments");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AdjustmentNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.AdjustmentType).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(512).IsRequired();
        entity.Property(e => e.BusinessDateUtc).IsRequired();
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.PostedAtUtc).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.AdjustmentNumber).IsUnique();
        entity.HasIndex(e => e.AdjustmentType);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureStockAdjustmentLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockAdjustmentLineEntity>();
        entity.ToTable("stock_adjustment_lines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Quantity).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.UnitCost).HasPrecision(19, 4).IsRequired();
        entity.HasOne(e => e.Adjustment)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.AdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.AdjustmentId });
    }

    private static void ConfigurePosOutlets(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PosOutletEntity>();
        entity.ToTable("pos_outlets");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Notes).HasMaxLength(512);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigurePosRegisters(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PosRegisterEntity>();
        entity.ToTable("pos_registers");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.PosOutlet)
            .WithMany(e => e.Registers)
            .HasForeignKey(e => e.PosOutletId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.InventoryLocation)
            .WithMany()
            .HasForeignKey(e => e.InventoryLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.PosOutletId, e.Code }).IsUnique();
        entity.HasIndex(e => e.Code).IsUnique();
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureCashierSessions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CashierSessionEntity>();
        entity.ToTable("cashier_sessions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SessionNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.CashierUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.OpeningFloat).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.DeclaredCash).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.ExpectedCash).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Variance).HasPrecision(19, 4);
        entity.Property(e => e.VarianceApproved).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(512);
        entity.Property(e => e.OpenedAtUtc).IsRequired();
        entity.Property(e => e.ReconciledByUserId).HasMaxLength(64);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Register)
            .WithMany()
            .HasForeignKey(e => e.RegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.SessionNumber).IsUnique();
        entity.HasIndex(e => new { e.RegisterId, e.Status });
        // A register may only have one unfinished session. This is enforced in the
        // database so two concurrent opens cannot both pass the application check.
        entity.HasIndex(e => e.RegisterId)
            .IsUnique()
            .HasFilter("\"Status\" IN (0, 1, 2)");
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureCashMovements(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CashMovementEntity>();
        entity.ToTable("cash_movements");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.MovementType).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.SignedAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(512);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Session)
            .WithMany(e => e.Movements)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.SessionId, e.CreatedAtUtc });
    }

    private static void ConfigureSales(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SaleEntity>();
        entity.ToTable("sales");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SaleNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CashierUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.MemberNumber).HasMaxLength(16);
        entity.Property(e => e.MemberName).HasMaxLength(160);
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Subtotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.DiscountTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TaxTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.GrandTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(1024);
        entity.Property(e => e.BusinessDateUtc).IsRequired();
        entity.Property(e => e.VoidReason).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Register)
            .WithMany()
            .HasForeignKey(e => e.RegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CashierSession)
            .WithMany(e => e.Sales)
            .HasForeignKey(e => e.CashierSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PosOutlet)
            .WithMany()
            .HasForeignKey(e => e.PosOutletId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.SaleNumber).IsUnique();
        entity.HasIndex(e => new { e.RegisterId, e.BusinessDateUtc });
        entity.HasIndex(e => new { e.MemberId, e.Status });
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureSaleLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SaleLineEntity>();
        entity.ToTable("sale_lines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ProductSku).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ProductName).HasMaxLength(160).IsRequired();
        entity.Property(e => e.StockManaged).IsRequired();
        entity.Property(e => e.Quantity).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.OriginalUnitPrice).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.UnitPrice).HasPrecision(19, 4).IsRequired();
        // A line without a discount stores NULL, so this must stay optional.
        entity.Property(e => e.DiscountType);
        entity.Property(e => e.DiscountValue).HasPrecision(19, 4);
        entity.Property(e => e.DiscountReason).HasMaxLength(512);
        entity.Property(e => e.DiscountAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TaxableBase).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TaxAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.LineTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TaxTreatment).IsRequired();
        entity.Property(e => e.TaxRateName).HasMaxLength(80);
        entity.Property(e => e.TaxRate).HasPrecision(9, 4);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.SaleId });
    }

    private static void ConfigureSalePayments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalePaymentEntity>();
        entity.ToTable("sale_payments");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PaymentMethodCode).HasMaxLength(24).IsRequired();
        entity.Property(e => e.PaymentMethodName).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Kind).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.IsRefund).IsRequired();
        entity.Property(e => e.RefundedAmount).HasPrecision(19, 4);
        entity.Property(e => e.ExternalReference).HasMaxLength(128);
        entity.Property(e => e.AccountTransactionReference).HasMaxLength(32);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Payments)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.CashierSession)
            .WithMany()
            .HasForeignKey(e => e.CashierSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PaymentMethod)
            .WithMany()
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.AccountTransaction)
            .WithMany()
            .HasForeignKey(e => e.AccountTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.SaleId });
        entity.HasIndex(e => new { e.PaymentMethodCode });
        entity.HasIndex(e => new { e.CashierSessionId, e.IsRefund });
    }

    private static void ConfigureSaleDiscounts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SaleDiscountEntity>();
        entity.ToTable("sale_discounts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Level).HasMaxLength(16).IsRequired();
        entity.Property(e => e.DiscountType).IsRequired();
        entity.Property(e => e.Value).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(512);
        entity.Property(e => e.AuthorizedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.AppliedAtUtc).IsRequired();
        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Discounts)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Line)
            .WithMany()
            .HasForeignKey(e => e.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.SaleId });
    }

    private static void ConfigureSaleRefunds(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SaleRefundEntity>();
        entity.ToTable("sale_refunds");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.RefundNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.SaleNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).HasMaxLength(24).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(512).IsRequired();
        entity.Property(e => e.RefundedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.RefundedAtUtc).IsRequired();
        entity.Property(e => e.LinesJson).IsRequired();
        entity.Property(e => e.PaymentsJson).IsRequired();
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Refunds)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.RefundNumber).IsUnique();
        entity.HasIndex(e => new { e.SaleId });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureReceipts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ReceiptEntity>();
        entity.ToTable("receipts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ReceiptNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.SaleNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).HasMaxLength(24).IsRequired();
        entity.Property(e => e.RegisterCode).HasMaxLength(24).IsRequired();
        entity.Property(e => e.PosOutletCode).HasMaxLength(24).IsRequired();
        entity.Property(e => e.CashierUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.MemberNumber).HasMaxLength(16);
        entity.Property(e => e.MemberName).HasMaxLength(160);
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Subtotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.DiscountTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TaxTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.GrandTotal).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.BusinessDateUtc).IsRequired();
        entity.Property(e => e.GeneratedAtUtc).IsRequired();
        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Receipts)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.ReceiptNumber).IsUnique();
        entity.HasIndex(e => new { e.SaleId });
    }

    private static void ConfigureFiscalisations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FiscalisationEntity>();
        entity.ToTable("fiscalisations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SaleNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.RefundNumber).HasMaxLength(16);
        entity.Property(e => e.InvoiceNumber).HasMaxLength(32).IsRequired();
        entity.Property(e => e.DocumentKind).HasMaxLength(24).IsRequired();
        entity.Property(e => e.OriginalInvoiceNumber).HasMaxLength(32);
        entity.Property(e => e.Status).HasMaxLength(24).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(64);
        entity.Property(e => e.Message).HasMaxLength(1024);
        entity.Property(e => e.ErrorInfo).HasMaxLength(2048);
        entity.Property(e => e.DeviceId).HasMaxLength(64);
        entity.Property(e => e.DeviceSerialNumber).HasMaxLength(64);
        entity.Property(e => e.FiscalDayNo).HasMaxLength(24);
        entity.Property(e => e.ReceiptCounter).HasMaxLength(24);
        entity.Property(e => e.ReceiptGlobalNo).HasMaxLength(24);
        entity.Property(e => e.ReceiptId).HasMaxLength(128);
        entity.Property(e => e.OperationId).HasMaxLength(64);
        entity.Property(e => e.DeviceSignature).HasMaxLength(512);
        entity.Property(e => e.ServerSignature).HasMaxLength(512);
        entity.Property(e => e.QrUrl).HasMaxLength(512);
        entity.Property(e => e.PaymentMapping).HasMaxLength(64);
        entity.Property(e => e.TendersJson).IsRequired();
        entity.Property(e => e.ValidationWarningsJson).IsRequired();
        entity.Property(e => e.PendingSync).IsRequired();
        entity.Property(e => e.AttemptCount).IsRequired();
        entity.Property(e => e.CorrelationId).HasMaxLength(64);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Sale)
            .WithMany()
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        // ZIMRA requires the invoice number to be unique in the taxpayer context, so
        // uniqueness is enforced here rather than trusted from the caller. This is
        // also the database-level guard against a retry creating a second document.
        entity.HasIndex(e => e.InvoiceNumber).IsUnique();

        // Exactly one fiscal invoice per sale; credit notes carry a RefundId and are
        // therefore excluded from this filtered index.
        entity.HasIndex(e => e.SaleId)
            .IsUnique()
            .HasFilter("\"RefundId\" IS NULL");

        // D5.5 finalisation: a credit note must point at a real refund, and the
        // one-credit-note-per-refund invariant is enforced by the database rather than
        // only by the application. Restrict on delete so a fiscal record — the durable
        // statement that a document is owed to ZIMRA — can never be orphaned by removing
        // the refund, consistent with the Sale relationship above and with SYS-010
        // (P2:108-110) preferring inactivation over destructive deletion.
        entity.HasOne(e => e.Refund)
            .WithMany()
            .HasForeignKey(e => e.RefundId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered because invoice rows leave RefundId NULL: without the filter a single
        // unique index on RefundId would collide on the second invoice. The filter admits
        // only credit notes, so a duplicate credit note for one refund is rejected.
        entity.HasIndex(e => e.RefundId)
            .IsUnique()
            .HasFilter("\"RefundId\" IS NOT NULL");

        entity.HasIndex(e => new { e.SaleId, e.RefundId });
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.FiscalDayNo);
        ConfigureConcurrencyToken(entity);
    }

    // -------------------------------------------------------------------------
    // D6 Finance
    // -------------------------------------------------------------------------

    private static void ConfigureFinancialAccounts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialAccountEntity>();
        entity.ToTable("financial_accounts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(32).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.AccountType).IsRequired();
        entity.Property(e => e.NormalBalance).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CashKind).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.ParentAccount)
            .WithMany(e => e.ChildAccounts)
            .HasForeignKey(e => e.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => e.AccountType);
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureFinancialPostingRules(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialPostingRuleEntity>();
        entity.ToTable("financial_posting_rules");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.SourceSection).HasMaxLength(64).IsRequired();
        entity.Property(e => e.PostingStrategy).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(1024);
        entity.Property(e => e.IsActive).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
    }

    private static void ConfigureFinancialPostingRuleMappings(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialPostingRuleMappingEntity>();
        entity.ToTable("financial_posting_rule_mappings");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Sequence).IsRequired();
        entity.Property(e => e.Direction).IsRequired();
        entity.Property(e => e.Selector).HasMaxLength(64);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Rule)
            .WithMany(e => e.Mappings)
            .HasForeignKey(e => e.RuleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.FinancialAccount)
            .WithMany()
            .HasForeignKey(e => e.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        // A rule resolves its lines in sequence order, so a sequence is unique per rule.
        entity.HasIndex(e => new { e.RuleId, e.Sequence }).IsUnique();
        entity.HasIndex(e => e.FinancialAccountId);
    }

    private static void ConfigureFinancialPeriods(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialPeriodEntity>();
        entity.ToTable("financial_periods");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.OpenedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.ClosedByUserId).HasMaxLength(64);
        entity.Property(e => e.ReopenedByUserId).HasMaxLength(64);
        entity.Property(e => e.ReopenReason).HasMaxLength(512);
        entity.Property(e => e.ReopenCount).IsRequired();
        entity.Property(e => e.StartUtc).IsRequired();
        entity.Property(e => e.EndUtc).IsRequired();
        entity.Property(e => e.OpenedAtUtc).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        // B6: the period is resolved from the journal TransactionDate, so (Year, Month)
        // identifies exactly one period.
        entity.HasIndex(e => new { e.Year, e.Month }).IsUnique();
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureJournals(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<JournalEntity>();
        entity.ToTable("journals");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512).IsRequired();
        entity.Property(e => e.TransactionDate).IsRequired();
        entity.Property(e => e.BusinessDate).IsRequired();
        entity.Property(e => e.TransactionCurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.BaseCurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.ExchangeRate).HasPrecision(19, 6).IsRequired();
        entity.Property(e => e.RateDateUtc).IsRequired();
        entity.Property(e => e.SourceType).HasMaxLength(64).IsRequired();
        entity.Property(e => e.SourceReference).HasMaxLength(128);
        entity.Property(e => e.PostedByUserId).HasMaxLength(64);
        entity.Property(e => e.CancelledReason).HasMaxLength(512);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.FinancialPeriod)
            .WithMany(e => e.Journals)
            .HasForeignKey(e => e.FinancialPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        // B4: a reversal points at the journal it compensates; the original is never
        // modified, so there is no reversed-by column to keep in sync.
        entity.HasOne(e => e.ReversalOfJournal)
            .WithMany()
            .HasForeignKey(e => e.ReversalOfJournalId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Receivable)
            .WithMany()
            .HasForeignKey(e => e.ReceivableId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Payable)
            .WithMany()
            .HasForeignKey(e => e.PayableId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        // Phase 8 §15: the same source transaction cannot produce duplicate financial
        // postings. Enforced by the database, not only by the posting engine.
        entity.HasIndex(e => new { e.SourceType, e.SourceId }).IsUnique();
        entity.HasIndex(e => new { e.FinancialPeriodId, e.Status });
        entity.HasIndex(e => e.ReversalOfJournalId);
        entity.HasIndex(e => e.TransactionDate);
        entity.HasIndex(e => e.ReceivableId);
        entity.HasIndex(e => e.PayableId);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureJournalLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<JournalLineEntity>();
        entity.ToTable("journal_lines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.LineNumber).IsRequired();
        entity.Property(e => e.Direction).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.BaseAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Journal)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.JournalId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.FinancialAccount)
            .WithMany(e => e.JournalLines)
            .HasForeignKey(e => e.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        // B4/B8: a reversal line points at the line it compensates, and the database
        // must not allow it to point at nothing.
        entity.HasOne(e => e.SourceLine)
            .WithMany()
            .HasForeignKey(e => e.SourceLineId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.JournalId, e.LineNumber }).IsUnique();
        entity.HasIndex(e => e.FinancialAccountId);
        entity.HasIndex(e => e.SourceLineId);
    }

    private static void ConfigureAccountsReceivable(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountsReceivableEntity>();
        entity.ToTable("accounts_receivable");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.SourceType).HasMaxLength(64).IsRequired();
        entity.Property(e => e.SourceReference).HasMaxLength(128);
        entity.Property(e => e.OriginalAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.AllocatedAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.OutstandingAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TransactionDate).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CancelledReason).HasMaxLength(512);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.MemberAccount)
            .WithMany()
            .HasForeignKey(e => e.MemberAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Journal)
            .WithMany()
            .HasForeignKey(e => e.JournalId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => new { e.SourceType, e.SourceId }).IsUnique();
        entity.HasIndex(e => e.MemberId);
        entity.HasIndex(e => new { e.Status, e.DueDate });
        entity.HasIndex(e => e.JournalId);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureAccountsReceivableAllocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountsReceivableAllocationEntity>();
        entity.ToTable("accounts_receivable_allocations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.AllocatedAtUtc).IsRequired();
        entity.Property(e => e.AllocatedByUserId).HasMaxLength(64).IsRequired();
        entity.HasOne(e => e.Receivable)
            .WithMany(e => e.Allocations)
            .HasForeignKey(e => e.ReceivableId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Payment)
            .WithMany()
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        // A payment settles a receivable at most once; duplicate allocation rows for the
        // same pair are rejected by the database.
        entity.HasIndex(e => new { e.PaymentId, e.ReceivableId }).IsUnique();
    }

    private static void ConfigureAccountsPayable(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountsPayableEntity>();
        entity.ToTable("accounts_payable");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.SourceType).HasMaxLength(64).IsRequired();
        entity.Property(e => e.SourceReference).HasMaxLength(128);
        entity.Property(e => e.OriginalAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.AllocatedAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.OutstandingAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.TransactionDate).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CancelledReason).HasMaxLength(512);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Journal)
            .WithMany()
            .HasForeignKey(e => e.JournalId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => new { e.SourceType, e.SourceId }).IsUnique();
        entity.HasIndex(e => e.SupplierId);
        entity.HasIndex(e => new { e.Status, e.DueDate });
        entity.HasIndex(e => e.JournalId);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureAccountsPayableAllocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountsPayableAllocationEntity>();
        entity.ToTable("accounts_payable_allocations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.AllocatedAtUtc).IsRequired();
        entity.Property(e => e.AllocatedByUserId).HasMaxLength(64).IsRequired();
        entity.HasOne(e => e.Payable)
            .WithMany(e => e.Allocations)
            .HasForeignKey(e => e.PayableId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Payment)
            .WithMany()
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.PaymentId, e.PayableId }).IsUnique();
    }

    private static void ConfigurePayments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentEntity>();
        entity.ToTable("payments");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Direction).IsRequired();
        entity.Property(e => e.SourceType).HasMaxLength(64);
        entity.Property(e => e.SourceReference).HasMaxLength(128);
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.UnappliedAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.TransactionDate).IsRequired();
        entity.Property(e => e.PaymentMethodCode).HasMaxLength(24);
        entity.Property(e => e.ExternalReference).HasMaxLength(128);
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(128);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.MemberAccount)
            .WithMany()
            .HasForeignKey(e => e.MemberAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Journal)
            .WithMany()
            .HasForeignKey(e => e.JournalId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        // Idempotency: a caller-supplied key replays the stored payment instead of
        // creating a second one. Filtered because manual payments have no key.
        entity.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        // A source transaction produces at most one finance payment.
        entity.HasIndex(e => new { e.SourceType, e.SourceId })
            .IsUnique()
            .HasFilter("\"SourceId\" IS NOT NULL");
        entity.HasIndex(e => e.MemberId);
        entity.HasIndex(e => e.SupplierId);
        entity.HasIndex(e => e.TransactionDate);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureFinancePaymentAllocations(ModelBuilder modelBuilder)
    {
        // D6 allocation table. Deliberately distinct from the D3
        // <c>payment_allocations</c> table, which settles a member account charge from
        // a D3 account transaction; this one allocates a finance payment to a D6
        // receivable or payable.
        var entity = modelBuilder.Entity<FinancePaymentAllocationEntity>();
        entity.ToTable("finance_payment_allocations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Payment)
            .WithMany(e => e.Allocations)
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Receivable)
            .WithMany()
            .HasForeignKey(e => e.ReceivableId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Payable)
            .WithMany()
            .HasForeignKey(e => e.PayableId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => new { e.PaymentId, e.ReceivableId });
        entity.HasIndex(e => new { e.PaymentId, e.PayableId });
    }

    private static void ConfigureFinancialReconciliations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialReconciliationEntity>();
        entity.ToTable("financial_reconciliations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.StatementDateUtc).IsRequired();
        entity.Property(e => e.StatementClosingBalance).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.LedgerClosingBalance).HasPrecision(19, 4);
        entity.Property(e => e.Variance).HasPrecision(19, 4);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(512);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CompletedByUserId).HasMaxLength(64);
        entity.Property(e => e.CancelledReason).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.FinancialAccount)
            .WithMany()
            .HasForeignKey(e => e.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => new { e.FinancialAccountId, e.StatementDateUtc });
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureFinancialReconciliationMatches(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialReconciliationMatchEntity>();
        entity.ToTable("financial_reconciliation_matches");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.LedgerAmount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(512);
        entity.Property(e => e.MatchedAtUtc).IsRequired();
        entity.Property(e => e.MatchedByUserId).HasMaxLength(64).IsRequired();
        entity.HasOne(e => e.Reconciliation)
            .WithMany(e => e.Matches)
            .HasForeignKey(e => e.ReconciliationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Journal)
            .WithMany()
            .HasForeignKey(e => e.JournalId)
            .OnDelete(DeleteBehavior.Restrict);
        // B15: a journal is matched into a reconciliation once, so the same entry cannot
        // be counted twice against one statement.
        entity.HasIndex(e => new { e.ReconciliationId, e.JournalId }).IsUnique();
    }

    // -----------------------------------------------------------------------
    // D7 Golf
    // -----------------------------------------------------------------------

    private static void ConfigureGolfCourses(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfCourseEntity>();
        entity.ToTable("golf_courses");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(32).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.NumberOfHoles).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        // Phase 3 §40 Code identifies a course.
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.Status);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureGolfHoles(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfHoleEntity>();
        entity.ToTable("golf_holes");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.HoleNumber).IsRequired();
        entity.Property(e => e.Par).IsRequired();
        entity.Property(e => e.Yardage);
        entity.Property(e => e.Name).HasMaxLength(80);
        entity.HasOne(e => e.Course)
            .WithMany(e => e.Holes)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
        // A hole number identifies a hole within its course.
        entity.HasIndex(e => new { e.CourseId, e.HoleNumber }).IsUnique();
    }

    private static void ConfigureGolfTees(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfTeeEntity>();
        entity.ToTable("golf_tees");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(32).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Capacity).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.MembersOnly).IsRequired();
        entity.Property(e => e.GuestsAllowed).IsRequired();
        entity.Property(e => e.MinimumPlayers);
        entity.Property(e => e.MaximumPlayers);
        entity.Property(e => e.Notes).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Course)
            .WithMany(e => e.Tees)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
        // GOLF-016: the green and guest fees are D4 catalogue products, never a
        // golf-specific price. No amount is stored on the tee.
        entity.HasOne(e => e.GreenFeeProduct)
            .WithMany()
            .HasForeignKey(e => e.GreenFeeProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.GuestFeeProduct)
            .WithMany()
            .HasForeignKey(e => e.GuestFeeProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => new { e.CourseId, e.Status });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureGolfTeeSlots(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfTeeSlotEntity>();
        entity.ToTable("tee_slots");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Date).HasColumnType("date").IsRequired();
        entity.Property(e => e.StartTime).HasColumnType("time").IsRequired();
        entity.Property(e => e.DurationMinutes).IsRequired();
        entity.Property(e => e.Capacity).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.MaximumPlayers);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.HasOne(e => e.Course)
            .WithMany(e => e.TeeSlots)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Tee)
            .WithMany(e => e.TeeSlots)
            .HasForeignKey(e => e.TeeId)
            .OnDelete(DeleteBehavior.Restrict);
        // A tee cannot host two slots at the same start time, so a double-booked
        // slot cannot be created by configuration.
        entity.HasIndex(e => new { e.TeeId, e.Date, e.StartTime }).IsUnique();
        entity.HasIndex(e => new { e.Date, e.CourseId });
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureGolfGuestRecords(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfGuestRecordEntity>();
        entity.ToTable("golf_guest_records");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.FullName).HasMaxLength(160).IsRequired();
        entity.Property(e => e.ContactDetails).HasMaxLength(256);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.VisitCount).IsRequired();
        entity.Property(e => e.LastVisitedAtUtc);
        entity.Property(e => e.Notes).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        // Phase 3 §45: a guest is not a member. The only member link is the host.
        entity.HasOne(e => e.HostMember)
            .WithMany()
            .HasForeignKey(e => e.HostMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => e.FullName);
        entity.HasIndex(e => e.HostMemberId);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureGolfBookings(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfBookingEntity>();
        entity.ToTable("golf_bookings");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reference).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.CancellationReason).HasMaxLength(512);
        entity.Property(e => e.OverrideReason).HasMaxLength(512);
        entity.Property(e => e.OverrideByUserId).HasMaxLength(64);
        entity.Property(e => e.OverriddenAtUtc);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.Property(e => e.ConfirmedAtUtc);
        entity.Property(e => e.CheckedInAtUtc);
        entity.Property(e => e.CompletedAtUtc);
        entity.Property(e => e.CancelledAtUtc);
        entity.Property(e => e.NoShowAtUtc);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CorrelationId).HasMaxLength(64).IsRequired();
        // Phase 3 §79: a booking cannot reference a nonexistent tee slot.
        entity.HasOne(e => e.TeeSlot)
            .WithMany(e => e.Bookings)
            .HasForeignKey(e => e.TeeSlotId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.BookedByMember)
            .WithMany()
            .HasForeignKey(e => e.BookedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.Reference).IsUnique();
        entity.HasIndex(e => new { e.TeeSlotId, e.Status });
        entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        entity.HasIndex(e => e.BookedByMemberId);
        ConfigureConcurrencyToken(entity);
    }

    private static void ConfigureGolfBookingPlayers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfBookingPlayerEntity>();
        entity.ToTable("golf_booking_players");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PlayerType).IsRequired();
        entity.Property(e => e.Fee).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.Position).IsRequired();
        entity.HasOne(e => e.Booking)
            .WithMany(e => e.Players)
            .HasForeignKey(e => e.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.GolfGuest)
            .WithMany(e => e.BookingPlayers)
            .HasForeignKey(e => e.GolfGuestId)
            .OnDelete(DeleteBehavior.Restrict);
        // A member cannot be entered twice on the same booking, and a guest cannot
        // be entered twice either. PostgreSQL treats NULLs as distinct in a unique
        // index, so these two together enforce the member/guest split that Phase 3
        // §44 describes without needing partial indexes.
        entity.HasIndex(e => new { e.BookingId, e.MemberId }).IsUnique();
        entity.HasIndex(e => new { e.BookingId, e.GolfGuestId }).IsUnique();
        entity.HasIndex(e => new { e.BookingId, e.Position }).IsUnique();
    }

    private static void ConfigureGolfCharges(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GolfChargeEntity>();
        entity.ToTable("golf_charges");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ChargeType).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(256).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
        entity.Property(e => e.CurrencyCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.WaivedReason).HasMaxLength(512);
        entity.Property(e => e.CreatedAtUtc).IsRequired();
        entity.HasOne(e => e.Booking)
            .WithMany(e => e.Charges)
            .HasForeignKey(e => e.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.BookingPlayer)
            .WithMany()
            .HasForeignKey(e => e.BookingPlayerId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        // A player's green fee is raised once. The partial unique index is added in
        // the D7 migration, where PostgreSQL's partial-index support is available.
        entity.HasIndex(e => new { e.BookingId, e.ChargeType, e.BookingPlayerId });
        entity.HasIndex(e => e.Status);
    }
}