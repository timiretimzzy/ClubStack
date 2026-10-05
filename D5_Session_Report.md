# ClubStack D5 Session Report

**D5 POS & Cashiering — backend vertical slice (Database → Domain → Application → API → Testing) + test-driven defect hardening**

## 1. Session Objective

Land the D5 POS and Cashiering backend vertical slice per the approved
specification chain (Phase 6 §11; Phase 2 sales/cashiering ranges; Phase 3
pricing/tax; Phase 7 POS/cashiering permissions; Phase 8 §5 audit, §7.4 pricing,
§8.x POS/cashiering postings) over the real cookie-authenticated HTTP pipeline,
and drive the new integration surface to green. Scope for this session was
**backend + tests** (see §2). The explicit goal of the final stretch was to find
and fix the **root cause** of the completion 500, not mask it.

## 2. Scope & Boundary

- **In scope (this session):** POS configuration (outlets/registers), sale
  lifecycle (create → complete → void / refund) with tender lines, discounts,
  receipts, stock posting and member-account charging; cashiering (sessions,
  opening float, cash movements, expected-cash, close, reconcile); permission
  gating and audit on the D5 endpoints; the three D5 HTTP integration tests; and
  the defect-fixing required to make that suite green.
- **Explicitly out of scope (not started):** the POS **SPA surface** (no
  `/pos`, `/sales`, or `/cashier-sessions` routes exist — §14), the procurement
  workflow slice, and any D6/D7 work. No frontend pages were added this session.
- Rule held throughout: fix real code; never weaken a test to force green. The
  single test edit made (§12, Fix 5) was reviewed as legitimate, not an
  accommodation of a production defect.

## 3. Specification Sources

- `ClubStack_Phase_*.md`: Phase 6 §11 (D5 slice / D4 exclusions), Phase 2
  POS+cashiering ranges, Phase 3 pricing/tax, Phase 7 (POS/cashiering
  permission descriptors), Phase 8 §5 audit / §7.4 pricing / §8.x postings.
- `D4_Session_Report.md` (D4 explicitly scoped D5 as the next slice; D0–D4
  baseline = 346 tests, 0/0 build).
- PM authorisation to proceed with EF SQL capture + root-cause fix and to remove
  the POS-DIAG instrumentation after green.

## 4. Design Decisions

- **Optimistic concurrency only on parents:** `xmin` rowversion is configured on
  the aggregate parents (`pos_outlets`, `pos_registers`, `cashier_sessions`,
  `sales`, `sale_refunds`) via `ConfigureConcurrencyToken`
  (`IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()`). Child/append-only rows
  (`sale_lines`, `sale_payments`, `sale_discounts`, `cash_movements`,
  `receipts`) intentionally have no token — they are inserted once and never
  updated in place. This is consistent between the EF model and the migration.
- **Ledger-style expected cash is derived, not cached:** API responses compute
  expected cash dynamically (opening float + cash sales − cash refunds + pay-ins
  − pay-outs) rather than trusting a stored column, so mid-session variance stays
  correct even though the persisted `ExpectedCash` column can lag until close.
- **Sign convention is split by purpose:** `CashMovementEntity.SignedAmount` is
  the *signed* drawer contribution (Float/PayIn positive, PayOut negative) and is
  part of the public movement DTO; the *expected-cash formula*
  (`PosCashMath.ExpectedCashOnHand`) takes **magnitudes** and applies the sign
  (`+ payIns − payOuts`). The aggregation therefore sums `Amount`, not
  `SignedAmount` (this split was the cause of a real bug — §12, Fix 3).
- **Sale numbering / idempotency:** sales, receipts, cashier sessions and refunds
  use width-6 references (`SALE-`, `RCT-`, `CSN-`, `RFD-`); completion/refund
  are idempotency-key guarded.

## 5. Domain — POS & Cashiering Rules

- `src/ClubStack.Domain/Pos/PosStates.cs`: `SaleStatus`
  (`Open → PendingPayment → Completed | Voided | Cancelled`),
  `CashierSessionStatus` (`Open → Active → Closing → Closed → Reconciled`),
  `CashMovementType` (`Float`, `PayIn`, `PayOut`), `ProductTaxTreatment`
  (`Inclusive`, `Exclusive`, `ZeroRated`).
