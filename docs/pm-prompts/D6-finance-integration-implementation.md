# ClubStack D6 — Finance Integration — IMPLEMENTATION AUTHORIZED

D6 is now authorized for implementation.

The previous session correctly stopped because the specification package left 19
finance decisions undefined.

The PM has now resolved B1-B19.

The PM Decision Pack below is AUTHORITATIVE.

DO NOT reopen B1-B19.
DO NOT invent alternate accounting rules.
DO NOT ask the PM to decide these again.
DO NOT perform another design-only review.

Implement exactly the decisions below.

---

## PM DECISION PACK

### B1 Journal lifecycle

Draft → Posted
Draft → Cancelled

Posted journals are immutable.

Posted journals are never moved into a Reversed state.
A reversal is a new compensating journal linked by ReversalOfJournalId.

### B2 Account types

Asset
Liability
Equity
Revenue
Expense

### B3 Normal balances

Asset = Debit
Expense = Debit
Liability = Credit
Equity = Credit
Revenue = Credit

### B4 Correction/reversal

Create a new compensating journal.
Invert every debit/credit line.
Original remains immutable.
Reversal requires Finance.Journal.Reverse.
Reversal posts in an open period.

### B5 Add

Finance.Period.Reopen

Closed → Open is permitted only for the most recently closed period and requires
elevated authorization plus audit.

### B6 Period assignment

TransactionDate determines FinancialPeriod.

BusinessDate is retained as operational metadata.

### B7 Create

financial_posting_rules
financial_posting_rule_mappings

Mappings support rule + sequence + direction + account + optional selector.

Import the eight existing Phase 8 §14 posting rules using their documented
identities. Do not invent new rule identities.

### B8 JournalLine

Id
JournalId
LineNumber
FinancialAccountId
Direction
Amount
Currency
BaseAmount
Description
SourceLineId nullable

### B9 Journals support N >= 2 lines.

Debits must equal credits.

### B10 Phase 9 model wins

Account is on JournalLine.
Direction is Debit/Credit.

### B11 Finance API base

/api/v1/finance

Implement the approved finance routes from the PM Decision Pack.

### B12 References

FA-######
JRN-######
AR-######
AP-######
PAY-######
REC-######
PER-######

Use existing reference generation infrastructure.

### B13 AR

Open
PartiallyPaid
Paid
Cancelled

Canonical member identity remains the existing Member/Member Account.

AP mirrors AR for suppliers.

Use allocation records.

### B14 Add

Finance.AR.Manage
Finance.AP.Manage
Finance.Payment.Manage

Preserve the existing 13 finance permissions.

### B15 Reconciliation

Manual reconciliation only.
No bank API.
No bank feed.
Record statement balance, matched transactions, variance, reconciliation status.

### B16 Reports

Trial Balance
General Ledger
AR Aging
AP Aging
Cash Position
Financial Periods

AR/AP aging:

0-30
31-60
61-90
91+

Do not invent P&L/Balance Sheet implementations.

### B17 Currency

Club-level configurable base currency.

Journal carries transaction currency, base currency, exchange rate and rate date.

No external FX provider.

Same currency rate = 1.

Amount precision = decimal(19,4).
Exchange rate precision = decimal(19,6).

### B18 Tax-to-GL

Sales:
Dr Cash/AR gross
Cr Revenue net
Cr Tax Payable tax

Purchases:
Dr Expense/Inventory net
Dr Input Tax
Cr AP gross

Tax accounts configured through posting mappings.

No hard-coded ZIMRA tax account IDs.

### B19

No direct modification of posted transactions.

Closed-period corrections use reversal/compensating journals posted into an open
period.

---

## 1. AUTHORITATIVE CODEBASE PATTERNS

Correct the previous D6 prompt's two inaccurate assumptions.

Migrations are located at:

src/ClubStack.Infrastructure/Migrations/

NOT:

src/ClubStack.Infrastructure/Persistence/Migrations/

Authorization uses:

[Authorize(Policy = "Permission:" + Permissions.X)]

NOT:

RequirePermission

Do not create a new authorization abstraction.

Do not create a second migration folder.

## 2. MIGRATION

