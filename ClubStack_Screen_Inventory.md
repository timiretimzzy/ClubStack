# ClubStack Screen Inventory

**Date:** 2026-09-28
**Purpose:** Complete inventory of all user-facing screens before PM-directed restructuring

---

## Summary

| Metric | Count |
|---|---|
| Total routes | 35 |
| Full-page screens | 35 |
| Top-level navigation items | 32 |
| Modal/dialog screens | 0 |
| Drawer screens | 0 |
| Tab-based screens | 0 |

---

## Complete Screen List

### Dashboard

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/` | DashboardPage | Dashboard | Platform | Overview dashboard | View stats, recent activity | KPIs, recent members, recent sales | All | Full page | Top-level nav |

### Membership

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/members` | MembersPage | Members | Membership | Member list and search | Search, filter, create, view detail | Members, search results | MemberDetailPage | Full page | Top-level nav |
| `/members/:memberId` | MemberDetailPage | (detail) | Membership | Member 360 view | View, edit, suspend, activate | Member details, accounts, credentials, bookings | MembersPage | Full page | Via Members list |
| `/applications` | ApplicationsPage | Applications | Membership | Membership applications | Approve, reject, view | Applications, applicants | MembersPage | Full page | Top-level nav |
| `/categories` | CategoriesPage | Categories | Membership | Membership categories | Create, edit, view fees | Categories, fees | MembersPage | Full page | Top-level nav |
| `/households` | HouseholdsPage | Households | Membership | Household management | Create, edit, view | Households, members | MembersPage | Full page | Top-level nav |
| `/fees` | FeeAssessmentsPage | Fee Assessments | Membership | Fee assessment runs | Run assessments, view history | Fee assessments, invoices | MembersPage | Full page | Top-level nav |

### Products & Inventory

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/products` | ProductsPage | Products | Inventory | Product catalogue | Create, edit, price, view | Products, categories, prices | — | Full page | Top-level nav |
| `/stock` | StockPage | Stock | Inventory | Stock levels | View, adjust | Stock levels, products | ProductsPage | Full page | Top-level nav |
| `/transfers` | TransfersPage | Transfers | Inventory | Stock transfers | Create, view | Transfers, locations | StockPage | Full page | Top-level nav |
| `/counts` | CountsPage | Counts | Inventory | Stock counts | Create, view, reconcile | Count sheets, variances | StockPage | Full page | Top-level nav |
| `/adjustments` | AdjustmentsPage | Adjustments | Inventory | Stock adjustments | Create, view | Adjustments, reasons | StockPage | Full page | Top-level nav |
| `/wastage` | WastagePage | Wastage | Inventory | Wastage recording | Create, view | Wastage, reasons | StockPage | Full page | Top-level nav |
| `/alerts` | AlertsPage | Reorder Alerts | Inventory | Reorder alerts | View, acknowledge | Alerts, products | StockPage | Full page | Top-level nav |
| `/suppliers` | SuppliersPage | Suppliers | Procurement | Supplier management | Create, edit, view | Suppliers, contacts | ProductsPage | Full page | Top-level nav |

### Point of Sale

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/pos` | PosPage | Point of Sale | POS | POS terminal | Create sale, add items, complete | Sales, products, payments | SalesPage | Full page | Top-level nav |
| `/sales` | SalesPage | Sales | POS | Sales history | View, refund | Sales, payments | PosPage | Full page | Top-level nav |
| `/cashier-sessions` | CashierSessionsPage | Cashier Sessions | POS | Cashier session management | Open, close, view | Sessions, cashiers | PosPage | Full page | Top-level nav |

