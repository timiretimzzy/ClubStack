# ClubStack D4 Session Report

**D4 Products & Inventory (end-to-end: Database → Domain → Application → API → Frontend → Testing)**

## 1. Session Objective

Complete the D4 Products + Inventory deliverable as a full vertical slice per the
approved specification chain (Phase 6 §11; Phase 2 INV-000 range incl. INV-001..045
and PROC-001; Phase 3 §21–§34; Phase 7 product/inventory permissions; Phase 8 §5
audit, §7.4 pricing, §8.6–§8.8 stock posting): product catalogue with pricing,
supplier master data, inventory locations, a ledger-authoritative stock model with
negative-stock protection and advisory-lock concurrency, transfers, stock counts,
adjustments and wastage, reorder alerts — over the real cookie-authenticated HTTPS
pipeline with a React/TS SPA surface — while keeping the D0–D3 regression suites
green and the project at **0 warnings / 0 errors**.

## 2. Scope & Boundary

- **In scope (D4):** products (SKU/barcode/category/pricing lifecycle), product
  categories, inventory locations, suppliers (PROC-001 master data), stock
  positions + movement ledger + goods receipts, stock transfers, stock counts
  (initiate/record/complete/approve), stock adjustments, wastage, reorder levels
  and low-stock alerts; server-side authorization + audit on every new endpoint;
  SPA catalogue, stock, transfers, counts, adjustments, wastage, alerts and
  suppliers pages.
- **Explicitly out of scope:** the D5 POS sale/damage/return/consumption movement
  postings (enum values reserved, not implemented), procurement **workflow**
  (requisitions/POs/goods-receipt workflow beyond the D4 supplier master data and
  the stock `Receive` posting), sale pricing plans beyond the operational Standard
  price row, gold/F&B product families, finance ledger postings, and any AI/cloud/
  mobile surface.
- PM verdict already fixed: implementation role only; spec documents are the
  authority — never invent states, rules, or financial policies beyond the specs.
  A genuine spec conflict would stop work and be reported (see §21).

## 3. Specification Sources

- `ClubStack_Phase_*.md`: Phase 6 §11 (Products + Inventory slice and D4
  exclusions), Phase 2 INV-001..045 (catalogue, stock, transfers, counts,
  adjustments, wastage, reorder) and PROC-001 (suppliers), Phase 3 §21–§22
  (catalogue + pricing), §30 (locations), §31–§32 (stock ledger + receipts), §33
  (transfers), §34 (counts/adjustments/wastage), Phase 7 (permission descriptors
  incl. `Inventory.*` / `Procurement.Supplier.*` role seeds), Phase 8 §5 (audit
  record), §7.4 (pricing data), §8.6–§8.8 (stock postings and permissions).
- `ClubStack_D3_Session_Report.md` + `ClubStack_D2_*` decision records (baseline:
  D0+D1+D2+D3 = 274 tests, 0/0 build; D3 closed awaiting D4 sign-off).
- PM messages that authorised D4 Products & Inventory and reaffirmed scope/
  exclusions and the 23-section report requirement.

## 4. Design Decisions

- **Ledger-authoritative balances:** `stock_balances` are **cached** per
  (product, location); the `stock_movements` ledger is authoritative and
  immutable after posting. Every balancing movement is inserted and the balance
  recomputed from the signed sum in the same transaction — no drift is possible.
- **Signed movement quantities, one guard:** `StockMovement.Quantity` stores the
  **signed** ledger contribution (`StockLedger.SignedQuantity`); Adjustment and
  CountReconciliation pass signed values through. The negative-stock guard is the
  single `DecreasesStock && result < 0 → ConflictException` check, so every
  downstream route (transfer out, negative adjustment, wastage, count variance)
  is protected with the same rule. Overdraft → **409 Conflict**.
- **Concurrency safety without sequence coupling:** each transaction that posts
  stock takes `pg_advisory_xact_lock` for every involved (product, location)
  pair (keys sorted, inside the `IUnitOfWork` transaction). Mathematically
  concurrent balances therefore still serialize; a concurrent overdraft settles
  one-writer-wins and the loser is rejected.
- **References generated before the transaction (Npgsql constraint):** Npgsql
  forbids issuing unassigned commands on a connection with an active
  transaction, so all reference numbers (`STM-…`, `STF-…`, `SCN-…`, `SADJ-…`)
  are generated via `IReferenceGenerator` **before** `BeginAsync` and passed into
  `StockLedgerWriter.ApplyAsync` as a parameter. Burned sequence numbers on
  rollback are accepted (documented in code).
