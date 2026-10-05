# D6 Implementation Session Report

> **STATUS: D6 IMPLEMENTED. 707/707 tests pass, solution builds with 0 warnings and 0 errors, SPA builds, TypeScript clean.**
>
> This report supersedes the previous session's "STOPPED — D6 NOT IMPLEMENTED" report. That
> earlier report was correct for its time: the specification package did not define the
> finance decisions, and the specification withholds coding authorization for the finance
> slice. The PM Decision Pack (`docs/pm-prompts/D6-finance-integration-implementation.md`,
> decisions B1–B19) subsequently supplied those decisions and granted coding authorization,
> so this session implemented the slice.

Throughout, **CURRENT SESSION** means work performed under that authorization, and
**PRIOR ACCEPTED WORK** means code that already existed and was only read, wired or extended.
No prior D6 finance code existed to preserve: the previous session created no migration,
entity, endpoint, permission or screen.

---

## 1. Objective

Implement D6 Finance Integration per PM Decision Pack B1–B19: a double-entry chart of
accounts, financial periods, journals, a mapping-driven posting engine, accounts receivable,
accounts payable, payments with allocation, manual reconciliation, six finance reports, the
API surface, the SPA screens, and integration with the existing D3 member-account and D5
POS/cashiering records.

## 2. PM Decision Pack used

`docs/pm-prompts/D6-finance-integration-implementation.md` — decisions B1–B19 plus its
architecture, API, UI, testing and reporting requirements. Where the mandate said "use the
exact routes from the PM Decision Pack" and no enumerated route list existed in either the
mandate or the specification (§11 route tables stop at Facilities), the routes follow the
conventions every other module in the codebase already uses under `/api/v1/`: collection
`GET`/`POST`, item `GET`/`PUT`, and `POST {id}/{action}` for state transitions. This is
recorded here because it is an interpretation, not a quoted instruction.

## 3. Authoritative documents read

| Document | Location | Used for |
| --- | --- | --- |
| D6 Finance Integration Implementation | `docs/pm-prompts/D6-finance-integration-implementation.md` | B1–B19, API/UI/report requirements |
| Phase 8 Detailed Pre-Coding Specifications | `..\ClubStack_Phase_8_Detailed_Pre_Coding_Specifications.md` | §14 posting rules, §15 posting requirements, §16 reporting contract, §11 API conventions, §21 stop condition |
| Phase 2 Requirements Specification | `..\ClubStack_Phase_2_Requirements_Specification.md` | FIN-*, PAY-*, AR-*, AP-* requirement identifiers |

The authoritative specification set was read in full by the **prior** session, which is how
the absence of finance mechanics and of a finance route table was established. This session
re-read the D6 mandate and the §14/§15/§16/§21/§22 sections of the Phase 8 specification it
implemented against.

## 4. Exact scope implemented

Implemented: chart of accounts with base currency and posting-rule mappings; financial
period open/close/reopen; journal draft/validate/post/cancel/reverse; a mapping-driven
posting engine idempotent per `(SourceType, SourceId)`; AR and AP creation, allocation and
compensation-based cancellation; payments with allocation and idempotency; manual
reconciliation; Trial Balance, General Ledger, AR Aging, AP Aging, Cash Position and
Financial Periods reports plus a finance overview; the `/api/v1/finance` API; nine SPA
screens; and the D3/POS outbox finance handlers.

Deliberately **not** implemented, and why: inventory-receipt auto-posting (§14.5). The
mandate's own summary of §14.5 defers the posting timing to club procurement policy, so
`INVENTORY-PURCHASE` remains a seeded, configurable rule with no automatic emitter. Also not
implemented: any P&L or Balance Sheet report (B16 forbids inventing them), any bank feed or
bank API (B15 is manual reconciliation only), and any external FX provider (B17).

## 5. Files created

**Domain** — `src/ClubStack.Domain/Finance/FinanceStates.cs`, `FinanceDomain.cs`,
`FinanceReferences.cs`.

**Application** — `src/ClubStack.Application/Finance/FinanceContracts.cs`,
`FinancialPostingRules.cs`, `FinanceEvents.cs`.

**Infrastructure** — `src/ClubStack.Infrastructure/Finance/`: `FinanceServiceHelpers.cs`,
`EntityFrameworkFinancialAccountService.cs`, `EntityFrameworkFinancialPeriodService.cs`,
`EntityFrameworkJournalService.cs`, `EntityFrameworkFinancePostingService.cs`,
`EntityFrameworkReceivableService.cs`, `EntityFrameworkPayableService.cs`,
`EntityFrameworkFinancePaymentService.cs`, `EntityFrameworkFinancialReconciliationService.cs`,
`EntityFrameworkFinanceReportService.cs`, `FinanceReferenceSeeder.cs`, `FinanceOutboxHandlers.cs`,
`FinanceRequirementWriter.cs`.

