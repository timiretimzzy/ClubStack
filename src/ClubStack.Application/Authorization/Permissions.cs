namespace ClubStack.Application.Authorization;

/// <summary>
/// Metadata for a single action-based permission. The <c>Key</c> follows the
/// authoritative <c>Module.Resource.Action</c> convention (Phase 8 §4.1/§4.2).
/// <c>IsSensitive</c> flags permissions that gate sensitive actions (Phase 8 §5)
/// which require elevated authorization and must always be audited.
/// </summary>
public sealed record PermissionDescriptor(string Key, string Module, string Description, bool IsSensitive = false);

/// <summary>
/// The authoritative ClubStack permission catalogue (Phase 8 §4.2) plus the
/// technical infrastructure permissions established during D0. Business
/// permissions exist as catalogue/seed entries for later slices; only D1-scoped
/// permissions are currently enforced on API endpoints.
/// </summary>
public static class Permissions
{
    // ---- Technical (D0) ----
    public const string PlatformHealthView = "Platform.Health.View";
    public const string PlatformDiagnosticsView = "Platform.Diagnostics.View";
    public const string PlatformDiagnosticsAll = "Platform.Diagnostics.All";

    // ---- Platform ----
    public const string PlatformClubView = "Platform.Club.View";
    public const string PlatformClubEdit = "Platform.Club.Edit";
    public const string PlatformDepartmentView = "Platform.Department.View";
    public const string PlatformDepartmentManage = "Platform.Department.Manage";
    public const string PlatformLocationView = "Platform.Location.View";
    public const string PlatformLocationManage = "Platform.Location.Manage";
    public const string PlatformOutletView = "Platform.Outlet.View";
    public const string PlatformOutletManage = "Platform.Outlet.Manage";
    public const string PlatformFacilityView = "Platform.Facility.View";
    public const string PlatformFacilityManage = "Platform.Facility.Manage";
    public const string PlatformPaymentMethodManage = "Platform.PaymentMethod.Manage";
    public const string PlatformCurrencyManage = "Platform.Currency.Manage";
    public const string PlatformTaxManage = "Platform.Tax.Manage";
    public const string PlatformNumberingManage = "Platform.Numbering.Manage";
    public const string PlatformConfigurationView = "Platform.Configuration.View";
    public const string PlatformConfigurationEdit = "Platform.Configuration.Edit";

    // ---- Identity ----
    public const string IdentityUserView = "Identity.User.View";
    public const string IdentityUserCreate = "Identity.User.Create";
    public const string IdentityUserEdit = "Identity.User.Edit";
    public const string IdentityUserDeactivate = "Identity.User.Deactivate";
    public const string IdentityUserResetPassword = "Identity.User.ResetPassword";
    public const string IdentityRoleView = "Identity.Role.View";
    public const string IdentityRoleManage = "Identity.Role.Manage";
    public const string IdentityPermissionView = "Identity.Permission.View";
    public const string IdentitySessionRevoke = "Identity.Session.Revoke";

    // ---- Membership ----
    public const string MembershipMemberView = "Membership.Member.View";
    public const string MembershipMemberCreate = "Membership.Member.Create";
    public const string MembershipMemberEdit = "Membership.Member.Edit";
    public const string MembershipMemberApprove = "Membership.Member.Approve";
    public const string MembershipMemberActivate = "Membership.Member.Activate";
    public const string MembershipMemberSuspend = "Membership.Member.Suspend";
    public const string MembershipMemberReinstate = "Membership.Member.Reinstate";
    public const string MembershipMemberRenew = "Membership.Member.Renew";
    public const string MembershipMemberTransfer = "Membership.Member.Transfer";
    public const string MembershipMemberCancel = "Membership.Member.Cancel";
    public const string MembershipCategoryView = "Membership.Category.View";
    public const string MembershipCategoryManage = "Membership.Category.Manage";
    public const string MembershipApplicationView = "Membership.Application.View";
    public const string MembershipApplicationCreate = "Membership.Application.Create";
    public const string MembershipApplicationApprove = "Membership.Application.Approve";
    public const string MembershipHouseholdManage = "Membership.Household.Manage";
    public const string MembershipDependantManage = "Membership.Dependant.Manage";
    public const string MembershipFeeView = "Membership.Fee.View";
    public const string MembershipFeeAssess = "Membership.Fee.Assess";
    public const string MembershipFeeAdjust = "Membership.Fee.Adjust";

