# ClubStack D5 Continuation Session Report

**Phase:** D5 — POS & Cashiering (continuation / hardening + SPA)
**Date:** 2026-09-25
**Repo root:** `C:\Users\TIMIRE\Desktop\Club Management\ClubStack`
**Supersedes:** nothing. `D5_Session_Report.md` (the original 18-section D5 report)
is retained as the prior-session record; this document is the continuation
deliverable required by the PM's 13-part directive.

---

## 1. Session objective

Close the seven open risks published in `D5_Session_Report.md` §14 and build the
missing POS SPA, per the PM's 13-part continuation directive. Specifically:
pre-allocate references before the transaction opens, map concurrency failures to
409, harden idempotency scoping, configure the missing foreign keys, enforce the
one-active-session-per-register invariant in the database, expand D5 test
coverage, build `/pos`, `/sales` and `/cashier-sessions`, then run a full
regression and report against the mandatory 33-point template.

Held throughout: fix real code at the root cause; never weaken or delete a test
to reach green; do not invent product rules; do not start D6/D7.

## 2. Current D5 baseline entering the session

| Metric | Value |
| --- | --- |
| Solution build | succeeded — 0 warnings, 0 errors |
| Tests passing | **349** — 0 failed |
| Domain.Tests | 220 |
| Application.Tests | 1 |
| Api.Tests | 1 |
| IntegrationTests | 127 |
| EF migrations | 6 |
| D5-specific tests | 3 (`D5PosCashieringApiTests.cs`) |
| POS SPA | absent — no `/pos`, `/sales`, `/cashier-sessions` routes or nav |
| Documented open risks | 7 (`D5_Session_Report.md` §14) |

The four production defects fixed in the prior session (EF `Modified`-promotion
on new child rows ×2, the expected-cash sign double-negation, and the unguarded
double-`SaveChanges`) were already green and were carried in as a fixed baseline.

## 3. Scope

Thirteen directed work items, all completed:

1. Reference generation before `BeginAsync`.
2. `DbUpdateConcurrencyException` → HTTP 409.
3. Idempotency hardening (operation + actor + request-hash scoping, failed-key
   re-acquisition, clean tracker after rollback).
4. Missing FK configuration and `DiscountType` nullability.
5. One active cashier session per register, enforced by a partial unique index
   and surfaced as 409.
6. D5 integration test expansion (+15 tests).
7–11. POS SPA: product search, current sale, cashiering screens, sales history,
   routing and permission-gated navigation.
12. Full solution build, full regression against real PostgreSQL, SPA build.
13. This report.

## 4. Out of scope

- **D6/D7 — not started**, per standing instruction.
- **Variance-approval endpoint — deliberately not built.** Phase 8 §12 defines no
  route for it (see §27).
- No architectural redesign; no new product/financial/tax rules.
- No automated SPA test suite (no test runner exists in `package.json`); the SPA
  is verified by strict `tsc --noEmit` plus a production Vite build.
- No CI/CD pipeline work; the `wwwroot` publish step remains manual (see §26).
- No commit, branch, or repository initialisation — this workspace is not a git
  repository, and committing was not requested.

## 5. Specification sources used