**Migration** — `src/ClubStack.Infrastructure/Migrations/20260927093441_D6_FinanceIntegration.cs`
and its `.Designer.cs`, plus `Migrations/D6FinanceIntegrity.cs` (raw SQL, see §8).

**API** — `src/ClubStack.Api/Contracts/D6Contracts.cs`;
`Controllers/FinancialAccountsController.cs`, `FinancialPeriodsController.cs`,
`JournalsController.cs`, `ReceivablesController.cs`, `PayablesController.cs`,
`FinancePaymentsController.cs`, `ReconciliationsController.cs`, `FinanceReportsController.cs`.

**Tests** — `tests/ClubStack.IntegrationTests/D6FinanceIntegrityTests.cs`;
`tests/ClubStack.Api.Tests/D6FinanceAuthorizationTests.cs`, `D6FinanceStubs.cs`.

**Client** — `client/clubstack-ui/src/pages/`: `FinanceOverviewPage.tsx`,
`ChartOfAccountsPage.tsx`, `JournalsPage.tsx`, `ReceivablesPage.tsx`, `PayablesPage.tsx`,
`PaymentsPage.tsx`, `FinancialPeriodsPage.tsx`, `FinanceReportsPage.tsx`,
`ReconciliationPage.tsx`.

## 6. Files modified

`src/ClubStack.Application/Authorization/Permissions.cs` (four permissions plus catalogue
rows), `src/ClubStack.Infrastructure/Persistence/Entities/FinanceEntities.cs` (new entities
in the shared entity file), `src/ClubStack.Infrastructure/Persistence/ClubStackDbContext.cs`
(finance DbSets, mappings, indexes, foreign keys), `src/ClubStack.Infrastructure/Persistence/IUnitOfWork.cs`,
`src/ClubStack.Infrastructure/InfrastructureServiceCollectionExtensions.cs`,
`src/ClubStack.Infrastructure/Bootstrap/BootstrapService.cs`,
`src/ClubStack.Infrastructure/Accounts/EntityFrameworkMemberAccountService.cs`,
`src/ClubStack.Infrastructure/Membership/EntityFrameworkMembershipFeeService.cs`,
`src/ClubStack.Infrastructure/Pos/EntityFrameworkSaleService.cs`,
`src/ClubStack.Application/Pos/PosEvents.cs`,
`tests/ClubStack.IntegrationTests/MigrationStartupBehaviorTests.cs` (expected migration count
10 → 11), `client/clubstack-ui/src/types.ts`, `client/clubstack-ui/src/App.tsx`,
`client/clubstack-ui/src/layout/Layout.tsx`.

## 7. Database schema

Fourteen tables created: `financial_accounts`, `financial_periods`, `financial_posting_rules`,
`financial_posting_rule_mappings`, `journals`, `journal_lines`, `accounts_receivable`,
`accounts_receivable_allocations`, `accounts_payable`, `accounts_payable_allocations`,
`payments`, `finance_payment_allocations`, `financial_reconciliations`,
`financial_reconciliation_matches`.

Precision follows B17: amounts `decimal(19,4)`, exchange rates `decimal(19,6)`. Journal lines
carry `Amount` + `Direction` (not separate debit/credit columns), and posting-rule mappings
carry `FinancialAccountId` + `Direction` + `Sequence` + optional `Selector`.

## 8. Migration

`20260927093441_D6_FinanceIntegration` is the only schema migration. It also invokes
`D6FinanceIntegrity`, which adds database-level guarantees that EF cannot express and which
therefore live outside the model snapshot: unique indexes on references, on
`(Year, Month)` for periods, on `(SourceType, SourceId)` for journals and documents, on
payment idempotency keys, on payment source identity, and on one reversal per original
journal; `CHECK` constraints for reference formats, positive amounts, valid enum values and
non-negative allocations; a deferred constraint trigger rejecting any journal that is not
balanced or has fewer than two lines; a trigger rejecting any mutation of a posted journal;
a trigger rejecting a journal line written outside the transaction that set its parent to
Posted; and a trigger rejecting postings into a closed period.

`journals.PostingTransactionId` is added by raw SQL and is deliberately absent from the EF
model. It is stamped by the database with `txid_current()` on the transition to Posted; the
line trigger compares the parent stamp against `txid_current()` and refuses the write when
they differ, which is how "the journal and its lines share one transaction" is enforced
rather than assumed.

## 9. Domain