    // ---- Credentials ----
    public const string CredentialsCardView = "Credentials.Card.View";
    public const string CredentialsCardIssue = "Credentials.Card.Issue";
    public const string CredentialsCardActivate = "Credentials.Card.Activate";
    public const string CredentialsCardSuspend = "Credentials.Card.Suspend";
    public const string CredentialsCardMarkLost = "Credentials.Card.MarkLost";
    public const string CredentialsCardMarkStolen = "Credentials.Card.MarkStolen";
    public const string CredentialsCardReplace = "Credentials.Card.Replace";
    public const string CredentialsCardReactivate = "Credentials.Card.Reactivate";

    // ---- Member accounts ----
    public const string AccountsAccountView = "Accounts.Account.View";
    public const string AccountsTransactionView = "Accounts.Transaction.View";
    public const string AccountsTransactionCreate = "Accounts.Transaction.Create";
    public const string AccountsPaymentRecord = "Accounts.Payment.Record";
    public const string AccountsPaymentReverse = "Accounts.Payment.Reverse";
    public const string AccountsCreditCreate = "Accounts.Credit.Create";
    public const string AccountsAdjustmentCreate = "Accounts.Adjustment.Create";
    public const string AccountsAdjustmentApprove = "Accounts.Adjustment.Approve";
    public const string AccountsStatementGenerate = "Accounts.Statement.Generate";
    public const string AccountsAgingView = "Accounts.Aging.View";

    // ---- POS ----
    public const string PosProductView = "POS.Product.View";
    public const string PosSaleCreate = "POS.Sale.Create";
    public const string PosSaleComplete = "POS.Sale.Complete";
    public const string PosSaleVoid = "POS.Sale.Void";
    public const string PosSaleRefund = "POS.Sale.Refund";
    public const string PosSaleDiscount = "POS.Sale.Discount";
    public const string PosSaleMemberCharge = "POS.Sale.MemberCharge";
    public const string PosSaleReprint = "POS.Sale.Reprint";
    public const string PosRegisterView = "POS.Register.View";
    public const string PosRegisterManage = "POS.Register.Manage";

    // ---- Cashiering ----
    public const string CashieringSessionOpen = "Cashiering.Session.Open";
    public const string CashieringSessionView = "Cashiering.Session.View";
    public const string CashieringCashMovementCreate = "Cashiering.CashMovement.Create";
    public const string CashieringSessionClose = "Cashiering.Session.Close";
    public const string CashieringSessionReconcile = "Cashiering.Session.Reconcile";
    public const string CashieringVarianceApprove = "Cashiering.Variance.Approve";

    // ---- Fiscalization (D5.5 ZIMRA) ----
    public const string FiscalizationDeviceView = "Fiscalization.Device.View";
    public const string FiscalizationDeviceManage = "Fiscalization.Device.Manage";
    public const string FiscalizationDayOpen = "Fiscalization.Day.Open";
    public const string FiscalizationDayClose = "Fiscalization.Day.Close";
    public const string FiscalizationTransactionView = "Fiscalization.Transaction.View";
    public const string FiscalizationTransactionRetry = "Fiscalization.Transaction.Retry";

    // ---- Inventory ----
    public const string InventoryProductView = "Inventory.Product.View";
    public const string InventoryProductCreate = "Inventory.Product.Create";
    public const string InventoryProductEdit = "Inventory.Product.Edit";
    public const string InventoryStockView = "Inventory.Stock.View";
    public const string InventoryStockReceive = "Inventory.Stock.Receive";
    public const string InventoryStockTransfer = "Inventory.Stock.Transfer";
    public const string InventoryStockCount = "Inventory.Stock.Count";
    public const string InventoryStockAdjust = "Inventory.Stock.Adjust";
    public const string InventoryStockWastage = "Inventory.Stock.Wastage";
    public const string InventoryStockApproveAdjustment = "Inventory.Stock.ApproveAdjustment";
    public const string InventoryLocationManage = "Inventory.Location.Manage";

