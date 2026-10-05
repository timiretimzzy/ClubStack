using ClubStack.Application.Authorization;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Identity;

public interface IReferenceDataSeeder
{
    /// <summary>
    /// Seeds system/started reference data (Phase 9 §42): the permission
    /// catalogue, the baseline roles, starter payment methods and starter
    /// currencies. Idempotent and non-destructive of administrator edits.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ReferenceDataSeeder : IReferenceDataSeeder
{
    private readonly ClubStackDbContext _context;
    private readonly RoleManager<ClubStackRole> _roleManager;

    public ReferenceDataSeeder(ClubStackDbContext context, RoleManager<ClubStackRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var permissionIds = await SeedPermissionsAsync(cancellationToken);
        await SeedStarterPaymentMethodsAsync(cancellationToken);
        await SeedStarterCurrenciesAsync(cancellationToken);
        await SeedBaselineRolesAsync(permissionIds, cancellationToken);
    }

    private async Task<Dictionary<string, Guid>> SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Permissions
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Key, p => p.Id, StringComparer.Ordinal, cancellationToken);

        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var descriptor in Permissions.Catalog)
        {
            if (existing.TryGetValue(descriptor.Key, out var id))
            {
                result[descriptor.Key] = id;
                continue;
            }

            var entity = new PermissionEntity
            {
                Key = descriptor.Key,
                Module = descriptor.Module,
                Description = descriptor.Description,
                IsSensitive = descriptor.IsSensitive,
                IsSystem = true,
            };
            _context.Permissions.Add(entity);
            result[descriptor.Key] = entity.Id;
        }

        if (result.Count > 0 || _context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async Task SeedStarterPaymentMethodsAsync(CancellationToken cancellationToken)
    {
        if (await _context.PaymentMethods.AnyAsync(cancellationToken: cancellationToken))
        {
            return;
        }

        _context.PaymentMethods.AddRange(
            new PaymentMethodEntity { Name = "Cash", Code = "CASH", Kind = "Cash" },
            new PaymentMethodEntity { Name = "Card", Code = "CARD", Kind = "Card" },
            new PaymentMethodEntity { Name = "Member Account", Code = "ACCOUNT", Kind = "Account" });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedStarterCurrenciesAsync(CancellationToken cancellationToken)
    {
        if (await _context.Currencies.AnyAsync(cancellationToken: cancellationToken))
        {
            return;
        }

        _context.Currencies.Add(new CurrencyEntity
        {
            Code = "ZAR",
            Name = "South African Rand",
            Symbol = "R",
            DecimalPlaces = 2,
            IsDefault = true,
            IsActive = true,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedBaselineRolesAsync(Dictionary<string, Guid> permissionIds, CancellationToken cancellationToken)
    {
        foreach (var (roleName, permissionKeys) in BaselineRolePermissionMap)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ClubStackRole(roleName) { IsSystem = true };
                var created = await _roleManager.CreateAsync(role);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Baseline role '{roleName}' could not be created: {string.Join("; ", created.Errors.Select(e => e.Description))}");
                }
            }
            else if (!role.IsSystem)
            {
                role.IsSystem = true;
                await _roleManager.UpdateAsync(role);
            }

            // Add missing starter permissions only; never remove administrator edits.
            var assigned = await _context.RolePermissions
                .AsNoTracking()
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission!.Key)
                .ToListAsync(cancellationToken);

            var missing = permissionKeys
                .Where(key => !assigned.Contains(key, StringComparer.Ordinal))
                .ToList();

            foreach (var key in missing)
            {
                _context.RolePermissions.Add(new RolePermissionEntity
                {
                    RoleId = role.Id,
                    PermissionId = permissionIds[key],
                });
            }
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<string> All(params string[] modules) =>
        Permissions.Catalog.Where(p => modules.Contains(p.Module)).Select(p => p.Key).ToArray();

    private static IReadOnlyList<string> Keys(params string[] keys) => keys;

    /// <summary>
    /// Documented starter configuration (Phase 9 §42): standard roles with
    /// permission starter maps. Administrators may adjust every mapping.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BaselineRolePermissionMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["System Administrator"] = Permissions.All,
            ["Club Manager"] = All("Platform", "Membership", "Credentials", "Accounts", "Facilities")
                .Union(Keys(
                    Permissions.IdentityUserView,
                    Permissions.IdentityUserCreate,
                    Permissions.IdentityUserEdit,
                    Permissions.IdentityUserDeactivate,
                    Permissions.IdentityUserResetPassword,
                    Permissions.IdentityRoleView,
                    Permissions.IdentityPermissionView,
                    Permissions.IdentitySessionRevoke,
                    Permissions.PosProductView,
                    Permissions.PosRegisterView,
                    Permissions.InventoryProductView,
                    Permissions.InventoryStockView,
                    Permissions.InventoryStockCount,
                    Permissions.GuestsGuestView,
                    Permissions.GuestsGuestRegister,
                    Permissions.GuestsGuestCheckIn,
                    Permissions.GuestsGuestCheckOut,
                    Permissions.ReportsView,
                    Permissions.ReportsExport,
                    Permissions.DashboardsView,
                    Permissions.AuditView,

                    // D5.5: fiscal device administration and fiscal day lifecycle.
                    Permissions.FiscalizationDeviceView,
                    Permissions.FiscalizationDeviceManage,
                    Permissions.FiscalizationDayOpen,
                    Permissions.FiscalizationDayClose,
                    Permissions.FiscalizationTransactionView,
                    Permissions.FiscalizationTransactionRetry), StringComparer.Ordinal)
                .ToArray(),
            ["Membership Officer"] = All("Membership", "Credentials", "Guests")
                .Union(Keys(
                    Permissions.AccountsAccountView,
                    Permissions.AccountsTransactionView,
                    Permissions.AccountsStatementGenerate,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Finance Officer"] = All("Accounts", "Finance")
                .Union(Keys(
                    Permissions.PlatformConfigurationView,
                    Permissions.PosProductView,
                    Permissions.PosSaleCreate,
                    Permissions.PosSaleComplete,
                    Permissions.PosSaleRefund,
                    Permissions.PosSaleReprint,
                    Permissions.ReportsView,
                    Permissions.ReportsExport,
                    Permissions.DashboardsView,

                    // D5.5: the finance-management role owns the fiscal device and its
                    // day lifecycle, so it carries the full fiscalisation set.
                    Permissions.FiscalizationDeviceView,
                    Permissions.FiscalizationDeviceManage,
                    Permissions.FiscalizationDayOpen,
                    Permissions.FiscalizationDayClose,
                    Permissions.FiscalizationTransactionView,
                    Permissions.FiscalizationTransactionRetry), StringComparer.Ordinal)
                .ToArray(),
            ["Cashier"] = All("Cashiering")
                .Union(Keys(
                    Permissions.AccountsTransactionView,
                    Permissions.AccountsPaymentRecord,
                    Permissions.PosProductView,
                    Permissions.PosSaleCreate,
                    Permissions.PosSaleComplete,
                    Permissions.PosSaleMemberCharge,
                    Permissions.PosSaleReprint,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["POS Operator"] = Keys(
                Permissions.PosProductView,
                Permissions.PosSaleCreate,
                Permissions.PosSaleComplete,
                Permissions.PosSaleMemberCharge,
                Permissions.PosSaleReprint,
                Permissions.AccountsAccountView,
                Permissions.AccountsPaymentRecord,
                Permissions.CredentialsCardView,
                Permissions.CashieringSessionOpen,
                Permissions.CashieringSessionView,
                Permissions.CashieringSessionClose,
                Permissions.CashieringCashMovementCreate,
                Permissions.DashboardsView),
            ["Golf Operations"] = All("Golf")
                .Union(Keys(
                    Permissions.FacilitiesFacilityView,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Tournament Manager"] = All("Golf")
                .Where(k => k.StartsWith("Golf.Tournament.", StringComparison.Ordinal) ||
                            k.StartsWith("Golf.Scoring.", StringComparison.Ordinal))
                .Union(Keys(
                    Permissions.GolfBookingView,
                    Permissions.GolfBookingCreate,
                    Permissions.GolfBookingEdit,
                    Permissions.GolfBookingCancel,
                    Permissions.GolfBookingCheckIn,
                    Permissions.GolfGuestManage,
                    Permissions.CredentialsCardView,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Inventory Officer"] = All("Inventory")
                .Union(Keys(
                    Permissions.PosProductView,
                    Permissions.ProcurementRequisitionCreate,
                    Permissions.ProcurementPurchaseOrderCreate,
                    Permissions.ProcurementGoodsReceiptCreate,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Procurement Officer"] = All("Procurement")
                .Union(Keys(
                    Permissions.InventoryStockView,
                    Permissions.InventoryProductView,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["F&B Manager"] = All("POS")
                .Union(Keys(
                    Permissions.InventoryStockView,
                    Permissions.InventoryStockCount,
                    Permissions.InventoryStockAdjust,
                    Permissions.InventoryStockWastage,
                    Permissions.ProcurementRequisitionCreate,
                    Permissions.AccountsPaymentRecord,
                    Permissions.CashieringVarianceApprove,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["F&B Operator"] = Keys(
                Permissions.PosProductView,
                Permissions.PosSaleCreate,
                Permissions.PosSaleComplete,
                Permissions.PosSaleMemberCharge,
                Permissions.PosSaleReprint,
                Permissions.AccountsAccountView,
                Permissions.AccountsTransactionView,
                Permissions.CredentialsCardView,
                Permissions.CashieringSessionOpen,
                Permissions.CashieringSessionView,
                Permissions.CashieringSessionClose,
                Permissions.CashieringCashMovementCreate),
            ["Pro Shop Operator"] = Keys(
                Permissions.PosProductView,
                Permissions.PosSaleCreate,
                Permissions.PosSaleComplete,
                Permissions.PosSaleMemberCharge,
                Permissions.PosSaleReprint,
                Permissions.CredentialsCardView,
                Permissions.CredentialsCardActivate,
                Permissions.CredentialsCardSuspend,
                Permissions.CredentialsCardReactivate,
                Permissions.AccountsPaymentRecord,
                Permissions.AccountsAccountView,
                Permissions.GolfBookingView,
                Permissions.GolfBookingCreate,
                Permissions.GolfBookingEdit,
                Permissions.GolfBookingCancel,
                Permissions.GolfBookingCheckIn,
                Permissions.GolfGuestManage,
                Permissions.CashieringSessionOpen,
                Permissions.CashieringSessionView,
                Permissions.CashieringSessionClose,
                Permissions.DashboardsView),
            ["Asset Manager"] = All("Assets")
                .Union(Keys(
                    Permissions.MaintenanceWorkOrderView,
                    Permissions.MaintenanceWorkOrderAssign,
                    Permissions.MaintenanceWorkOrderComplete,
                    Permissions.ProcurementSupplierView,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Maintenance Officer"] = All("Maintenance")
                .Union(Keys(
                    Permissions.AssetsAssetView,
                    Permissions.AssetsAssetEdit,
                    Permissions.FacilitiesFacilityView,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Reception Officer"] = All("Guests")
                .Union(Keys(
                    Permissions.MembershipMemberView,
                    Permissions.CredentialsCardView,
                    Permissions.FacilitiesFacilityView,
                    Permissions.FacilitiesReservationView,
                    Permissions.FacilitiesReservationCreate,
                    Permissions.FacilitiesReservationEdit,
                    Permissions.FacilitiesReservationCancel,
                    Permissions.EventsEventView,
                    Permissions.EventsRegistrationManage,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Event Manager"] = All("Events")
                .Union(Keys(
                    Permissions.FacilitiesFacilityView,
                    Permissions.FacilitiesReservationView,
                    Permissions.FacilitiesReservationCreate,
                    Permissions.FacilitiesReservationEdit,
                    Permissions.FacilitiesReservationCancel,
                    Permissions.GuestsGuestView,
                    Permissions.GuestsGuestRegister,
                    Permissions.DashboardsView), StringComparer.Ordinal)
                .ToArray(),
            ["Auditor"] = Keys(
                Permissions.AuditView,
                Permissions.AuditExport,
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.AccountsAccountView,
                Permissions.AccountsTransactionView,
                Permissions.AccountsAgingView,
                Permissions.FinanceJournalView,
                Permissions.FinanceReportView,
                Permissions.FinanceArView,
                Permissions.FinanceApView,
                Permissions.IdentityUserView,
                Permissions.IdentityRoleView,
                Permissions.IdentityPermissionView,
                Permissions.DashboardsView),
            ["Report Viewer"] = Keys(
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.ReportsPrint,
                Permissions.DashboardsView,
                Permissions.AccountsAccountView,
                Permissions.AccountsTransactionView,
                Permissions.FinanceJournalView,
                Permissions.InventoryStockView,
                Permissions.MembershipMemberView,
                Permissions.PosProductView,
                Permissions.CashieringSessionView,
                Permissions.GolfBookingView,
                Permissions.EventsEventView),
        };
}