`FinanceStates.cs` defines the enums and their values: `JournalStatus` Draft=0/Posted=1/
Cancelled=2; `FinancialPeriodStatus` Open=0/Closed=1; `JournalDirection` Debit=0/Credit=1;
`FinancialAccountType` Asset=0…Expense=4; `NormalBalance` Debit=0/Credit=1;
`FinancialAccountStatus` Active=0/Inactive=1; `FinancialCashKind` None=0/CashOnHand=1/Bank=2;
`ReceivableStatus` and `PayableStatus` Open=0/PartiallyPaid=1/Paid=2/Cancelled=3;
`PaymentDirection` Received=0/Made=1; `FinancialReconciliationStatus` InProgress=0/Completed=1/
Cancelled=2. `FinanceDomain.cs` holds amount normalization, balance, period-window, FX-rate
and aging rules. `FinanceReferences.cs` holds the reference formats.

## 10. Application

`FinanceContracts.cs` defines nine service interfaces — chart of accounts, journals, periods,
posting engine, receivables, payables, payments, reconciliation, reports — with no persistence
or HTTP concerns. `FinancialPostingRules.cs` defines the eight §14 rule identities with their
strategies, selectors and source-section provenance. `FinanceEvents.cs` defines the D3
member-payment and membership-fee finance events.

## 11. Infrastructure

EF Core implementations of all nine interfaces. Every mutating operation runs inside one
transaction through the existing `IUnitOfWork`: a payment records the payment, allocates it,
moves the document statuses, posts the cash/bank journal and saves, or rolls back. Financial
accounts are deactivated, never deleted. Journals are immutable once posted; the only
correction path is a compensating reversal journal.

## 12. API

Eight controllers under `/api/v1/finance`: `accounts` (chart, base currency, posting rules
and mappings), `periods`, `journals`, `receivables`, `payables`, `payments`, `reconciliations`
and `reports` (overview, trial-balance, general-ledger, receivable-aging, payable-aging,
cash-position, periods). Every route carries `[Authorize(Policy = "Permission:" + Permissions.X)]`
as required by §13; no `RequirePermission` is used; errors are RFC7807 problem details via
the existing exception middleware; requests validate through the existing model-validation and
correlation-id conventions. The raw posting engine is deliberately **not** exposed over HTTP:
posting happens through operational flows and controlled journal commands, so that no client
can bypass the posting rules.

## 13. Permissions

Four permissions added, each marked sensitive in the catalogue because each is a finance
action that is always audited: `Finance.Period.Reopen` (Closed → Open, most recent period
only, reason required), `Finance.AR.Manage`, `Finance.AP.Manage`, `Finance.Payment.Manage`.
Thirteen pre-existing finance permissions were retained unchanged. All seventeen finance
permissions are registered as policies by the application's own authorization setup, and the
D6 API tests assert each of them against every finance route.

## 14. Audit

Every finance command records an audit entry through the existing `RecordAuditAsync` pipeline
on `AuditedControllerBase`, carrying action, entity type, entity id, reference, the actor from
the authenticated principal, and the request correlation id. Post, close, reopen, reverse,
cancel, allocation, base-currency change and posting-rule mapping changes all record the
figures or reasons involved as metadata.

## 15. Outbox

Handlers consume the existing transactional outbox: POS cash sale (§14.1/§14.2) posts the
cash or member-receivable journal, POS refund (§14.8) reverses every journal the sale posted
and cancels the receivable with a reason rather than posting a second entry, member-account
payment (§14.3) and membership fee assessment (§14.4) post their journals. Handling is
idempotent per `(SourceType, SourceId)`, so a replayed event returns the existing journal
instead of double posting. Inventory receipt (§14.5) has no emitter by design.

## 16. Posting engine

`EntityFrameworkFinancePostingService` resolves the rule's configured mappings, builds the
journal from the supplied amount components and selectors, verifies balance, resolves the
period from the transaction date, refuses a closed period, writes journal and lines in one
transaction, and returns the existing journal when `(SourceType, SourceId)` has already been
posted. Tax handling for §14.1/§14.6/§14.7 is driven by mappings (`tax`, `net`, `gross`
selectors) with no hard-coded tax account id, per B18.

## 17. AR/AP

Receivables and payables are documents, not balances: each links to its member or supplier,
its source operation and its journal. Creating one posts the GL side in the same transaction.
Allocation moves the document through Open → PartiallyPaid → Paid. Cancellation writes a
compensating journal and keeps the document, per B19 — no destructive delete exists on either
type. Over-allocation is rejected.

## 18. Payments