    // ---- Procurement ----
    public const string ProcurementSupplierView = "Procurement.Supplier.View";
    public const string ProcurementSupplierManage = "Procurement.Supplier.Manage";
    public const string ProcurementRequisitionCreate = "Procurement.Requisition.Create";
    public const string ProcurementRequisitionApprove = "Procurement.Requisition.Approve";
    public const string ProcurementPurchaseOrderCreate = "Procurement.PurchaseOrder.Create";
    public const string ProcurementPurchaseOrderApprove = "Procurement.PurchaseOrder.Approve";
    public const string ProcurementGoodsReceiptCreate = "Procurement.GoodsReceipt.Create";
    public const string ProcurementGoodsReceiptReverse = "Procurement.GoodsReceipt.Reverse";
    public const string ProcurementSupplierInvoiceRecord = "Procurement.SupplierInvoice.Record";

    // ---- Golf ----
    public const string GolfCourseView = "Golf.Course.View";
    public const string GolfCourseManage = "Golf.Course.Manage";
    public const string GolfTeeSheetView = "Golf.TeeSheet.View";
    public const string GolfTeeSlotManage = "Golf.TeeSlot.Manage";
    public const string GolfBookingView = "Golf.Booking.View";
    public const string GolfBookingCreate = "Golf.Booking.Create";
    public const string GolfBookingEdit = "Golf.Booking.Edit";
    public const string GolfBookingCancel = "Golf.Booking.Cancel";
    public const string GolfBookingOverride = "Golf.Booking.Override";
    public const string GolfBookingCheckIn = "Golf.Booking.CheckIn";
    public const string GolfGuestManage = "Golf.Guest.Manage";
    public const string GolfChargeCreate = "Golf.Charge.Create";
    public const string GolfTournamentView = "Golf.Tournament.View";
    public const string GolfTournamentManage = "Golf.Tournament.Manage";
    public const string GolfScoringManage = "Golf.Scoring.Manage";

    // ---- Finance ----
    public const string FinanceAccountView = "Finance.Account.View";
    public const string FinanceAccountManage = "Finance.Account.Manage";
    public const string FinanceJournalView = "Finance.Journal.View";
    public const string FinanceJournalCreate = "Finance.Journal.Create";
    public const string FinanceJournalPost = "Finance.Journal.Post";
    public const string FinanceJournalReverse = "Finance.Journal.Reverse";
    public const string FinancePeriodView = "Finance.Period.View";
    public const string FinancePeriodOpen = "Finance.Period.Open";
    public const string FinancePeriodClose = "Finance.Period.Close";
    // D6 (PM Decision Pack B5): reopening a closed period is the highest-impact
    // finance action, so it is sensitive and always audited.
    public const string FinancePeriodReopen = "Finance.Period.Reopen";
    public const string FinanceArView = "Finance.AR.View";
    // D6 (PM Decision Pack B14): AR/AP mutation and payment management.
    public const string FinanceArManage = "Finance.AR.Manage";
    public const string FinanceApView = "Finance.AP.View";
    public const string FinanceApManage = "Finance.AP.Manage";
    public const string FinancePaymentManage = "Finance.Payment.Manage";
    public const string FinanceReconciliationPerform = "Finance.Reconciliation.Perform";
    public const string FinanceReportView = "Finance.Report.View";

    // ---- Assets and maintenance ----
    public const string AssetsAssetView = "Assets.Asset.View";
    public const string AssetsAssetCreate = "Assets.Asset.Create";
    public const string AssetsAssetEdit = "Assets.Asset.Edit";
    public const string AssetsAssetRetire = "Assets.Asset.Retire";
    public const string MaintenanceWorkOrderView = "Maintenance.WorkOrder.View";
    public const string MaintenanceWorkOrderCreate = "Maintenance.WorkOrder.Create";
    public const string MaintenanceWorkOrderAssign = "Maintenance.WorkOrder.Assign";
    public const string MaintenanceWorkOrderComplete = "Maintenance.WorkOrder.Complete";
    public const string MaintenanceScheduleManage = "Maintenance.Schedule.Manage";
    public const string MaintenanceCostRecord = "Maintenance.Cost.Record";