Create the first D6 EF Core migration in:

src/ClubStack.Infrastructure/Migrations/

Use real PostgreSQL.

Do not use EnsureCreated.

The existing migration assertion currently expects 10 migrations.

After creating the D6 migration, update:

tests/ClubStack.IntegrationTests/MigrationStartupBehaviorTests.cs

from the exact old count to the new count.

Do not weaken or remove the assertion.

## 3. CORE D6 MODEL

Implement the finance entities required by the PM Decision Pack and Phase 8:

- FinancialAccountType
- FinancialAccount
- FinancialPostingRule
- FinancialPostingRuleMapping
- Journal
- JournalLine
- FinancialPeriod
- AccountsReceivable
- AccountsReceivableAllocation
- AccountsPayable
- AccountsPayableAllocation
- Payment
- FinancialReconciliation

Use the existing ClubStack entity conventions:

- UUIDv7 IDs
- business references separate from IDs
- audit fields
- concurrency tokens
- inactivation instead of destructive deletion where applicable

## 4. JOURNAL INVARIANTS

Enforce:

- N >= 2 lines
- at least one debit
- at least one credit
- debits = credits
- posted journal immutable
- journal period must be Open
- journal reference unique
- duplicate source transaction prevented

Validate in Application/Domain logic.

Add the strongest feasible PostgreSQL-level constraints/triggers for invariants
that cannot be expressed by simple CHECK constraints.

Do not rely only on application validation.

## 5. PERIODS

Implement period lifecycle according to B5/B6/B19.

Required:

Open
Closed

Opening creates the active posting period.

Closing requires the period's required D6 checks.

Reopening only:

- latest closed period
- Finance.Period.Reopen
- reason
- audit

Do not allow a non-latest closed period to reopen.

## 6. POSTING RULES

Implement the eight existing Phase 8 §14 posting rules.

Do not invent their identities.

Read the exact rule definitions from the authoritative Phase 8 document before
seeding.

Map those rules through:

financial_posting_rule_mappings

Do not hard-code account IDs in posting services.

## 7. OPERATIONAL INTEGRATION

Integrate D6 with authoritative existing records.

At minimum implement the D6-defined finance effects for:

- POS sales
- member charges
- payments
- inventory purchases
- supplier invoices

Operational modules remain authoritative for their own transactions.

Finance becomes the authoritative accounting layer.

Do not duplicate member accounts, POS sales or inventory ledgers.

## 8. OUTBOX / TRANSACTION BOUNDARY

Respect the architecture decision:

Operational financial effects that must be atomic with their source transaction
are created in the same transaction.

Outbox is used for downstream reactions/integration where appropriate.

Follow the existing ClubStack OutboxProcessorJob pattern.

Never bypass the outbox where D6 explicitly requires it.

## 9. AR/AP

Implement AR/AP using the PM Decision Pack.

AR and AP must support:

- source reference
- original amount
- outstanding amount
- transaction date
- due date
- currency
- status
- allocations
- finance reference
- linked journal

Do not create duplicate customer/member masters.

Member identity remains the existing ClubStack Member/Member Account.

Supplier identity remains the existing Supplier.

## 10. PAYMENTS

Implement Finance.Payment.Manage.

Payments must integrate with the existing operational payment records.

Do not create a second POS tender ledger.

Finance Payment is the accounting representation and allocation mechanism.

Use idempotency and database uniqueness.

## 11. RECONCILIATION

Implement manual reconciliation.

No external bank APIs.

No automatic statement import.

The reconciliation must support:

- target financial account
- statement/as-of date
- external closing balance
- matched ledger entries
- variance
- final status
- actor/audit

Use:

Finance.Reconciliation.Perform

## 12. REPORTS

Implement only:

- Trial Balance
- General Ledger
- AR Aging
- AP Aging
- Cash Position
- Financial Periods

Each report must have a defined query source.

Do not create separate duplicated financial state solely for reporting.

All reports must derive from authoritative financial records.

## 13. API

Use:

/api/v1/finance

Use the exact routes from the PM Decision Pack.

Use:

[Authorize(Policy = "Permission:" + Permissions.X)]

No RequirePermission.