- `src/ClubStack.Domain/Pos/PosDomain.cs`: reference formats (§3 prefixes,
  width 6), `PosCashMath.ExpectedCashOnHand` / `Variance` (§4), and pricing/tax/
  receipt rules.
- Domain tests remain in `tests/ClubStack.Domain.Tests` (no new D5-only domain
  file was added this session; the POS state/format rules are exercised via the
  integration flow and the shared domain suite).

## 6. Application Layer — Contracts

- `src/ClubStack.Application/Pos/PosContracts.cs`: DTOs + service interfaces
  (`IPosProductService`, `IPosConfigurationService`, `ICashierSessionService`,
  `ISaleService`); `PosEvents.cs` defines the outbox event types/payloads.
- `src/ClubStack.Application/Pos/` validation is centralized behind the existing
  `ValidationException` → 400 pipeline; conflict/state rejections → 409.

## 7. Infrastructure — Entities, Migration & Concurrency

- `Persistence/Entities/PosEntities.cs`: the D5 entity set; all entity `Id`s are
  client-pre-assigned via `Guid.CreateVersion7()`.
- Migration `20260924174005_D5_PosAndCashiering` creates the 10 D5 tables
  (`pos_outlets`, `pos_registers`, `cashier_sessions`, `cash_movements`,
  `sales`, `sale_lines`, `sale_payments`, `sale_discounts`, `sale_refunds`,
  `receipts`); the dev DB applies it at startup via `MigrateOnStartup`.
- Services in `src/ClubStack.Infrastructure/Pos/`:
  `EntityFrameworkSaleService`, `EntityFrameworkCashierSessionService`,
  `EntityFrameworkPosProductService`, `EntityFrameworkPosConfigurationService`,
  `PosServiceHelpers` (validation/pricing/tax/mappings).

## 8. API Contracts & Controllers (D5)

- `PosController.cs` — POS configuration + product/member lookup
  (`GET /api/v1/pos/...`).
- `SalesController.cs` — sale create / get / complete / void / refund / receipt
  (`/api/v1/sales/...`).
- `CashierSessionsController.cs` — session open / get / search / cash-movement /
  close / reconcile (`/api/v1/cashier-sessions/...`).
- `Contracts/D5Contracts.cs` holds the request/response records; `[ApiController]`
  validation → 400, state rejections → 409, missing permission → 403, anonymous
  → 401.

## 9. Audit & Server-Side Authorization

- Every D5 action is `[Authorize]`-gated (anonymous → 401) with per-action
  permission policies enforced server-side (missing key → 403), verified over
  HTTP in the D5 test class.
- Sale/cashiering mutations are audited with the Phase 8 §5 conventions
  (entity type + reference attached) via the shared `AuditedControllerBase`.

## 10. Permissions & Role Cataloguing (D5)

New D5 keys in `Permissions.cs` (exact strings):

```
POS.Product.View | POS.Sale.Create | POS.Sale.Complete | POS.Sale.Void
POS.Sale.Refund | POS.Sale.Discount | POS.Sale.MemberCharge | POS.Sale.Reprint
POS.Register.View | POS.Register.Manage
Cashiering.Session.Open | Cashiering.Session.View
Cashiering.CashMovement.Create | Cashiering.Session.Close
Cashiering.Session.Reconcile | Cashiering.Variance.Approve
```

Sensitive keys (`Void`, `Refund`, `Discount`, `Register.Manage`, `Session.Close`,
`Variance.Approve`) are flagged `IsSensitive`. Member lookup is an **any-of**
policy (`POS.Product.View` ∨ `POS.Sale.Create` ∨ `Membership.Member.View`);
there is deliberately no standalone `POS.MemberLookup` key. System Administrator
already carries all keys via `Permissions.All`.

## 11. Reference Generation & Numbering

`SALE`/`RCT`/`CSN`/`RFD` (width 6) resolve through the existing
`IReferenceGenerator` numeric-sequence presets. *(Open risk: unlike the D4 stock
writers, the sale service does not currently pre-generate every reference before
`BeginAsync`; see §14.)*

## 12. Integration-Test Bugs Found & Fixed (root cause, not masking)

The D5 flow test initially failed with HTTP 500 on sale completion. The failure
was chased to root cause with a temporary EF-SQL capture + a file-based EF change
tracker probe (both removed after green):