- **Transfers post a linked pair:** completing a transfer writes TransferOut at
  the source and TransferIn at the destination in one transaction, both with
  `SourceType="StockTransfer"` and `SourceId=transfer.Id`; both carry the same
  transfer reference so movement search shows the pair. No `InTransit` state in
  D4; cancel only works pre-completion.
- **Counts snapshot then reconcile:** initiating a count snapshots expected
  balances per location into count lines; recording overwrites recounted values;
  completing freezes the sheet; approving posts a single
  `CountReconciliation` movement per line, `Variance = CountedExpected −
  Expected`, requiring `Inventory.Stock.ApproveAdjustment`.
- **Adjustments + wastage share the posting path:** both create a header + lines
  and post immediately (no draft approval step in D4); distribution between them
  is by `AdjustmentType` (`Adjustment`/`Wastage`) and, on the API, which audit
  action and permission gate applies; wastage quantities must be positive,
  adjustment quantities nonzero.
- **Prices are rows, not columns:** `product_prices` holds operational
  `PriceType="Standard"` rows (currency = club default, `decimal(19,4)`,
  `EffectiveFromUtc`); a price change appends a row so history is never
  rewritten (Phase 3 §22). `CreateProductRequest` takes an optional
  `PriceAmount` to seed the first price.
- **Reorder is a count, not a balance:** the Low/Reorder status aggregates the
  **total on-hand across all the product's locations** vs the product
  `ReorderLevel`; the POS-ready scan uses `GET /stock?reorderOnly=true`.
- **UI stays a thin client:** rules live server-side; the SPA renders contract
  shapes and gates every control with the exact `Permissions` keys from
  `/api/v1/auth/me`.

## 5. Domain — Products, Categories, Locations, Suppliers

- `src/ClubStack.Domain/Inventory/*` adds the D4 compute rules:
  - **StockIdentifier** (SKU / barcode): non-empty, bounded lengths (32/64),
    trimmed; enforced by domain + EF unique indexes.
  - **ProductManagementSettings**: positive pricing, optional reorder level, cost
    scale, currency formatting rules.
  - **SupplierRules**: supplier code format, required name, contact/email/phone
    length bounds.
- Controllers and services reuse the existing D1/D2 domain vocabulary for
  active/inactive catalogue items; no new D4 state machines beyond the stock
  documents below.

## 6. Domain — Numbering & Stock Documents

- `InventoryDomain.cs` / `InventoryStates.cs` define:
  - **StockMovementNumber** (`STM-`, width 6), **StockTransferNumber** (`STF-`,
    width 6), **StockCountNumber** (`SCN-`, width 6), **StockAdjustmentNumber**
    (`SADJ-`, width 6) — each `Format` pads the sequence to 6 digits and
    round-trips on parse (tested).
  - **Movement types:** equality/protection semantics via
    `StockLedger.DecreasesStock`, `Scalar` helpers, and
    `ComputeBalance(items) = Σ SignedQuantity` used to refresh cached balances.
  - **Transfer states:** `Draft → Completed | Cancelled`; **Count states:**
    `InProgress → Completed → Approved`, with `InProgress`/`Completed`→`Cancelled`;
    transition tables reject invalid moves with conflict semantics (409).
- Unit tests in `tests/ClubStack.Domain.Tests/InventoryDomainTests.cs` drive the
  transition tables, number formats, identifier rules, and ledger math (220
  domain tests in the project total).

## 7. Application Layer — Contracts & Service Interfaces

- `src/ClubStack.Application/Inventory/InventoryContracts.cs` defines all DTOs
  and interfaces: `IProductService`, `IProductCategoryService`,
  `IInventoryLocationService`, `ISupplierService`, `IStockLedgerService`,
  `IStockTransferService`, `IStockCountService`, `IStockAdjustmentService`
  (search/get/create/transition methods, DTOs mirror the controller response
  shapes 1:1).
- `InventoryServiceHelpers.cs` carries `StockLedgerWriter`: `AcquireLocksAsync`
  (sorted advisory keys), `ApplyAsync(reference, …)` (insert movements + refresh
  balances in the active unit-of-work transaction) and a `CurrentBalance`
  helper.
- Validation is centralized behind the existing `ValidationException` → 400
  pipeline (reason required for adjustments/wastage, positive quantities,
  nonzero adjustment quantities, currency match).

## 8. Application Layer — Stock Receipts