Use RFC7807 Problem Details.

Use the existing correlation-id and validation conventions.

## 14. UI

Implement the D6 finance navigation and required screens.

At minimum:

- Finance Overview
- Chart of Accounts
- Journals
- Accounts Receivable
- Accounts Payable
- Payments
- Financial Periods
- Reports
- Reconciliation

Journal screen must include:

Date
Reference
Description
Account | Debit | Credit
Validate
Save Draft
Post

Use the existing ClubStack SPA architecture and permission-gated navigation.

## 15. PERMISSIONS

Existing permissions remain authoritative.

Add only:

Finance.Period.Reopen
Finance.AR.Manage
Finance.AP.Manage
Finance.Payment.Manage

Use the existing Permission catalogue pattern.

Do not duplicate permissions.

Ensure all new permissions are registered, seeded and tested.

## 16. TESTING

Use real PostgreSQL integration tests.

No SQLite substitute.

No EnsureCreated.

Follow the proven fixture pattern:

[Collection("Database")]
DatabaseTestBase
real EF migrations
scratch database
real OutboxProcessorJob
provider scope factory

Test:

- journal balancing
- account types
- normal balances
- period assignment
- period close
- period reopen authorization
- posted journal immutability
- reversal
- closed-period correction
- posting-rule mapping
- duplicate source prevention
- idempotency
- concurrency
- AR
- AP
- allocations
- payments
- reconciliation
- every required report
- authorization
- audit
- API routes
- POS → Finance
- member charge → Finance
- payment → Finance
- inventory purchase → Finance
- supplier invoice → Finance

## 17. SECURITY

Never store:

- database password
- fiscal activation key
- fiscal license
- other secrets

in source, reports, tests, audit metadata or API responses.

The existing local database credential is already persisted on the development
machine.

DO NOT ask the user for it again.

## 18. REGRESSION

D5.5 is accepted and must remain green.

Run:

dotnet build ClubStack.slnx

dotnet test ClubStack.slnx

npm run build

Also run:

npx tsc --noEmit

Expected:

0 warnings
0 errors
0 failed tests

Do not count skipped tests as equivalent to executed tests in the session
report.

## 19. DOCUMENTATION

Update the appropriate D6 documentation.

Do not modify the original Phase 0-9 authoritative documents.

Create/update the PM implementation documentation as appropriate.

Record:

- entities
- permissions
- routes
- migrations
- posting rules
- accounting decisions
- test approach

## 20. SESSION REPORT

Create:

D6_Implementation_Session_Report.md

It must contain:

1. Objective
2. PM Decision Pack used
3. Authoritative documents read
4. Exact scope implemented
5. Files created
6. Files modified
7. Database schema
8. Migration
9. Domain
10. Application
11. Infrastructure
12. API
13. Permissions
14. Audit
15. Outbox
16. Posting engine
17. AR/AP
18. Payments
19. Reconciliation
20. Reports
21. UI
22. Security
23. Tests
24. Exact test counts
25. Build
26. SPA
27. TypeScript
28. Regression
29. Defects discovered
30. Defects fixed
31. Known limitations
32. D6 acceptance status
33. Exact next-session entry point

Clearly distinguish:

CURRENT SESSION

from

PRIOR ACCEPTED WORK.

Do not claim any behavior was proven unless it was actually tested.

## 21. STOP CONDITIONS

Do not invent any additional accounting policy.

If implementation reveals a genuine contradiction NOT resolved by B1-B19:

STOP.

Record the exact contradiction and the affected code path.

Do not silently choose.

Otherwise, implement the D6 Decision Pack completely.

## 22. FINAL INSTRUCTION

D6 coding authorization is NOW GRANTED.

Do not perform another design-only review.

Do not stop because the original specification lacked the decisions. The PM
Decision Pack has supplied them.

Implement D6.

Use the real codebase patterns.

Use the correct migration path.

Use the existing authorization pattern.

Use the persistent local PostgreSQL configuration.

Do not ask for the database password.

Do not touch the FiscalStackZW DLL.

Do not reopen D5.5.

Do not start D7.

When D6 implementation is complete, run the complete regression suite, write the
session report, and STOP.