- **⚠ Fix 1 — new child tender lines were tracked as `Modified` (the 500):**
  brand-new `SalePaymentEntity` rows (pre-assigned `Guid.CreateVersion7()` keys)
  added only through the navigation collection (`sale.Payments.Add(payment)`)
  were promoted to `Modified` on the first/only `SaveChanges`, so EF emitted
  full-column `UPDATE sale_payments … WHERE "Id" = @p` (no `xmin` on that table) →
  0 rows → `DbUpdateConcurrencyException` → 500. Proven with a change-tracker
  probe (both payments `state=Modified`) and the captured SQL. Fixed in
  `EntityFrameworkSaleService.CompleteAsync` by adding through the context set —
  `_context.SalePayments.Add(payment)` — which tracks them `Added` while keeping
  `sale.Payments` fixup intact. (`sale.Lines`/`sale.Discounts` were unaffected
  because the sale root is added via `_context.Sales.Add`, which tracks the whole
  new graph `Added`.)
- **⚠ Fix 2 — identical bug in cashiering:** `AddCashMovementAsync` added the new
  `CashMovementEntity` via `session.Movements.Add(...)` on an **already-tracked**
  session, producing the same `Modified` promotion and a second 500
  (`UPDATE cash_movements` → 0 rows). Fixed by adding through
  `_context.CashMovements.Add(movement)` with an explicit `SessionId = id`.
- **⚠ Fix 3 — expected-cash aggregation contradicted the domain formula:** the
  pay-out bucket summed `SignedAmount` (negative) and then handed it to
  `PosCashMath.ExpectedCashOnHand`, which *subtracts* pay-outs — double-negating
  a pay-out so it **increased** expected cash (test asserted 550, got 650). Fixed
  by summing the positive `Amount` for the Float/PayIn/PayOut buckets, matching the
  domain formula and the `SignedAmount` public DTO (which stays genuinely signed).
- **Fix 4 — latent double-`SaveChanges` (hardening):** `EfOutboxStore.EnqueueAsync`
  and `EntityFrameworkMemberAccountService.ChargeAsync` call `SaveChangesAsync`
  even when the unit-of-work transaction is already open; guarded both with
  `if (_context.Database.CurrentTransaction is null)`. Not the cause of the 500,
  but a real correctness/transaction-boundary risk removed.
- **Fix 5 — idempotency fail-path no longer masks the real error:** the
  completion `catch (DbUpdateConcurrencyException)` now suppresses a secondary
  `FailAsync` failure (try/catch) and rethrows the original concurrency error, so
  a failure-marker write can never hide the true cause.
- **Test maintenance (reviewed as legitimate, not a fake-green):** the
  unbalanced-tender case originally reused the already-`Completed` sale's id, so
  the status guard correctly returned 409 before the 400 total-mismatch
  validation could run. The test now creates a **fresh draft** for the unbalanced
  attempt so it actually exercises the intended 400 path. The other 409 assertion
  (re-completing a completed sale) is retained and still passes.

## 13. Testing

`tests/ClubStack.IntegrationTests/D5PosCashieringApiTests.cs` — 3 tests:

- `Anonymous_requests_to_d5_endpoints_are_unauthorized` — 401 on D5 routes.
- `Missing_permissions_are_forbidden_for_pos_and_cashiering` — 403 with a
  permission-limited role.
- `Pos_flow_runs_over_http_with_expected_cash_and_variance` — the full flow over
  real HTTP: open session → product/member search → sale create → complete
  (cash + card) → idempotent replay → unbalanced tender (400) → void (200, then
  409 re-complete) → account charge + balance → cash refund → account refund →
  pay-in (600) → pay-out (550) → close (declared 550, variance 0, auto-approved)
  → reconcile → search assertions.

## 14. Open Risks & Review Findings (not fixed this session — reported, not hidden)

A code review of the D5 slice surfaced the following; none are covered by the
current 3-test surface, and they are **not** claimed as fixed:

1. **Reference generation inside the transaction (high risk):** unlike the D4
   stock writers, `EntityFrameworkSaleService` does not pre-generate all
   references before `BeginAsync`; `ChargeAsync`/`CreateCreditAsync` allocate
   references on an active Npgsql transaction, which D4 established as unsafe
   ("unassigned command on an active transaction"). Not yet exercised at runtime.
2. **Concurrency exceptions still surface as HTTP 500:** `GlobalExceptionHandler`
   does not map `DbUpdateConcurrencyException` to a 409 problem response, so any
   surviving EF concurrency failure is a 500 rather than a domain conflict.