`IStockLedgerService.ReceiveAsync(location, lines, businessDate, reference)`:
per line, pre-generates one `STM-…` reference, locks the (product, location)
pair, inserts a signed positive `Receipt` movement carrying the unit cost, and
recomputes the cached balance. Returns the posted movements so the controller can
audit with their references. All service-level concurrent tests assert the
derived balance equals the summed ledger after concurrent receipts.

## 9. Application Layer — Transfers

`IStockTransferService`: `CreateAsync` (draft, validates source ≠ destination,
positive qty, products' stock-managed), `CompleteAsync` (posts the linked
TransferOut/TransferIn pair in one transaction with a pre-generated `STM-…` for
each line and a shared `STF-…` transfer reference), `CancelAsync` (Draft only).
Completing an already-completed or cancelled transfer is rejected; cancelling a
completed one is rejected; both tested at service level and HTTP level.

## 10. Application Layer — Counts, Adjustments & Wastage

- **Counts:** `InitiateAsync` snapshots expected balances (optionally filtered to
  one product) into `stock_count_lines`; `RecordLinesAsync` overwrites counted
  quantities; `CompleteAsync` freezes the sheet; `ApproveAsync` posts one
  `CountReconciliation` movement per line (signed `Variance`), updating balances,
  and marks the count approved — all inside the advisory-lock transaction with a
  pre-generated `STM-…` per line. `CancelAsync` is allowed on
  InProgress/Completed sheets.
- **Adjustments/wastage:** `CreateAsync` writes the `SADJ-…` header + lines and
  posts an `STM-…` per line (signed quantities: positive = increase, negative =
  decrease; wastage is always positive → ledger decrease) in one atomic
  transaction; any failure rolls back header, lines and movements together
  (regression-fixed in §16).

## 11. Infrastructure — EF Entities & D4 Migration

- `Persistence/Entities/InventoryEntities.cs` with `ClubStackDbContext`
  configurations: products, product_categories, product_prices, suppliers,
  inventory_locations, stock_balances, stock_movements, stock_transfers (+lines),
  stock_counts (+lines), stock_adjustments (+lines); unique indexes on SKU,
  barcode, category code, location code, supplier code, price row (product/type/
  from), and the (product, location) balance key; lengths/required-ness/scales
  (`decimal(19,4)` money) per Phase 3.
- **Migration `20260924123254_D4_ProductsAndInventory`** creates the D4 table
  set; the dev DB applied it at startup via `MigrateOnStartup`
  (`MigrationStartupBehaviorTests` expected count updated `4 → 5`).

## 12. API Contracts & Controllers (D4)

Prefix `/api/v1`; all eight new controllers:

- `ProductsController.cs` — `GET /api/v1/products` (search: query, category,
  stock-managed, status), `GET /api/v1/products/{id}`, `GET/POST
  /api/v1/products/{id}/prices`, `POST /api/v1/products`,
  `PUT /api/v1/products/{id}`, `POST /api/v1/products/{id}/status`.
- `ProductCategoriesController.cs` — `GET/POST /api/v1/product-categories`,
  `PUT /api/v1/product-categories/{id}`.
- `InventoryLocationsController.cs` — `GET/POST /api/v1/inventory-locations`,
  `PUT /api/v1/inventory-locations/{id}`.
- `SuppliersController.cs` — `GET/POST /api/v1/suppliers`, `GET/PUT
  /api/v1/suppliers/{id}`.
- `StockController.cs` — `GET /api/v1/stock` (+`reorderOnly`), `GET
  /api/v1/stock/movements`, `POST /api/v1/stock/receive`.
- `StockTransfersController.cs` — `GET /api/v1/transfers`, `GET/POST
  /api/v1/transfers/{id}`, `POST /api/v1/transfers/{id}/complete | cancel`.
- `StockCountsController.cs` — `GET /api/v1/counts`, `GET/POST
  /api/v1/counts/{id}`, `PUT /api/v1/counts/{id}/lines`, `POST
  /api/v1/counts/{id}/complete | approve | cancel`.
- `StockAdjustmentsController.cs` — `GET /api/v1/adjustments`
  (+`adjustmentType`), `GET/POST /api/v1/adjustments/{id}`,
  `POST /api/v1/adjustments`.
- `D4Contracts.cs` holds the request/response records; requests are
  `[ApiController]`-validated (validation failures → 400), rejections → 409,
  missing permission → 403.

## 13. Audit & Server-Side Authorization