    // ---- Facilities, events and guests ----
    public const string FacilitiesFacilityView = "Facilities.Facility.View";
    public const string FacilitiesFacilityManage = "Facilities.Facility.Manage";
    public const string FacilitiesReservationView = "Facilities.Reservation.View";
    public const string FacilitiesReservationCreate = "Facilities.Reservation.Create";
    public const string FacilitiesReservationEdit = "Facilities.Reservation.Edit";
    public const string FacilitiesReservationCancel = "Facilities.Reservation.Cancel";
    public const string FacilitiesReservationOverride = "Facilities.Reservation.Override";
    public const string EventsEventView = "Events.Event.View";
    public const string EventsEventCreate = "Events.Event.Create";
    public const string EventsEventEdit = "Events.Event.Edit";
    public const string EventsEventCancel = "Events.Event.Cancel";
    public const string EventsRegistrationManage = "Events.Registration.Manage";
    public const string EventsAttendanceRecord = "Events.Attendance.Record";
    public const string GuestsGuestView = "Guests.Guest.View";
    public const string GuestsGuestRegister = "Guests.Guest.Register";
    public const string GuestsGuestCheckIn = "Guests.Guest.CheckIn";
    public const string GuestsGuestCheckOut = "Guests.Guest.CheckOut";

    // ---- Reporting, dashboards and audit ----
    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string ReportsPrint = "Reports.Print";
    public const string DashboardsView = "Dashboards.View";
    public const string AuditView = "Audit.View";
    public const string AuditExport = "Audit.Export";