One call records the payment, validates the counterparty against the direction, validates
each allocation, posts the cash/bank journal, allocates, updates document statuses and saves
in a single transaction. It is idempotent both on the supplied idempotency key and on
`(SourceType, SourceId)`; a lost race on either unique index is caught and replayed rather
than surfaced as a 500.

## 19. Reconciliation

Manual only. A reconciliation is created for a cash or bank account with a statement date and
closing balance, journals are matched one by one, and completion records the statement balance,
the computed ledger balance and the variance. There is no bank feed, import or API.

## 20. Reports

Trial Balance (per account, with totals and difference), General Ledger (per account, with
opening/closing balance), AR Aging and AP Aging (buckets 0–30, 31–60, 61–90, 91+),
Cash Position (per account classified by `CashKind`, so no account code is hard-coded), and
Financial Periods. Every figure is derived from posted journals at query time; no duplicated
financial state was created for reporting. `asOf` defaults to now, so a cash position or aging
report excludes future-dated postings unless an explicit `asOf` is supplied.

## 21. UI

Nine screens, each gated on its own permission, each showing a plain-language message rather
than a broken page when the permission is absent: Finance Overview, Chart of Accounts
(including base currency and posting-rule mappings), Journals, Accounts Receivable, Accounts
Payable, Payments, Financial Periods, Reports and Reconciliation. The journal screen presents
the mandated entry grid — Date, Reference, Description, Account, Debit, Credit. Actions are
offered only in the states where they are legal: Post on drafts, Reverse on posted journals,
Close on open periods, Reopen on closed periods, Complete on in-progress reconciliations.
Receivables and payables are read-and-cancel screens because those documents are raised by
operational events, which the screens state explicitly.

## 22. Security

Every finance route is authenticated and permission-gated; the D6 API tests assert 401 for
anonymous, 403 when the specific permission is withheld, and success when it is held — across
all 50 finance routes, including that a cashier with fiscalisation and POS permissions reaches
none of them. Sensitive finance actions are separated: journal view cannot post, journal post
cannot reverse, period close cannot reopen, AR manage cannot touch payables, payment manage
cannot manage the chart of accounts, and report view cannot open a period. No secret is echoed
in any response. No finance route exposes the raw posting engine.

## 23. Tests

`D6FinanceIntegrityTests` — 20 PostgreSQL integration tests over the real database proving:
balanced-journal enforcement, single-line rejection, duplicate-source rejection, closed-period
rejection, posted-journal immutability, out-of-transaction line rejection, period
open/close/reopen including the empty-period close rejection, AR and AP lifecycles through
allocation, payment allocation and over-allocation rejection, payment idempotency by key and
by source, reversal as a linked compensating journal, reconciliation, report correctness
including the cash classification and trial balance, reference-format enforcement on every
service, and account deactivation.

`D6FinanceAuthorizationTests` — 158 new cases over the real controllers and the real
`Permission:*` policies: 50 routes × anonymous/withheld/allowed = 150, plus 8
separation-of-duty and cashier-isolation cases. With the 40 pre-existing cases, the API test
project now runs 198 tests.

## 24. Exact test counts

| Project | Passed | Failed | Skipped | Total |
| --- | --- | --- | --- | --- |
| ClubStack.Domain.Tests | 220 | 0 | 0 | 220 |
| ClubStack.Application.Tests | 80 | 0 | 0 | 80 |
| ClubStack.IntegrationTests | 209 | 0 | 0 | 209 |
| ClubStack.Api.Tests | 198 | 0 | 0 | 198 |
| **Total** | **707** | **0** | **0** | **707** |

The pre-D6 baseline was 529 passing tests. This session added 178: 20 D6 finance integration
tests and 158 D6 finance authorization cases.

## 25. Build

`dotnet build ClubStack.slnx` → **Build succeeded. 0 Warning(s), 0 Error(s).**

## 26. SPA

`npm run build` (Vite) → **✓ built**, `dist/index.html` 0.39 kB, `dist/assets/index-DpQwXAk3.css`
3.77 kB, `dist/assets/index-zQ_QzaaM.js` 464.89 kB (gzip 117.10 kB).

## 27. TypeScript

`npx tsc --noEmit` → **exit 0**, no diagnostics.

## 28. Regression

`dotnet test ClubStack.slnx` against the persistent local PostgreSQL configuration →
**707 passed, 0 failed, 0 skipped.** D5.5 fiscalisation, D3 member accounts, D4 inventory and
D5 POS/cashiering tests all still pass; no D5.5 or earlier work was modified, reopened or
reverted. `FiscalStackZW.dll` was not touched.

## 29. Defects discovered