- Every D4 mutation is gated server-side: `[Authorize]` yields 401 for anonymous
  calls to every D4 endpoint; permissions are enforced per action (static policy
  attributes for single-role endpoints, and type-scoped imperative
  `IAuthorizationService` checks for the unified adjustments/wastage endpoint,
  see §13a), so permission-missing roles → 403 (verified over HTTP).
- Sensitive actions are audited with the Phase 8 §5 conventions, e.g.
  `Inventory.Product.Create`, `Inventory.ProductCategory.Create`,
  `Inventory.Location.Create`, `Procurement.Supplier.Create`,
  `Inventory.Stock.Receive`, `Inventory.Stock.Transfer`, `Inventory.Stock.Count`,
  `Inventory.Stock.Adjust`, `Inventory.Stock.Wastage` — entity type and reference
  (`SKU`, `STF-…`, `SCN-…`, `SADJ-…`) attached; asserted by the HTTP tests.

### 13a. Wastage vs Adjustment Authorization (Correction Gate)

- The PM rejection identified one authorization defect: the unified
  `StockAdjustmentsController` gated **all three** actions (Search/Get/Create)
  with the static policy `Permission:Inventory.Stock.Adjust`, so a role holding
  only the Wastage key could not post or view wastage, while a role holding only
  the Adjust key could. Since the type arrives per-request (query `adjustmentType`
  or body `AdjustmentType`), the static attribute was replaced with type-aware
  imperative checks via injected `IAuthorizationService` on the same
  `"Permission:{key}"` policies (Phase 8 authoritative key set; no redesign, no
  route change, no contract change).
- Scope rule (`AuthorizeScopeAsync`): `Wastage` → `Inventory.Stock.Wastage`;
  `Adjustment` (and any other explicit type) → `Inventory.Stock.Adjust`; an
  **unfiltered** request spans both documents → requires **both** permissions
  (prevents leaking wastage rows to adjust-only callers). `Get`/`Create` authorize
  by the actual/requested document type; class-level `[Authorize]` keeps
  anonymous → 401. Audit actions are unchanged: a posted wastage still writes the
  `Inventory.Stock.Wastage` audit row regardless of which permission admitted it.
- Verified over HTTP with two purpose-created single-scope roles ("Wastage
  Officer" = only `Inventory.Stock.Wastage`; "Adjustment Officer" = only
  `Inventory.Stock.Adjust`) plus the existing sealed roles:
  - Anonymous → 401 on wastage search/post.
  - Wastage-only role → can search and post wastage (200), can Get its own
    wastage document, cannot post or search adjustments (403), cannot unfiltered
    (403), cannot Get an adjustment document (403).
  - Adjustment-only role → can search and post adjustments (200, unchanged), can
    Get its own adjustment document, cannot post or search wastage (403 — the
    Adjust key does **not** implicitly grant wastage), cannot unfiltered (403),
    cannot Get a wastage document (403).
  - Audit rows post with the exact per-type actions
    (`Inventory.Stock.Wastage` / `Inventory.Stock.Adjust`).

## 14. Permissions & Role Cataloguing (D4)

New keys (exact strings in `Permissions.cs`, lines ~118–139):

```
Inventory.Product.View|Create|Edit
Inventory.Stock.View|Receive|Transfer|Count|Adjust|Wastage|ApproveAdjustment
Inventory.Location.Manage
Procurement.Supplier.View|Manage
```

- No role `Permissions.All` change needed (System Administrator already carries
  all keys). **Inventory Officer** and **Procurement Officer** starter maps were
  confirmed from the existing D1 identity seed to cover the D4 keys
  (Procurement Officer = All("Procurement") + InventoryStockView +
  InventoryProductView + DashboardsView — used as the permission-limited role in
  the D4 HTTP tests).

## 15. Reference generation & Numbering Presets

The `STM`/`STF`/`SCN`/`SADJ` prefixes resolve through the existing
`IReferenceGenerator`/`CodeGenerator` numeric sequence presets (D1
infrastructure). All reference numbers are unique, width-6 padded, and generated
concurrency-safely; see §4 for the pre-transaction contract.

## 16. Integration-Test Bugs Found & Fixed

- **⚠ Fix 1 — references inside the transaction (Npgsql):** assigning
  `STockMovement.Reference` while the unit-of-work transaction was open used
  `IReferenceGenerator` over the same connection → Npgsql "unassigned command on
  an active transaction" risk. Fixed by generating references **before**
  `BeginAsync` and threading them as an argument into `StockLedgerWriter
  .ApplyAsync`; the writer no longer owns a reference generator. Burned
  sequences on rollback are acceptable (documented).