Specification documents are **not** in the repository; they live in the parent
directory `C:\Users\TIMIRE\Desktop\Club Management\`.

- **Phase 2** — sales and cashiering requirement ranges; `CON-005` (one active
  session per register).
- **Phase 3** — pricing, tax treatment, and the cash-movement sign convention.
- **Phase 4** — `PosCashMath.ExpectedCashOnHand`, variance formula, reference
  prefixes and widths.
- **Phase 5 §43** — cashiering screens: Current Session, Open Session, Cash
  Movements, Close Session, Reconciliation, History; the POS screen's
  Product Search + Current Sale with `[Cash] [Card] [Account] [Split] [Complete
  Sale]`.
- **Phase 6 §11** — the D5 slice definition and D4 exclusions.
- **Phase 7** — POS/cashiering permission descriptors.
- **Phase 8** — §5 audit conventions, §7.4 pricing, §9 one-active-session
  constraint, §11 the 409 conflict convention, §12 route catalogue, §13 the
  "authenticated actor and operation context" idempotency-scoping requirement.
- **Phase 9** — consistency audit.
- `D0_Implementation_Package.md`, `D2_Market_Reconciliation...md` — prior-phase
  decisions.
- `D5_Session_Report.md` — the prior-session record and its §14 open-risk list.
- `docs/pm-prompts/` — this session's archived PM directive and report template.

## 6. Decisions followed

- **One active session per register is a database-level requirement**, not merely
  an application check (Phase 8 §9 + Phase 2 `CON-005`), so it is enforced by a
  partial unique index over the non-terminal statuses.
- **Idempotency is scoped to the authenticated actor and the operation context**
  (Phase 8 §13). A globally unique key is non-conformant and was replaced with a
  composite `(IdempotencyKey, Operation, ActorUserId)` unique index.
- **409 is the established conflict convention** (Phase 8 §11), so both
  concurrency failures and the register-session collision return 409.
- **The request hash must include the target aggregate id.** An earlier
  implementation hashed the payload only; the same key plus the same tenders
  against a *different* sale replayed the wrong stored result. The hash is now
  computed over `"{id:N}|{payload}"`.
- **Two distinct entities are both named "location"** — `LocationEntity`
  (`locations`, club-level) and `InventoryLocationEntity` (`inventory_locations`).
  `SaleEntity.LocationId` receives `register.InventoryLocationId` and is compared
  against `StockMovementEntity.LocationId`, so its FK targets `inventory_locations`.
- **A cashier session cannot close from `Open`.** It becomes `Active` on its
  first completed sale; close requires `Active`. This is existing domain
  behaviour, preserved and asserted rather than worked around.
- The expected-cash sign convention established in the prior session is held:
  `SignedAmount` is the signed drawer contribution exposed on the DTO, while the
  aggregation sums positive magnitudes and applies the sign in the domain formula.
- SPA styling uses only the CSS classes that actually exist in `styles.css`. The
  pre-existing `ProductsPage` uses two non-existent classes (`.btn-secondary`,
  `.status-pill.good`); those were deliberately **not** copied.

## 7. Backend changes

- `EntityFrameworkSaleService` — pre-resolves `memberAccountId` and pre-allocates
  all charge references before `BeginAsync`; refund pre-allocates credit
  references and the refund account id; `foreach` → indexed `for` so the
  pre-allocated array is addressable; extracted `CompleteSaleCoreAsync` and
  `RefundSaleCoreAsync` so an outer `try/catch` wraps *all* post-acquire work;
  added `MarkIdempotencyFailedAsync`, `ReplayStoredOrConflict<T>`, and the
  operation constants `pos.sale.complete` / `pos.sale.refund`.
- `EntityFrameworkCashierSessionService` — `OpenAsync` catches the partial-unique
  index violation and converts it to a 409 `ConflictException`; added
  `IsUniqueViolation(DbUpdateException)` and `using Npgsql;`.
- `EntityFrameworkMemberAccountService` — `ChargeAsync` honours `input.Reference`
  else `CHG-`; `CreateCreditAsync` honours `input.Reference` else `CRT-`;
  `GetOrCreateAccountAsync` and `CreateCreditAsync` guard `SaveChangesAsync` on
  `CurrentTransaction is null`; added `CreditReferencePrefix = "CRT"`.
- `EfOutboxStore.EnqueueAsync` — `SaveChangesAsync` guarded on
  `CurrentTransaction is null`.
- `IUnitOfWork.RollbackAsync` — detaches **all** tracked entries after rollback.
- `GlobalExceptionHandler` — new `DbUpdateConcurrencyException` → 409 branch with
  a `LogWarning`; the pre-existing `ConcurrencyException` → 409 mapping is intact.
- **New** `EfIdempotencyStore` — the first infrastructure implementation of the
  scoped, hash-aware store.

## 8. Database/model/migration changes

Two migrations were added; the migration count moved 6 → 8.

**`20260925105931_D5ContinuationIdempotencyScoping`**
- Drop `IX_idempotency_records_IdempotencyKey`.
- Add `Operation` (varchar 128, required, default `""`).
- Add `ActorUserId` (varchar 128, required, default `""`).
- Add `RequestHash` (varchar 64, nullable).
- Create `IX_idempotency_records_IdempotencyKey_Operation_ActorUserId` (unique).

**`20260925111433_D5ContinuationReferentialIntegrity`**
- Six foreign keys, all `ON DELETE RESTRICT`, each with its supporting index:
  `sales.PosOutletId → pos_outlets`,
  `sales.LocationId → inventory_locations`,
  `sale_lines.ProductId → products`,
  `sale_payments.PaymentMethodId → payment_methods`,
  `sale_payments.AccountTransactionId → account_transactions`,
  `sale_discounts.LineId → sale_lines`.
- Alter `sale_lines.DiscountType` to **nullable**, matching the entity and the
  original D5 migration (an undiscounted line stores `NULL`).
- Create filtered unique index `IX_cashier_sessions_RegisterId` with
  `filter: "\"Status\" IN (0, 1, 2)"` — i.e. at most one `Open`/`Active`/`Closing`
  session per register.

Entity changes: `IdempotencyRecordEntity` gained `Operation`, `ActorUserId`,
`RequestHash`; `SaleEntity` gained `PosOutlet` and `Location` navigations;
`SaleLineEntity` gained `Product`; `SalePaymentEntity` gained `PaymentMethod` and
`AccountTransaction`; `SaleDiscountEntity` gained `Line`.

`ClubStackDbContextModelSnapshot.cs` was regenerated to match.

## 9. Application/domain changes

- `AccountContracts.cs` — `CreditInput` gained a trailing optional
  `string? Reference = null` (trailing so no existing positional construction
  breaks).
- `Idempotency.cs` — `StoredIdempotencyResult` gained `Operation`, `ActorUserId`,
  `RequestHash`; new `public static class IdempotencyRequestHash { Compute(string) }`
  returning a 64-character lowercase SHA-256 hex digest; every
  `IIdempotencyStore` method now takes `operation` and `actorUserId`, and
  `TryAcquireAsync` additionally takes `string? requestHash`.
- `ClubStackDbContext.ConfigureIdempotencyRecords` — composite unique index;
  `RequestHash` max length 64.
- `ConfigureCashierSessions` — the filtered partial unique index on `RegisterId`.
- `ConfigureSaleLines` — removed `.IsRequired()` from `DiscountType`.
- **No Domain-layer changes.** The POS/cashiering state machines, the cash
  formulas and the reference formats are unchanged; this session fixed
  infrastructure and application defects rather than altering financial rules.

## 10. API/controller changes

No controller signatures, routes, or HTTP contracts were changed. The D5 surface
is exactly as it was, and the SPA was built against it:

- `PosController` — `[Route("/api/v1/pos")]`: `GET products`,
  `GET products/members`, `GET configuration`, `POST outlets`, `POST registers`.
- `SalesController` — `[Route("/api/v1/sales")]`: `POST` (create), `GET` (search),
  `GET {id}`, `POST {id}/complete`, `POST {id}/void`, `POST {id}/refund`,
  `GET {id}/receipt`.
- `CashierSessionsController` — `[Route("/api/v1/cashier-sessions")]`: `POST`,
  `GET`, `GET {id}`, `POST {id}/movements`, `POST {id}/close`, `POST {id}/reconcile`.

The only API-layer edit is the `GlobalExceptionHandler` status mapping (§7).
`GET /api/v1/pos/products/members` accepts `query` and `credentialId` — **not**
`credentialValue`; the SPA was corrected to match (see §23, item 5).

## 11. Authorization/permissions changes

No new permission keys and no role changes. The existing D5 catalogue was reused
as-is:

```
POS.Product.View | POS.Sale.Create | POS.Sale.Complete | POS.Sale.Void
POS.Sale.Refund | POS.Sale.Discount | POS.Sale.MemberCharge | POS.Sale.Reprint
POS.Register.View | POS.Register.Manage
Cashiering.Session.Open | Cashiering.Session.View
Cashiering.CashMovement.Create | Cashiering.Session.Close
Cashiering.Session.Reconcile | Cashiering.Variance.Approve
```

The `POS Operator` role in `ReferenceDataSeeder.cs` is unchanged and still holds
create/complete/member-charge plus session open/view/close and cash-movement
create — but **not** void, refund, or reconcile. The SPA honours that: the Void
and Refund controls are gated on `POS.Sale.Void` / `POS.Sale.Refund` and the
Reconcile control on `Cashiering.Session.Reconcile`, so a POS Operator sees
neither. All gating is server-side first; the client-side `can(...)` checks are
presentation only.

## 12. Idempotency changes

This was the largest single item. `idempotency_records` previously keyed on
`IdempotencyKey` alone, which the PM directive and Phase 8 §13 both reject.

- **Scoped uniqueness** — composite unique index on
  `(IdempotencyKey, Operation, ActorUserId)`. The same key used for a completion
  and a refund, or by two different actors, are now independent records.
- **Request-hash comparison** — the store records a SHA-256 hash; a replay with a
  different payload raises 409 rather than silently returning the old result.
  Distinct messages for `InProgress`, `Failed` ("previously failed; retry the
  request."), and hash mismatch.
- **Hash includes the target id** — `Compute($"{id:N}|{PosJson.Serialize(input)}")`
  for both complete and refund. Payload-only hashing was a real defect (§23/§24).
- **Failed keys are re-acquirable** — a `Failed` record is reset to `InProgress`
  with its result/error/completed-at cleared and the hash and correlation
  refreshed, so a client can retry after a genuine failure.
- **Tracker hygiene** — `RollbackAsync` detaches all tracked entries, so a later
  `FailAsync` save cannot re-attempt a discarded graph. This was the mechanism
  behind the wedged-key failure mode.
- **Race-loser handling** — the insert race catches `DbUpdateException`, clears the
  change tracker, and re-reads the winning record.
- **Secondary-failure suppression** — `MarkIdempotencyFailedAsync` swallows its own
  failures so recording the failure can never mask the original exception.

## 13. Concurrency changes

`GlobalExceptionHandler` now maps `DbUpdateConcurrencyException` to:

- HTTP **409**
- error code `ApiErrorCode.ConcurrencyConflict`
- detail `"The resource was modified by another request. Retry the operation."`
- a `LogWarning` (no exception detail, no payload, no secrets)

The pre-existing domain `ConcurrencyException` → 409 mapping is preserved. This
directly closes open risk §14.2. `xmin` concurrency tokens remain configured on
exactly the five aggregate parents (`pos_outlets`, `pos_registers`,
`cashier_sessions`, `sales`, `sale_refunds`); child/append-only tables
intentionally have none.

## 14. Cashiering invariant changes

- **One unfinished session per register is now a database invariant.** A filtered
  unique index on `RegisterId` over statuses `0,1,2` (`Open`, `Active`, `Closing`)
  makes a second concurrent open impossible regardless of application timing.
- **The violation is translated, not leaked.** `OpenAsync` catches
  `DbUpdateException` filtered on `InnerException is PostgresException
  { SqlState: PostgresErrorCode.UniqueViolation }`, clears the change tracker, and
  throws `ConflictException($"POS register '{register.Code}' already has an active
  cashier session.")` → 409. The raw provider exception never reaches the client.
- Once a session reaches `Closed` or `Reconciled` it leaves the filter, so a new
  session can be opened on the same register — asserted by
  `A_new_session_may_be_opened_once_the_previous_one_is_finished`.
- `Closing → Closed` still occurs in a single save (no durable closing phase), and
  the persisted `ExpectedCash` column may still lag mid-session while the API
  derives it live. Both are unchanged and reported in §26.

## 15. POS SPA changes

Three new pages, all following the existing page conventions (shared `json`
client, `describeError`, `useAuth().can`, `alert`/`status-pill`/`card`/`toolbar`/
`detail-grid` classes only).

**`pages/PosPage.tsx` (575 lines)** — the till. Register selector (from
`GET /api/v1/pos/configuration`, defaulting to the first `Active` register);
product search by name/SKU/barcode scoped to the register's inventory location,
with an out-of-stock guard; cart with inline quantity edit and remove; member
lookup; sale creation; `[Cash] [Card] [Account] [Split] [Complete Sale]` exactly
as Phase 5 §43 specifies; an editable tender table with running **Tendered** and
**Outstanding** totals; Void; receipt panel; reset.
- The **Split** control appends a tender line, letting the operator mix methods
  (e.g. part cash, part card) until the tendered total equals the grand total.
- `[Account]` is disabled without a resolved member and without
  `POS.Sale.MemberCharge`.
- The completion idempotency key is generated **once per sale**
  (`pos-${crypto.randomUUID()}`) and reused on every retry, so a retried
  completion replays the stored result instead of double-posting.
- Outstanding must equal zero before Complete is enabled — a client-side mirror of
  the server's tender-balance rule, not a substitute for it.

**`pages/SalesPage.tsx`** — search and history. Free-text search plus a status
filter; sale detail with lines, tenders and refunds; reprint receipt via
`GET /api/v1/sales/{id}/receipt`; void; refund with a mandatory reason and a
fresh per-attempt idempotency key (`sales-refund-${crypto.randomUUID()}`).

**`pages/CashierSessionsPage.tsx`** — all six Phase 5 §43 screens. Current Session
(status, float, expected cash, declared cash, variance and approval flag, open and
close timestamps, full movement ledger); Open Session (with the one-unfinished-
session rule surfaced in the helper text and the control disabled while a session
is open); Cash Movements (pay-in / pay-out with amount and reason, showing both
the magnitude and the signed drawer effect); Close Session (declared cash →
variance); Reconciliation; and History with a status filter.

## 16. Routes/navigation added

Three routes in `App.tsx`, inside the existing authenticated layout:

| Route | Component | Nav permission |
| --- | --- | --- |
| `/pos` | `PosPage` | `POS.Sale.Create` |
| `/sales` | `SalesPage` | `POS.Sale.Create` |
| `/cashier-sessions` | `CashierSessionsPage` | `Cashiering.Session.View` |

Three `NAV_ITEMS` entries added in `layout/Layout.tsx` — "Point of Sale", "Sales",
"Cashier Sessions" — filtered by the existing `can(item.permission)` predicate.

All three deep links are served by the pre-existing
`ApiBootstrapper.ServeClientAppIfPresent` fallback, which returns `index.html`
for any non-`/api` path and preserves contract 404s for unknown API endpoints.
The new bundle was published to `src/ClubStack.Api/wwwroot`, replacing the stale
`index-nYnHWvfK.js` with `index-JwDyQFvB.js`.

## 17. Frontend types/API integration

- `types.ts` grew from 536 to ~826 lines with 17 D5 interfaces appended, mirroring
  `D5Contracts.cs` record-for-record: `PosProduct`, `PosProductSearch`,
  `PosMember`, `PosMemberSearch`, `PosOutlet`, `PosRegister`, `PosConfiguration`,
  `SaleLine`, `SalePayment`, `SaleDiscount`, `Receipt`, `Sale`, `SaleSearch`,
  `SaleRefundLine`, `SaleRefundPayment`, `SaleRefund`, `CashMovement`,
  `CashierSession`, `CashierSessionSearch`. Property names are camelCase, matching
  the server's `JsonSerializerDefaults.Web` configuration.
- All three pages use the existing `api/client` helpers (`json.get`, `json.post`,
  `describeError`) — no bespoke fetch logic, no hard-coded base URL, no new HTTP
  dependency.
- Every call goes to an existing, already-permissioned D5 endpoint. The SPA
  introduced no new backend surface.

## 18. Tests added

**`tests/ClubStack.IntegrationTests/D5ContinuationApiTests.cs` — 11 new tests**
(`IAsyncLifetime`, scratch database per test, real cookie-authenticated HTTP
against two clients with different roles):

1. `Repeating_a_completion_with_the_same_key_replays_the_stored_result`
2. `Reusing_a_key_with_a_different_payload_is_rejected`
3. `Reusing_a_key_with_a_different_amount_is_rejected`
4. `Concurrent_completions_with_one_key_produce_exactly_one_effect`
5. `A_failed_completion_can_be_retried_with_the_same_key`
6. `One_key_is_independent_for_completion_and_refund`
7. `One_key_is_independent_per_authenticated_actor`
8. `Concurrent_voids_of_one_sale_yield_one_success_and_one_conflict`
9. `Only_one_cashier_session_may_be_unfinished_on_a_register`
10. `A_new_session_may_be_opened_once_the_previous_one_is_finished`
11. `Sale_rows_reject_an_unknown_product_line`

`IdempotencyPersistenceTests.cs` grew 5 → 9 tests, adding per-operation
independence, per-actor independence, failed-key re-acquisition, and
request-hash preservation.

Integration total: **127 → 142 (+15)**.

## 19. Existing tests changed

All changes were signature/expectation updates forced by the Part 3 and Part 4
production changes. No test was weakened, skipped, or deleted.

- `IdempotencyPersistenceTests.cs` — rewritten for the new
  `operation`/`actorUserId`/`requestHash` signatures; 5 → 9 tests.
- `ConcurrentIdempotencyTests.cs` (4 tests) — updated to pass
  `Operation = "test.concurrent.operation"` and `Actor = "actor-concurrent"`.
- `SchemaTests.cs` — asserted index name updated to
  `IX_idempotency_records_IdempotencyKey_Operation_ActorUserId` (both the
  required-index switch case and the `Assert.Contains`).
- `MigrationStartupBehaviorTests.cs` — expected migration count 6 → 8.
- `PostgreSqlTestFixture.DropDatabaseAsync` — **infrastructure flake fix**, not a
  behaviour change: now calls `NpgsqlConnection.ClearAllPools()`, attempts
  `DROP DATABASE IF EXISTS "x" WITH (FORCE)`, falls back to the plain form, and
  swallows `PostgresException` (the test role lacks `pg_signal_backend`; an
  orphaned scratch database is preferable to failing a passing test).
- `D5ContinuationApiTests.PostJsonAsync` — permanently improved to include the
  response body in assertion messages. This immediately diagnosed failure §23.3.
- `D5PosCashieringApiTests.cs`, `EfOutboxStore.cs`, `D5_Session_Report.md` also
  carry today's timestamp but were edited in the **prior** session, not this one;
  they are excluded from the changed-file lists in §32/§33.

## 20. Exact build results

```
> dotnet build "ClubStack.slnx"
  Determining projects to restore...
  All projects are up-to-date for restore.
  ClubStack.Desktop -> ...\ClubStack.Desktop.dll
  ClubStack.Contracts -> ...\ClubStack.Contracts.dll
  ClubStack.Domain -> ...\ClubStack.Domain.dll
  ClubStack.Hardware -> ...\ClubStack.Hardware.dll
  ClubStack.Application -> ...\ClubStack.Application.dll
  ClubStack.Domain.Tests -> ...\ClubStack.Domain.Tests.dll
  ClubStack.Application.Tests -> ...\ClubStack.Application.Tests.dll
  ClubStack.Infrastructure -> ...\ClubStack.Infrastructure.dll
  ClubStack.Api -> ...\ClubStack.Api.dll
  ClubStack.Api.Tests -> ...\ClubStack.Api.Tests.dll
  ClubStack.IntegrationTests -> ...\ClubStack.IntegrationTests.dll
  ClubStack.Host -> ...\ClubStack.Host.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:12.78
```

## 21. Exact test results

```
> $env:CLUBSTACK_DB_PASSWORD='<set once at User scope: CLUBSTACK_DB_PASSWORD; see docs/local-setup>'; dotnet test "ClubStack.slnx"

Passed!  - Failed:     0, Passed:   220, Skipped:     0, Total:   220, Duration: 372 ms - ClubStack.Domain.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration:  22 ms - ClubStack.Application.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:   142, Skipped:     0, Total:   142, Duration: 2 m 37 s - ClubStack.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration:  22 ms - ClubStack.Api.Tests.dll (net10.0)
```

**Total: 364 passed, 0 failed, 0 skipped** (baseline 349, delta **+15**).
Run against real PostgreSQL. No test was skipped, filtered, or quarantined.

## 22. Frontend build result

```
> npm run build          # in client\clubstack-ui
> clubstack-ui@0.1.0 build
> tsc --noEmit -p tsconfig.json && vite build

vite v6.4.3 building for production...
transforming...
✓ 55 modules transformed.
rendering chunks...
computing gzip size...
dist/index.html                   0.39 kB │ gzip:  0.27 kB
dist/assets/index-DpQwXAk3.css    3.77 kB │ gzip:  1.26 kB
dist/assets/index-JwDyQFvB.js   364.63 kB │ gzip: 99.90 kB
✓ built in 5.10s
```

`tsc --noEmit` passed. `tsconfig.json` enables `strict`, plus
`noUnusedLocals`, `noUnusedParameters` and `noFallthroughCasesInSwitch`, so the
typecheck is genuinely strict. The three new pages contain no `any` escape hatch
and no non-existent CSS class (`.btn-secondary` and `.status-pill.good` are
absent). There is no lint script in `package.json`; the build script is the
project's full verification gate. Output published to
`src/ClubStack.Api/wwwroot`, and the published `index.html` was confirmed to
reference the new `assets/index-JwDyQFvB.js` bundle.

## 23. Failures encountered

1. **Wrong FK target for `Sales.LocationId` (runtime 500s).** The first
   referential-integrity migration pointed `LocationId` at `locations`. Sale
   creation returned 500 because the column receives a *register's inventory
   location id*, which is not a row in the club-level `locations` table.
2. **Idempotency hash omitted the target sale id (wrong-result replay).** The same
   key with the same tender payload against a *different* sale replayed the first
   sale's stored completion instead of being rejected or applied to its own sale.
   Caught by the new test `Reusing_a_key_with_a_different_amount_is_rejected`
   during Part 3.
3. **`Only_one_cashier_session_may_be_unfinished_on_a_register` failed on close.**
   The test attempted to close a session still in `Open`. The API correctly
   rejected it, because a session becomes `Active` only after its first completed
   sale. Diagnosed because `PostJsonAsync` had just been improved to surface the
   response body in the assertion message; the test was corrected to complete a
   sale first, not the production rule.
4. **`PostgreSqlTestFixture.DropDatabaseAsync` flaked.** The test role lacks
   `pg_signal_backend`, so terminating connections before `DROP DATABASE` failed
   and failed otherwise-passing tests.
5. **`PosPage` member lookup used the wrong query parameter.** It called
   `GET /api/v1/pos/products/members?credentialValue=…`, but the endpoint accepts
   `query` and `credentialId`. Caught by reading `PosController` before building,
   so it never reached a failing build.
6. **Two migrations were needed instead of one** (idempotency scoping and
   referential integrity were separate, independently revertible changes).

## 24. Root causes

1. **Name collision between two unrelated "location" concepts.**
   `LocationEntity` (`locations`, club-level) and `InventoryLocationEntity`
   (`inventory_locations`) are distinct entities whose names differ by one
   prefix. `SaleEntity.LocationId` is assigned `register.InventoryLocationId` and
   compared with `StockMovementEntity.LocationId`, so it is unambiguously an
   inventory location. The mistake was reading the property name rather than its
   assignment.
2. **The hash input was under-specified.** Hashing only the request payload is
   insufficient for operations addressed to a resource: the resource identity is
   part of the operation's meaning, so it belongs in the hash. The collision was
   invisible for a single-sale test and only appeared once per-sale keys were
   exercised.
3. **The test encoded an incorrect assumption about the state machine.** `Open` →
   `Closing` is not a legal close path; `Active` is required. The domain was right
   and the test was wrong.
4. **Least-privilege test role vs. administrative teardown.** The fixture assumed
   it could terminate backend connections, which its role cannot do. Test
   infrastructure had to degrade gracefully rather than assume privileges.
5. **Contract drift between client assumption and controller signature.** The SPA
   was written from the DTO shape rather than from the action signature, and the
   member-search action takes `query`/`credentialId`, not `credentialValue`.

## 25. Fixes applied

1. Removed the incorrect migration with `dotnet ef migrations remove` and
   regenerated it against `inventory_locations`, then re-verified the principals:
   `sale_lines`, `products`, `account_transactions`, `payment_methods`,
   `inventory_locations`, `pos_outlets`.
2. Changed both hash computations to
   `IdempotencyRequestHash.Compute($"{id:N}|{PosJson.Serialize(input)}")` so the
   target sale is part of the compared identity.
3. Corrected the test to complete a sale before closing the session, matching the
   documented `Open → Active → Closing → Closed` progression.
4. `DropDatabaseAsync` now clears pools, tries `WITH (FORCE)`, falls back, and
   swallows `PostgresException`.
5. Switched the lookup to `?query=`, and kept `credentialValue` on sale creation
   only as the server-side member-resolution path (sending `memberId` when a
   member was explicitly identified).
6. Split the schema work into the two migrations actually listed in §8.

Each fix was applied to production code or to genuinely incorrect test
assumptions. No test was weakened to make a defect disappear.

## 26. Remaining limitations

1. **`Closing → Closed` happens in a single save.** There is no durable closing
   phase, so a crash between them cannot leave a session durably `Closing`. This
   is pre-existing and unchanged; the `Closing` status is in the partial unique
   index so the invariant holds regardless.
2. **The persisted `ExpectedCash` column can lag during an active session.** API
   responses derive expected cash live from the float, cash sales, cash refunds
   and movements, so responses are correct; the column is only refreshed at close.
3. **No automated SPA tests.** `package.json` has no test runner, so the SPA is
   verified by strict `tsc --noEmit` and a production build only. No component,
   route, or interaction test exists.
4. **The `wwwroot` publish is manual.** `client/clubstack-ui` is **not** a member
   of `ClubStack.slnx`, and there is no MSBuild or npm integration, so
   `npm run build` does not update `src/ClubStack.Api/wwwroot`. This session copied
   the output by hand; a future build can silently serve a stale bundle.
5. **Client-side permission checks are presentation only.** They correctly hide
   controls a user cannot use, but the security boundary is entirely server-side.
6. **Pre-existing CSS class bugs in `ProductsPage.tsx`** (`.btn-secondary` and
   `.status-pill.good`, neither of which exists in `styles.css`) were left
   untouched — out of scope, and deliberately not copied into the new pages.
7. **Idempotency records are retained indefinitely**; there is no expiry or
   pruning job. Long-term table growth is unaddressed.
8. **`Reusing_a_key_with_a_different_payload_is_rejected` and
   `Reusing_a_key_with_a_different_amount_is_rejected`** cover complete; the
   refund path is covered for per-operation independence and retry, but not with
   an explicit differing-payload case.

## 27. Remaining specification issues

1. **No variance-approval endpoint is specified.** `Cashiering.Variance.Approve`
   exists as a permission (Phase 8 §4.2) but Phase 8 §12's route catalogue defines
   no endpoint that consumes it. Building one would mean inventing a product rule,
   so it was **not** built. The existing semantics stand: zero variance
   auto-approves; a non-zero variance stays unapproved with no way to approve it.
   **The PM must specify the route, request shape, and approver role.**
2. **Route divergence from the Phase 8 §12 catalogue.** The specification lists
   POS sale routes under `/api/v1/pos/sales/...` and cash movements under
   `/cashier-sessions/{id}/cash-movements`; the implementation uses
   `/api/v1/sales/...` and `/movements`. This was **reported, not redesigned** —
   the implementation was already shipped and tested against its current routes.
   The PM should confirm which is canonical before D6 builds on it.
3. **No dedicated Sales page is specified in Phase 5 §43**, yet the PM directive
   requires `/sales`. Implemented as directed; flagged for confirmation.
4. **`ExpectedCash` persistence semantics are unspecified** — whether the column
   is authoritative or a cached projection is not stated. Current implementation
   treats the live calculation as authoritative.
5. **Idempotency record retention is unspecified.** No TTL or pruning rule is
   given.

## 28. Security considerations

- **Authorization is server-side and unchanged.** Every D5 endpoint keeps its
  `[Authorize]` plus per-action permission policy: anonymous → 401, missing
  permission → 403. Existing HTTP tests cover both.
- **The idempotency actor is derived from the authenticated principal, never from
  the request body.** A client cannot widen its own scope by sending a different
  actor id, and cannot collide with another actor's key. This is the direct
  security benefit of Part 3.
- **Payload-hash comparison prevents cross-operation result leakage.** Without it,
  a key collision could return another request's stored response body.
- **Provider exceptions are not leaked.** The register-collision path converts a
  `PostgresException` into a domain `ConflictException`; the client sees a 409 and
  a readable message, never a database error, SQL text, or connection detail.
- **Concurrency logging is safe.** The new `LogWarning` records the failure
  category and a fixed message; it does not log request bodies, credentials, or
  connection strings.
- **Referential integrity is now enforced by the database.** Six previously
  unconstrained FKs mean a sale can no longer reference a non-existent product,
  payment method, account transaction, outlet, inventory location, or discount
  line. `DeleteBehavior.Restrict` prevents cascading destruction of financial
  history.
- **The one-session-per-register invariant is enforced below the application
  layer**, so it holds even under concurrent requests from multiple instances.
- **The SPA does not hold secrets.** No tokens are stored in `localStorage`; it
  uses the existing cookie-authenticated `api/client` and relies on the
  server's authorization. The client-side `can(...)` checks are UX only and are
  never treated as a security control.
- **Idempotency keys are generated client-side with `crypto.randomUUID()`** and
  are not secret; they are correlation aids, not credentials. The completion key
  is scoped per sale, so it is not reusable across sales.
- **Test credentials** live only in the integration fixture's scratch databases and
  are not written to the repository.

## 29. Regression status

**Fully green, no regressions.** Baseline 349/349 → 364/364 (+15), 0 failures,
0 skipped. Domain 220 and Application 1 are unchanged; Api 1 is unchanged;
Integration grew 127 → 142. The full solution builds with **0 warnings and 0
errors** — no new warning was introduced by any of the two migrations, the new
`EfIdempotencyStore`, or the SPA type additions. The prior session's four
production fixes remain green, and the temporary EF SQL capture, sensitive-data
logging and change-tracker probes remain removed.

## 30. D5 acceptance status

**Presented as ready for PM acceptance. This report does not accept D5 — only
the PM can.**

All seven open risks from `D5_Session_Report.md` §14 are now closed:

| Prior §14 risk | Status |
| --- | --- |
| 1. Reference generation inside the transaction | **Closed** — all references pre-allocated before `BeginAsync` |
| 2. Concurrency surfacing as HTTP 500 | **Closed** — `DbUpdateConcurrencyException` → 409 |
| 3. Global, wedging idempotency keys | **Closed** — actor+operation scoped, hash-checked, failed keys re-acquirable, tracker cleaned on rollback |
| 4. FK / nullability configuration gaps | **Closed** — 6 FKs configured, `DiscountType` nullable |
| 5. Cashiering invariants | **Closed** — partial unique index + 409 translation. Residual: single-save `Closing → Closed` (§26.1) |
| 6. Coverage gaps | **Substantially closed** — 11 new D5 tests + 4 new idempotency tests covering the directed areas |
| 7. POS SPA surface not built | **Closed** — `/pos`, `/sales`, `/cashier-sessions` built, routed, nav-gated, typechecked, built, published |

Two items are **reported for PM decision rather than resolved by the agent**,
because resolving them would require inventing product rules: the unspecified
variance-approval endpoint (§27.1) and the route-catalogue divergence (§27.2).
Neither blocks the D5 backend or SPA; both should be settled before D6 builds
on this surface.

## 31. Exact next-session entry points

1. **PM decisions required first** — variance-approval endpoint shape and role
   (§27.1); canonical sale/movement routes (§27.2); confirmation that `/sales`
   is wanted (§27.3).
2. **Optional, if the PM wants a stronger SPA gate:** add a test runner to
   `client/clubstack-ui/package.json` and cover the three new pages, or wire
   `npm run build` into the build so `wwwroot` cannot go stale (§26.4).
3. **Optional durability work:** a durable `Closing` phase (§26.1) and an
   idempotency-record retention job (§26.7).
4. **Then, and only on PM instruction, begin D6.** Do not start it before the
   §27 decisions are recorded.
5. **Files to read first in a next session:** this report (§26, §27, §31),
   `D5_Session_Report.md` §14 for the closed-risk baseline,
   `docs/pm-prompts/D5-continuation-13-part-prompt.md` for the directive, and
   `D5ContinuationApiTests.cs` for the working D5 test harness pattern
   (`IAsyncLifetime`, scratch DB, `CreateTillAsync`, two role-scoped clients).

## 32. Files created

```
src/ClubStack.Infrastructure/Idempotency/EfIdempotencyStore.cs
src/ClubStack.Infrastructure/Migrations/20260925105931_D5ContinuationIdempotencyScoping.cs
src/ClubStack.Infrastructure/Migrations/20260925105931_D5ContinuationIdempotencyScoping.Designer.cs
src/ClubStack.Infrastructure/Migrations/20260925111433_D5ContinuationReferentialIntegrity.cs
src/ClubStack.Infrastructure/Migrations/20260925111433_D5ContinuationReferentialIntegrity.Designer.cs
tests/ClubStack.IntegrationTests/D5ContinuationApiTests.cs
client/clubstack-ui/src/pages/PosPage.tsx
client/clubstack-ui/src/pages/SalesPage.tsx
client/clubstack-ui/src/pages/CashierSessionsPage.tsx
docs/pm-prompts/README.md
docs/pm-prompts/D5-continuation-13-part-prompt.md
docs/pm-prompts/D5-continuation-report-33-point-template.md
D5_Continuation_Session_Report.md
```

Build artifacts also created by `npm run build` and published to
`src/ClubStack.Api/wwwroot`: `assets/index-JwDyQFvB.js` (new),
`assets/index-DpQwXAk3.css` (rebuilt), `index.html` (rebuilt). The stale
`assets/index-nYnHWvfK.js` was deleted.

## 33. Files modified

```
src/ClubStack.Application/Accounts/AccountContracts.cs
src/ClubStack.Application/Idempotency/Idempotency.cs
src/ClubStack.Api/ErrorHandling/GlobalExceptionHandler.cs
src/ClubStack.Infrastructure/Accounts/EntityFrameworkMemberAccountService.cs
src/ClubStack.Infrastructure/Migrations/ClubStackDbContextModelSnapshot.cs
src/ClubStack.Infrastructure/Persistence/ClubStackDbContext.cs
src/ClubStack.Infrastructure/Persistence/Entities/InfrastructureEntities.cs
src/ClubStack.Infrastructure/Persistence/Entities/PosEntities.cs
src/ClubStack.Infrastructure/Persistence/IUnitOfWork.cs
src/ClubStack.Infrastructure/Pos/EntityFrameworkCashierSessionService.cs
src/ClubStack.Infrastructure/Pos/EntityFrameworkSaleService.cs
tests/ClubStack.IntegrationTests/ConcurrentIdempotencyTests.cs
tests/ClubStack.IntegrationTests/IdempotencyPersistenceTests.cs
tests/ClubStack.IntegrationTests/MigrationStartupBehaviorTests.cs
tests/ClubStack.IntegrationTests/PostgreSqlTestFixture.cs
tests/ClubStack.IntegrationTests/SchemaTests.cs
client/clubstack-ui/src/types.ts
client/clubstack-ui/src/App.tsx
client/clubstack-ui/src/layout/Layout.tsx
```

Edited in the **prior** session (not this one), listed for completeness because
they also carry today's timestamp:
`D5_Session_Report.md`, `tests/ClubStack.IntegrationTests/D5PosCashieringApiTests.cs`,
`src/ClubStack.Infrastructure/Outbox/EfOutboxStore.cs`.
(`CreditInput` lives in `Accounts/AccountContracts.cs`, which **was** modified
this session and is listed above; `Pos/PosContracts.cs` was not touched.)

No secrets, keys, or credentials were added to any file. Nothing was committed —
this workspace is not a git repository and no commit was requested.

**Repository hygiene.** One stray file was removed during the closing pass:
`tests/ClubStack.IntegrationTests/testresults-del.txt` — a 264-byte, 2-line
MSBuild error dump (`MSB1009: Project file does not exist`, from a mistyped
project path) written on 2026-09-24, referenced by no project or source file. Its
full content was reproduced before deletion, so nothing of substance was lost.
A closing sweep also confirmed that no temporary instrumentation remains
(EF SQL `LogTo` capture, `EnableSensitiveDataLogging`, change-tracker probes),
that all 33 report headings are present and correctly ordered, and that the
published `wwwroot` bundle matches the latest SPA build.

---

D5 READY FOR PM ACCEPTANCE