1. **EF Core state bug in payment allocation (product defect).** A new allocation entity added
   only to a tracked, Unchanged receivable's navigation collection was tracked as `Modified`
   rather than `Added`, because EF decides Added-vs-Modified by whether the key is already set.
   Every payment allocation therefore failed with
   `DbUpdateConcurrencyException: expected to affect 1 row(s), but actually affected 0 row(s)`.
2. **Payable allocations were never counted (product defect).** The payable branch of
   `RecordAsync` failed to add to `allocatedTotal`, so a payment could over-allocate a payable
   and still report the full amount unapplied. This was a real accounting-validation hole, not
   a test artifact.
3. **`EfUnitOfWork.RollbackAsync` masked root causes (infrastructure defect).** Detaching
   entities one at a time threw `The association between 'X' and 'Y' has been severed` on
   graphs with multiple dependents, replacing the real exception with a misleading one.
4. **Untranslatable report query (product defect).** `Dictionary.ContainsKey` in the
   Trial Balance LINQ could not be translated by Npgsql, failing the report at runtime.
5. **Trigger and schema mismatches in `D6FinanceIntegrity` (migration defect).** The balance
   trigger was attached to both `journals` and `journal_lines` but assumed the `journals` shape,
   so it failed with `record "new" has no field "JournalId"`; the SQL also referenced
   non-existent `DebitAmount`/`CreditAmount` columns and wrong enum values (`Posted = 2`).
6. **Test defects.** A cash-position assertion omitted `asOf` against deliberately future-dated
   data; a period lifecycle test closed a period with no posted journals, which B5 correctly
   refuses; a reversal test relied on another test having opened the current period; and a
   single-transaction assertion compared the committed `PostingTransactionId` against
   `txid_current()` on a *new* connection, which can never match.
7. **Route table absent (specification defect, non-blocking).** §11's route tables stop at
   Facilities and neither the mandate nor the specification enumerates the finance routes. The
   §21 stop condition is scoped to accounting-policy contradictions, and the mandate's §22
   forbids stopping for missing specification detail, so the routes were derived from the
   codebase's own module conventions and recorded in §2.

## 30. Defects fixed

All seven were fixed and are now covered by a passing test: (1) allocations are explicitly
added to their `DbSet` so EF tracks them as `Added`; (2) `allocatedTotal` now includes
payable allocations; (3) rollback uses `ChangeTracker.Clear()`; (4) the report materializes the
account-id list and uses `Contains`; (5) the balance trigger branches on `TG_TABLE_NAME`/`TG_OP`
and the SQL matches the real schema and enum values; (6) the four test defects were corrected
to assert the real contract, including proving single-transaction writes by asserting the stamp
is present and that an out-of-transaction line insert is rejected. (7) was resolved by
following the codebase's established route conventions and documenting the interpretation.

## 31. Known limitations

- The nine SPA screens compile, build and follow the codebase's patterns, but they have never
  been exercised against a running API in a browser. Field-level mismatches would only surface
  at runtime.
- D4 inventory-receipt auto-posting is intentionally absent (§14.5 timing policy deferred);
  `INVENTORY-PURCHASE` exists as a seeded, configurable rule with no emitter.
- The chart of accounts and posting-rule mappings are club configuration. Only the eight rule
  identities and the starter currency are seeded; a club must configure accounts and mappings
  before operational posting produces meaningful journals.
- D3 member-payment and membership-fee outbox emission, and the POS refund receivable
  cancellation, are implemented and wired but are not covered by a dedicated automated
  end-to-end test in this session; they are exercised only indirectly.
- Reconciliation has no statement import; balances are entered by hand (B15).

## 32. D6 acceptance status

**Accepted.** B1–B19 are implemented, the full regression suite passes with zero failures, the
solution builds with zero warnings, and the SPA builds with clean TypeScript. No accounting
policy was invented: the two places where a decision was genuinely absent (the §14.5 inventory
timing and the finance route table) were resolved by deferring to the specification and to the
codebase's own conventions respectively, and both are recorded above rather than buried.

## 33. Exact next-session entry point

D6 is complete. Do not reopen it. The next session should start at D7 by reading
`docs/pm-prompts/README.md` to determine which prompt governs it, then:

1. If D7 touches finance, read §2 of this report first — the eight posting rules, the mapping
   contract and the four sensitive permissions are the integration surface.
2. Verify the SPA against a running API before treating the finance screens as user-accepted;
   this is the one claim in this report that rests on compilation rather than execution.
3. Configure a club's chart of accounts and posting-rule mappings through
   `PUT /api/v1/finance/accounts/posting-rules/{ruleCode}/mappings` before any operational
   posting is expected to produce a meaningful journal.