- **⚠ Fix 2 — adjustment atomicity gap:** initially the adjustment header+lines
  were saved and only then did the posting loop begin; a movement failure left
  the header committed. Fixed by opening the unit-of-work, taking the locks,
  **adding header + lines and saving inside the transaction**, then applying the
  movements and committing; `RollbackAsync` on any failure. Regression-tested so
  an under-stock adjustment leaves no partial rows.
- **Test maintenance (legitimate):** `MigrationStartupBehaviorTests` migration
  count `4 → 5` for the D4 migration; the `ReferenceDataSeeder` must be resolved
  as `IReferenceDataSeeder` (interface registration) in the shared "Database"
  fixture tests; EF expression trees require a `StockLedgerRow(Type, Quantity)`
  projection record rather than a tuple literal; `string.Concat` in a xUnit
  `[InlineData]` attribute replaced with a literal. All part of D4's own new
  tests, not accommodations of production defects.
- **Correction-gate test addition:** `D4InventoryApiTests` gained purpose-created
  single-scope roles ("Wastage Officer", "Adjustment Officer") and users via
  `IIdentityAdministrationService.CreateRoleAsync`/`CreateUserAsync`, plus the
  new `Wastage_permission_is_separate_from_adjustment_permission` scenario that
  proves both permutations plus cross-document Get isolation and per-type audit
  rows (D4 API tests `3 → 4`).
- A transient PostgreSQL teardown race (`permission denied to terminate process`
  in `DROP DATABASE … FORCE`) surfaced once in the shared D3 fixture during a
  full-suite pass; the D3 class passes in isolation (3/3) and the full
  integration suite has since run green 124/124 — environmental, not a D4 defect.

## 17. Frontend SPA — Inventory & Products Surface

- `types.ts` mirrors every D4 contract: `Product`, `ProductSearch`,
  `ProductPrice`, `ProductCategory`, `InventoryLocation`, `Supplier`,
  `SupplierSearch`, `StockBalance`, `StockSearch`, `StockMovement`,
  `MovementSearch`, `StockTransfer(+Line/Search)`, `StockCount(+Line/Search)`,
  `StockAdjustment(+Line/Search)`.
- New permission-gated pages (routes + nav wired in `App.tsx` /
  `layout/Layout.tsx`):
  - `ProductsPage` (`/products`, `Inventory.Product.View`) — catalogue table
    with SKU/name/category/cost/price/status; create (with optional first price,
    reorder level, preferred supplier); inline price append for editors.
  - `StockPage` (`/stock`, `Inventory.Stock.View`) — balances by location and
    recent movement history; `Receive` posting (`Inventory.Stock.Receive`).
  - `TransfersPage` (`/transfers`, `Inventory.Stock.Transfer`) — drafts with
    complete/cancel actions per document.
  - `CountsPage` (`/counts`, `Inventory.Stock.Count`) — initiate, record at
    expected, complete, approve (approved gated on
    `Inventory.Stock.ApproveAdjustment`), cancel.
  - `AdjustmentsPage` (`/adjustments`, `Inventory.Stock.Adjust`) — create
    (signed quantity, reason required) and history filtered to
    `AdjustmentType=Adjustment`.
  - `WastagePage` (`/wastage`, `Inventory.Stock.Wastage`) — positive-quantity
    wastage postings and history.
  - `AlertsPage` (`/alerts`, `Inventory.Stock.View`) — reorder alerts via
    `GET /stock?reorderOnly=true`.
  - `SuppliersPage` (`/suppliers`, `Procurement.Supplier.View`/`Manage`) —
    master data + create.
- `npm run build` = `tsc --noEmit` (strict) + `vite build` — clean; built output
  copied to `src/ClubStack.Api/wwwroot`.

## 18. SPA Hosting & Desktop Shell

Unchanged and re-verified: `ApiBootstrapper.ServeClientAppIfPresent` serves
`wwwroot/index.html` with `MapFallback` for non-`/api` paths; the built bundle
with all D4 pages was copied into `wwwroot`; `ClubStack.Desktop` still loads
`https://localhost:5001` on loopback and builds 0/0.

## 19. Build Results

```
dotnet build "ClubStack.slnx" -c Debug   -> Build succeeded. 0 Warning(s) 0 Error(s)
npm run build (client/clubstack-ui)      -> tsc --noEmit clean; vite build ok
dist -> src/ClubStack.Api/wwwroot        -> refreshed
```