### Finance

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/finance` | FinanceOverviewPage | Finance Overview | Finance | Finance dashboard | View reports | KPIs, balances, trends | All finance | Full page | Top-level nav |
| `/finance/accounts` | ChartOfAccountsPage | Chart of Accounts | Finance | Account management | Create, edit, view | Accounts, balances | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/journals` | JournalsPage | Journals | Finance | Journal entries | Create, post, reverse | Journals, lines | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/receivables` | ReceivablesPage | Accounts Receivable | Finance | Receivables | View, collect | Invoices, payments | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/payables` | PayablesPage | Accounts Payable | Finance | Payables | View, pay | Bills, payments | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/payments` | PaymentsPage | Payments | Finance | Payment processing | Create, view | Payments, methods | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/periods` | FinancialPeriodsPage | Financial Periods | Finance | Period management | Open, close, view | Periods, statuses | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/reports` | FinanceReportsPage | Reports | Finance | Financial reports | View, export | Reports, filters | FinanceOverviewPage | Full page | Top-level nav |
| `/finance/reconciliation` | ReconciliationPage | Reconciliation | Finance | Bank reconciliation | Perform, view | Reconciliation, transactions | FinanceOverviewPage | Full page | Top-level nav |

### Golf

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/golf/courses` | GolfCoursesPage | Golf Courses | Golf | Course management | Create, edit, view | Courses, tees | — | Full page | Top-level nav |
| `/golf/tee-sheet` | GolfTeeSheetPage | Tee Sheet | Golf | Tee sheet view | View, generate | Tee times, slots | GolfCoursesPage | Full page | Top-level nav |
| `/golf/bookings` | GolfBookingsPage | Golf Bookings | Golf | Booking list | View, filter | Bookings, members | GolfBookingDetailPage | Full page | Top-level nav |
| `/golf/bookings/new` | GolfBookingsPage | (new) | Golf | New booking | Create | Booking form, members, tee times | GolfBookingsPage | Full page | Via Bookings list |
| `/golf/bookings/:bookingId` | GolfBookingDetailPage | (detail) | Golf | Booking detail | Confirm, check-in, complete, cancel | Booking, charges, payments | GolfBookingsPage | Full page | Via Bookings list |
| `/golf/guests` | GolfGuestsPage | Golf Guests | Golf | Guest management | Create, view | Guests, bookings | GolfBookingsPage | Full page | Top-level nav |
| `/golf/charges` | GolfChargesPage | Golf Charges | Golf | Golf charges | View, post | Charges, invoices | GolfBookingsPage | Full page | Top-level nav |

### Administration

| Route | Component | Nav Label | Domain | Purpose | Actions | Data | Related Screens | Type | Access Path |
|---|---|---|---|---|---|---|---|---|---|
| `/fiscalisation` | FiscalisationPage | Fiscalisation | Admin | Fiscal device management | View status, configure | Devices, statuses | — | Full page | Top-level nav |

---

## Navigation Structure (Current)

The current navigation is a flat list of 32 top-level items:

```
Dashboard
Members
Applications
Categories
Households
Fee Assessments
Products
Stock
Transfers
Counts
Adjustments
Wastage
Reorder Alerts
Suppliers
Point of Sale
Sales
Cashier Sessions
Golf Courses
Tee Sheet
Golf Bookings
Golf Guests
Golf Charges
Fiscalisation
Finance Overview
Chart of Accounts
Journals
Accounts Receivable
Accounts Payable
Payments
Financial Periods
Reports
Reconciliation
```

---

## Issues Identified

1. **Too many top-level items** — 32 items is excessive for a sidebar
2. **No grouping** — related functions are scattered (e.g., Golf Courses, Tee Sheet, Bookings, Guests, Charges are all separate)
3. **Product Categories missing** — no UI to create product categories
4. **Membership Categories placement** — currently top-level, should be under Membership
5. **Fiscalisation placement** — currently top-level, should be under Administration
6. **Reports placement** — currently under Finance, should be its own domain

---

## Screens Not Currently Exposed in Navigation

| Screen | Route | How Reached |
|---|---|---|
| Member Detail | `/members/:memberId` | Via Members list |
| Golf Booking Detail | `/golf/bookings/:bookingId` | Via Bookings list |
| New Golf Booking | `/golf/bookings/new` | Via Bookings list |

---

## Conclusion

The current UI has 32 top-level navigation items with no grouping. The PM-directed restructuring will consolidate these into 7 top-level domains with nested navigation, reducing clutter while preserving all functionality.