    /// <summary>
    /// The authoritative permission catalogue (Phase 8 §4.2 + D0 technical
    /// permissions). Sensitive flags follow Phase 8 §5.
    /// </summary>
    public static readonly IReadOnlyList<PermissionDescriptor> Catalog =
    [
        // Technical (D0)
        new(PlatformHealthView, "Platform", "View the application health state"),
        new(PlatformDiagnosticsView, "Platform", "View system diagnostics"),
        new(PlatformDiagnosticsAll, "Platform", "View full system diagnostics", IsSensitive: true),

        // Platform
        new(PlatformClubView, "Platform", "View the club profile and settings"),
        new(PlatformClubEdit, "Platform", "Edit the club profile and settings", IsSensitive: true),
        new(PlatformDepartmentView, "Platform", "View departments"),
        new(PlatformDepartmentManage, "Platform", "Manage departments", IsSensitive: true),
        new(PlatformLocationView, "Platform", "View locations"),
        new(PlatformLocationManage, "Platform", "Manage locations", IsSensitive: true),
        new(PlatformOutletView, "Platform", "View outlets"),
        new(PlatformOutletManage, "Platform", "Manage outlets", IsSensitive: true),
        new(PlatformFacilityView, "Platform", "View facilities (base configuration)"),
        new(PlatformFacilityManage, "Platform", "Manage facilities (base configuration)", IsSensitive: true),
        new(PlatformPaymentMethodManage, "Platform", "Manage payment methods", IsSensitive: true),
        new(PlatformCurrencyManage, "Platform", "Manage currencies", IsSensitive: true),
        new(PlatformTaxManage, "Platform", "Manage tax rates", IsSensitive: true),
        new(PlatformNumberingManage, "Platform", "Manage numbering configuration", IsSensitive: true),
        new(PlatformConfigurationView, "Platform", "View system configuration"),
        new(PlatformConfigurationEdit, "Platform", "Edit system configuration", IsSensitive: true),

        // Identity
        new(IdentityUserView, "Identity", "View users"),
        new(IdentityUserCreate, "Identity", "Create users", IsSensitive: true),
        new(IdentityUserEdit, "Identity", "Edit users", IsSensitive: true),
        new(IdentityUserDeactivate, "Identity", "Deactivate users and their sessions", IsSensitive: true),
        new(IdentityUserResetPassword, "Identity", "Reset user passwords", IsSensitive: true),
        new(IdentityRoleView, "Identity", "View roles"),
        new(IdentityRoleManage, "Identity", "Manage roles and role permissions", IsSensitive: true),
        new(IdentityPermissionView, "Identity", "View the permission catalogue"),
        new(IdentitySessionRevoke, "Identity", "Revoke user sessions", IsSensitive: true),

        // Membership
        new(MembershipMemberView, "Membership", "View members"),
        new(MembershipMemberCreate, "Membership", "Create members"),
        new(MembershipMemberEdit, "Membership", "Edit members"),
        new(MembershipMemberApprove, "Membership", "Approve membership applications", IsSensitive: true),
        new(MembershipMemberActivate, "Membership", "Activate members"),
        new(MembershipMemberSuspend, "Membership", "Suspend members", IsSensitive: true),
        new(MembershipMemberReinstate, "Membership", "Reinstate members", IsSensitive: true),
        new(MembershipMemberRenew, "Membership", "Renew memberships"),
        new(MembershipMemberTransfer, "Membership", "Transfer memberships"),
        new(MembershipMemberCancel, "Membership", "Cancel memberships", IsSensitive: true),
        new(MembershipCategoryView, "Membership", "View membership categories"),
        new(MembershipCategoryManage, "Membership", "Manage membership categories", IsSensitive: true),
        new(MembershipApplicationView, "Membership", "View membership applications"),
        new(MembershipApplicationCreate, "Membership", "Create membership applications"),
        new(MembershipApplicationApprove, "Membership", "Approve membership applications", IsSensitive: true),
        new(MembershipHouseholdManage, "Membership", "Manage households"),
        new(MembershipDependantManage, "Membership", "Manage dependants"),
        new(MembershipFeeView, "Membership", "View membership fees"),
        new(MembershipFeeAssess, "Membership", "Assess membership fees"),
        new(MembershipFeeAdjust, "Membership", "Adjust membership fees", IsSensitive: true),

        // Credentials
        new(CredentialsCardView, "Credentials", "View credentials"),
        new(CredentialsCardIssue, "Credentials", "Issue credentials"),
        new(CredentialsCardActivate, "Credentials", "Activate credentials"),
        new(CredentialsCardSuspend, "Credentials", "Suspend credentials"),
        new(CredentialsCardMarkLost, "Credentials", "Mark credentials lost"),
        new(CredentialsCardMarkStolen, "Credentials", "Mark credentials stolen"),
        new(CredentialsCardReplace, "Credentials", "Replace credentials", IsSensitive: true),
        new(CredentialsCardReactivate, "Credentials", "Reactivate credentials"),

        // Member accounts
        new(AccountsAccountView, "Accounts", "View member accounts"),
        new(AccountsTransactionView, "Accounts", "View account transactions"),
        new(AccountsTransactionCreate, "Accounts", "Create account transactions"),
        new(AccountsPaymentRecord, "Accounts", "Record account payments"),
        new(AccountsPaymentReverse, "Accounts", "Reverse account payments", IsSensitive: true),
        new(AccountsCreditCreate, "Accounts", "Create account credits"),
        new(AccountsAdjustmentCreate, "Accounts", "Create account adjustments", IsSensitive: true),
        new(AccountsAdjustmentApprove, "Accounts", "Approve account adjustments", IsSensitive: true),
        new(AccountsStatementGenerate, "Accounts", "Generate account statements"),
        new(AccountsAgingView, "Accounts", "View account aging"),

        // POS
        new(PosProductView, "POS", "View POS products"),
        new(PosSaleCreate, "POS", "Create POS sales"),
        new(PosSaleComplete, "POS", "Complete POS sales"),
        new(PosSaleVoid, "POS", "Void POS sales", IsSensitive: true),
        new(PosSaleRefund, "POS", "Refund POS sales", IsSensitive: true),
        new(PosSaleDiscount, "POS", "Apply POS discounts", IsSensitive: true),
        new(PosSaleMemberCharge, "POS", "Charge POS sales to member accounts"),
        new(PosSaleReprint, "POS", "Reprint POS receipts"),
        new(PosRegisterView, "POS", "View POS registers"),
        new(PosRegisterManage, "POS", "Manage POS registers", IsSensitive: true),

        // Cashiering
        new(CashieringSessionOpen, "Cashiering", "Open cashier sessions"),
        new(CashieringSessionView, "Cashiering", "View cashier sessions"),
        new(CashieringCashMovementCreate, "Cashiering", "Create cash movements"),
        new(CashieringSessionClose, "Cashiering", "Close cashier sessions", IsSensitive: true),
        new(CashieringSessionReconcile, "Cashiering", "Reconcile cashier sessions"),
        new(CashieringVarianceApprove, "Cashiering", "Approve cashier variances", IsSensitive: true),

        // Fiscalization (D5.5 ZIMRA)
        new(FiscalizationDeviceView, "Fiscalization", "View fiscal device status and configuration"),
        new(FiscalizationDeviceManage, "Fiscalization", "Manage fiscal device configuration and registration", IsSensitive: true),
        new(FiscalizationDayOpen, "Fiscalization", "Open the ZIMRA fiscal day", IsSensitive: true),
        new(FiscalizationDayClose, "Fiscalization", "Close the ZIMRA fiscal day", IsSensitive: true),
        new(FiscalizationTransactionView, "Fiscalization", "View ZIMRA fiscalisation results"),
        new(FiscalizationTransactionRetry, "Fiscalization", "Retry failed ZIMRA fiscalisation", IsSensitive: true),

        // Inventory
        new(InventoryProductView, "Inventory", "View products"),
        new(InventoryProductCreate, "Inventory", "Create products"),
        new(InventoryProductEdit, "Inventory", "Edit products"),
        new(InventoryStockView, "Inventory", "View stock"),
        new(InventoryStockReceive, "Inventory", "Receive stock"),
        new(InventoryStockTransfer, "Inventory", "Transfer stock"),
        new(InventoryStockCount, "Inventory", "Perform stock counts"),
        new(InventoryStockAdjust, "Inventory", "Adjust stock", IsSensitive: true),
        new(InventoryStockWastage, "Inventory", "Record stock wastage", IsSensitive: true),
        new(InventoryStockApproveAdjustment, "Inventory", "Approve stock adjustments", IsSensitive: true),
        new(InventoryLocationManage, "Inventory", "Manage inventory locations", IsSensitive: true),

        // Procurement
        new(ProcurementSupplierView, "Procurement", "View suppliers"),
        new(ProcurementSupplierManage, "Procurement", "Manage suppliers"),
        new(ProcurementRequisitionCreate, "Procurement", "Create purchase requisitions"),
        new(ProcurementRequisitionApprove, "Procurement", "Approve purchase requisitions", IsSensitive: true),
        new(ProcurementPurchaseOrderCreate, "Procurement", "Create purchase orders"),
        new(ProcurementPurchaseOrderApprove, "Procurement", "Approve purchase orders", IsSensitive: true),
        new(ProcurementGoodsReceiptCreate, "Procurement", "Create goods receipts"),
        new(ProcurementGoodsReceiptReverse, "Procurement", "Reverse goods receipts", IsSensitive: true),
        new(ProcurementSupplierInvoiceRecord, "Procurement", "Record supplier invoices"),

        // Golf
        new(GolfCourseView, "Golf", "View golf courses"),
        new(GolfCourseManage, "Golf", "Manage golf courses", IsSensitive: true),
        new(GolfTeeSheetView, "Golf", "View the tee sheet"),
        new(GolfTeeSlotManage, "Golf", "Manage tee slots", IsSensitive: true),
        new(GolfBookingView, "Golf", "View golf bookings"),
        new(GolfBookingCreate, "Golf", "Create golf bookings"),
        new(GolfBookingEdit, "Golf", "Edit golf bookings"),
        new(GolfBookingCancel, "Golf", "Cancel golf bookings"),
        new(GolfBookingOverride, "Golf", "Override golf booking capacity", IsSensitive: true),
        new(GolfBookingCheckIn, "Golf", "Check in golf bookings"),
        new(GolfGuestManage, "Golf", "Manage golf guests"),
        new(GolfChargeCreate, "Golf", "Create golf charges"),
        new(GolfTournamentView, "Golf", "View tournaments"),
        new(GolfTournamentManage, "Golf", "Manage tournaments", IsSensitive: true),
        new(GolfScoringManage, "Golf", "Manage tournament scoring"),

        // Finance
        new(FinanceAccountView, "Finance", "View financial accounts"),
        new(FinanceAccountManage, "Finance", "Manage the chart of accounts", IsSensitive: true),
        new(FinanceJournalView, "Finance", "View journals"),
        new(FinanceJournalCreate, "Finance", "Create journals"),
        new(FinanceJournalPost, "Finance", "Post journals", IsSensitive: true),
        new(FinanceJournalReverse, "Finance", "Reverse journals", IsSensitive: true),
        new(FinancePeriodView, "Finance", "View financial periods"),
        new(FinancePeriodOpen, "Finance", "Open financial periods"),
        new(FinancePeriodClose, "Finance", "Close financial periods", IsSensitive: true),
        new(FinancePeriodReopen, "Finance", "Reopen the most recently closed financial period", IsSensitive: true),
        new(FinanceArView, "Finance", "View accounts receivable"),
        new(FinanceArManage, "Finance", "Manage accounts receivable", IsSensitive: true),
        new(FinanceApView, "Finance", "View accounts payable"),
        new(FinanceApManage, "Finance", "Manage accounts payable", IsSensitive: true),
        new(FinancePaymentManage, "Finance", "Manage finance payments and allocations", IsSensitive: true),
        new(FinanceReconciliationPerform, "Finance", "Perform financial reconciliation"),
        new(FinanceReportView, "Finance", "View financial reports"),

        // Assets and maintenance
        new(AssetsAssetView, "Assets", "View assets"),
        new(AssetsAssetCreate, "Assets", "Create assets"),
        new(AssetsAssetEdit, "Assets", "Edit assets"),
        new(AssetsAssetRetire, "Assets", "Retire assets", IsSensitive: true),
        new(MaintenanceWorkOrderView, "Maintenance", "View maintenance work orders"),
        new(MaintenanceWorkOrderCreate, "Maintenance", "Create maintenance work orders"),
        new(MaintenanceWorkOrderAssign, "Maintenance", "Assign maintenance work orders"),
        new(MaintenanceWorkOrderComplete, "Maintenance", "Complete maintenance work orders"),
        new(MaintenanceScheduleManage, "Maintenance", "Manage maintenance schedules"),
        new(MaintenanceCostRecord, "Maintenance", "Record maintenance costs"),

        // Facilities, events and guests
        new(FacilitiesFacilityView, "Facilities", "View facilities"),
        new(FacilitiesFacilityManage, "Facilities", "Manage facilities", IsSensitive: true),
        new(FacilitiesReservationView, "Facilities", "View reservations"),
        new(FacilitiesReservationCreate, "Facilities", "Create reservations"),
        new(FacilitiesReservationEdit, "Facilities", "Edit reservations"),
        new(FacilitiesReservationCancel, "Facilities", "Cancel reservations"),
        new(FacilitiesReservationOverride, "Facilities", "Override reservation conflicts", IsSensitive: true),
        new(EventsEventView, "Events", "View events"),
        new(EventsEventCreate, "Events", "Create events"),
        new(EventsEventEdit, "Events", "Edit events"),
        new(EventsEventCancel, "Events", "Cancel events", IsSensitive: true),
        new(EventsRegistrationManage, "Events", "Manage event registrations"),
        new(EventsAttendanceRecord, "Events", "Record event attendance"),
        new(GuestsGuestView, "Guests", "View guest visits"),
        new(GuestsGuestRegister, "Guests", "Register guests"),
        new(GuestsGuestCheckIn, "Guests", "Check in guests"),
        new(GuestsGuestCheckOut, "Guests", "Check out guests"),

        // Reporting, dashboards and audit
        new(ReportsView, "Reporting", "View reports"),
        new(ReportsExport, "Reporting", "Export reports"),
        new(ReportsPrint, "Reporting", "Print reports"),
        new(DashboardsView, "Reporting", "View dashboards"),
        new(AuditView, "Audit", "View audit activity"),
        new(AuditExport, "Audit", "Export audit activity"),
    ];

    /// <summary>All catalogue keys, used for seeding and enumeration.</summary>
    public static readonly IReadOnlyList<string> All = Catalog.Select(c => c.Key).ToArray();
}