3. **Idempotency keys are global and can wedge:** `idempotency_records` has no
   operation/payload hash, and a validation/conflict failure can leave a key
   stuck `InProgress`; `FailAsync` reuses the scoped context after rollback.
4. **FK/config gaps:** several D5 FKs are not configured in the EF model
   (`SaleEntity.PosOutletId`/`LocationId`, `SaleLineEntity.ProductId`,
   `SaleDiscountEntity.LineId`, `SalePaymentEntity.PaymentMethodId`/
   `AccountTransactionId`); `SaleLineEntity.DiscountType` is nullable in the
   entity/migration but required in `ConfigureSaleLines`.
5. **Cashiering invariants:** no transaction/partial-unique-index guarantees one
   open session per register; `Closing → Closed` happens in one save (no durable
   closing phase); `VarianceApproved` only auto-approves zero variance and no
   endpoint currently uses `Cashiering.Variance.Approve`; the persisted
   `ExpectedCash` column can lag during an active session.
6. **Coverage gaps:** no D5 unit tests; no tests for the unguarded session GET,
   over/cumulative refunds, `Closing`/`Cancelled` transitions, variance approval,
   future-dated prices, or several permission/state paths.
7. **POS SPA surface not built** (§2/§17) — no POS/sales/cashiering routes or nav
   in the SPA.

## 15. Build & Test Results

```
dotnet build "ClubStack.slnx" -c Debug   -> Build succeeded. 0 Warning(s) 0 Error(s)

$env:CLUBSTACK_DB_PASSWORD='<set once at User scope: CLUBSTACK_DB_PASSWORD; see docs/local-setup>'
dotnet test (all four test projects, real PostgreSQL fixture):
  Domain.Tests ......... Passed 220  0 failed
  Application.Tests .... Passed   1  0 failed
  Api.Tests ............ Passed   1  0 failed
  IntegrationTests ..... Passed 127  0 failed   (D0-D4 baseline + 3 D5 POS)
  Total ................ Passed 349  0 failed
```

All temporary instrumentation (EF SQL `LogTo` capture, `EnableSensitiveDataLogging`
in the D5 test factory, and the EF change-tracker probe) was removed before this
final run; the suite is green with clean production code.

## 16. Specification Conflicts & Notes for PM

- **No blocking spec conflict encountered in this session.** The defects fixed
  (§12) were implementation defects (EF tracking, a sign-convention mismatch), not
  genuine spec ambiguities. The sign-convention fix (§12, Fix 3) was resolved in
  favour of the documented domain formula (`PosCashMath`), not by inventing a new
  financial rule.
- No financial, tax, or stock policy was invented beyond the specified
  formulas/states.

## 17. Verification Status

- **Backend slice GREEN; D5 as a whole NOT independently closed.** The full
  solution builds **0 warnings / 0 errors** and **349 tests pass, 0 failures**
  (Domain 220, Application 1, Api 1, Integration 127 including the 3 D5 POS
  tests). The four production defects in §12 are fixed at the root cause and
  covered by the green flow. However, the **POS SPA surface has not been built**
  and a review surfaced the open risks in §14 (notably reference generation inside
  the transaction, concurrency→500 mapping, idempotency wedging, and missing FK
  config) that are **not** yet fixed or covered. D5 therefore requires a decision:
  either (a) schedule the SPA surface + address §14 before sign-off, or (b)
  accept a backend-only D5 milestone with the §14 items formally accepted as
  follow-up. This report does not declare D5 closed.

## 18. Next Steps

- Await PM direction on §17: build the POS SPA surface and/or remediate the §14
  open risks, versus accepting a backend-only D5 milestone.
- If the SPA surface proceeds: add `/pos`, `/sales`, `/cashier-sessions` routes +
  nav gated on the D5 permission keys, mirror the D5 contracts in `types.ts`, and
  run `npm run build` (strict `tsc --noEmit` + `vite build`) with output copied to
  `wwwroot`.
- If §14 is taken on: prioritize (1) pre-generating sale/account references before
  `BeginAsync`, (2) mapping `DbUpdateConcurrencyException` → 409 in
  `GlobalExceptionHandler`, (3) the missing FK configuration, then the
  cashiering invariants and idempotency hardening.
- D6/D7 remain not started, per instruction.