## 20. Test Results (D4 + Full)

```
$env:CLUBSTACK_DB_PASSWORD='<set once at User scope: CLUBSTACK_DB_PASSWORD; see docs/local-setup>'
dotnet build ClubStack.slnx -c Debug            0 Warning(s) 0 Error(s)
dotnet test ClubStack.slnx -c Debug
  Domain.Tests ......... Passed 220  0 failed   (incl. 40+ D4 InventoryDomainTests)
  Application.Tests .... Passed   1  0 failed
  Api.Tests ............ Passed   1  0 failed
  IntegrationTests ..... Passed 124  0 failed   (baseline + 4 D4 API + 5 D4 ledger + MigrationStartupBehavior 4→5)
  Total ................ Passed 346  0 failed
```

(D4-specific integration: `D4InventoryApiTests` 4/4 — anonymous 401s incl.
wastage-gated routes, full HTTP inventory flow incl. transfer pair, count
variance, wastage, adjustment, overdraw 409, reorder alert, audit rows,
Procurement-Officer 403 gates incl. wastage, plus the two single-scope-role
separation scenario; `InventoryLedgerIntegrityTests` 5/5 — concurrent receipts =
derived balance, linked transfer pair + cancel/complete rejections, count-approval
single reconciliation, atomic rejections, concurrent overdraft one-writer-wins.)

## 21. Specification Conflicts & Notes for PM

- **No blocking spec conflict; report to PM clearly:**
  1. **Count approval permission key:** Phase 7 §7 lists
     `Inventory.Stock.Count.Approve`, but Phase 8 §8.8 (and the D4 instruction's
     decision chain) use `Inventory.Stock.ApproveAdjustment`. The implementation
     follows Phase 8 (`Inventory.Stock.ApproveAdjustment`) on the approve
     endpoint; the Phase-7-style `…Count.Approve` key is **not** catalogued.
  2. **No `Inventory.Category.*` permission:** the spec defines product category
     rows but no category permission keys; category mutations are gated by
     `Inventory.Product.Create` (create) and `Inventory.Product.Edit` (update).
     Role maps that want category-only operators need a permission key that the
     spec does not define — left as-is, flagged.
3. **Wastage/adjustment permission separation — RESOLVED in this revision.** The
      original implementation gated the unified adjustments endpoint with
      `Inventory.Stock.Adjust` regardless of `AdjustmentType`, so a Wastage-only
      role could not post wastage and the Adjust key implicitly granted wastage.
      Per the PM correction gate, `StockAdjustmentsController` now authorizes
      **by document type** using the two Phase 8 keys (`Inventory.Stock.Wastage`
      for wastage, `Inventory.Stock.Adjust` for adjustments; unfiltered = both),
      covered by purpose-created single-scope roles over HTTP (§13a). A
      wastage-only role now posts wastage; adjustment-only roles are still
      forbidden from wastage; audit actions remain per type.
  4. **Approval-step for adjustments is out of D4:** adjustments and wastage post
     immediately on create (as specified); there is no Pending→Approve step in
     D4 — only count approval has one.
- No financial or stock policy was invented beyond the formats/states already
  specified.

## 22. Verification Status

- **Status: GREEN after correction gate.** Full solution builds **0 warnings /
  0 errors**; **346 tests pass, 0 failures** (Domain 220, Application 1, Api 1,
  Integration 124 incl. D4); migration `20260924123254_D4_ProductsAndInventory`
  applied; SPA builds clean (strict TS + Vite); `wwwroot` refreshed; cookie auth
  + server-side authorization verified at HTTP level (anonymous 401s,
  grant-missing 403s). Both pre-identified D4 risks (transaction-era reference
  generation; adjustment atomicity) are fixed and regression-covered. The PM's
  D4 authorization defect — wastage gated by `Inventory.Stock.Adjust` instead of
  `Inventory.Stock.Wastage` — is fixed with type-scoped authorization and the
  mandated verification tests are in (§13a), so D4 is **ready for PM re-review**;
  it is not independently closed.

## 23. Next Steps

- **D4 is NOT independently closed — awaiting PM review of §13a's correction.**
  On approval of the correction, natural next work is **D5 POS**
  (sale/return/consumption postings — the reserved movement types become live)
  and the longer procurement workflow slice; neither begins until D4 is
  authorised. Manual browser walkthrough of the new SPA pages was not performed
  (covered by HTTP + integration tests); the Desktop shell requires the API on
  `https://localhost:5001` with the dev certificate for a manual pass.