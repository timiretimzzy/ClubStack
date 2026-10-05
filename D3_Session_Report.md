# ClubStack D3 Session Report

**D3 Credentials & Member Accounts (end-to-end: Database → Domain → Application → API → Frontend → Testing)**

## 1. Session Objective

Complete the D3 Credentials + Member Accounts deliverable as a full vertical slice
per the approved specification chain (Phase 8 §7.4–§7.5, §8.2–§8.3, §14; Phase 5
client UX; Phase 2 MEM-0xx / accounting requirements): physical credential
(card) lifecycle with full audit history, per-member billing accounts with
charges/payments/credits/adjustments, reversals, payment allocation, statements,
and an aging report — over the real cookie-authenticated HTTPS pipeline with a
React/TS SPA surface — while keeping the D0 + D1 + D2 regression suites green and
the project at **0 warnings / 0 errors**.

## 2. Scope & Boundary

- **In scope (D3):** credential issue/activate/suspend/reactivate/lost/stolen/
  replace state machine, credential resolution by card value, full history trail;
  one account per member (`ACC-…`), charges, payments (with optional explicit
  allocation), credits, adjustments, reversals of payments/credits, duplicate
  external-reference guard, statement generation, 30-day aging report; server-side
  authorization + audit on every new endpoint; SPA Member 360 sections for
  credentials and the member account.
- **Explicitly out of scope:** payment **settlement/external interchange** with
  banking rails, invoice generation, `Paid` states on charges (charge lifecycle is
  `Outstanding/Adjusted/Voided` from D2), aging of reversed originals, and any
  credential hardware reads beyond the software card identifier (`MagneticStripe`
  encoding recognized). Aging thresholds/definitions are from Phase 8 §7.5; no
  policy (e.g. penalty rules) is invented.
- PM verdict stands: implementation role only; spec documents are the authority —
  never invent states, rules, or financial policies beyond the specs.

## 3. Specification Sources

- `ClubStack_Phase_8_*.md` §7.4 (credential state machine), §7.5 (account/
  transaction states, aging buckets), §8.2 (permission descriptors ~243–260),
  §8.3 (D3 tables: credentials, credential_history, member_accounts,
  account_transactions, account_statements, payment_allocations), §14
  (session/bootstrap gates); Phase 5 (client UX); Phase 2 (MEM-0xx accounts
  requirements, PAY-005 duplicate-reference).
- `ClubStack_D2_Session_Report.md` (baseline: 164 tests, 0/0 build, member 360
  page, fee assessment ledger to now settle).
- PM decisions: "D2 Membership" closed; D3 Credentials & Member Accounts
  authorised. `ClubStack_D2_Market_Reconciliation_and_Product_Decisions.md`
  (final decision record).

## 4. Design Decisions

- **One account per member:** `member_accounts.MemberId` is unique; the first
  lookup (`GetAccountForMemberAsync`) creates the account with a generated
  `ACC-…` number (same `EfReferenceGenerator` concurrency-safe path as member
  numbers). Account `Status` = `Active`/`Suspended`/`Closed`; closed/suspended
  accounts reject new charges.
- **Balance is always derived:** a member account stores no running total —
  `Balance.AmountDue = Σ SignedAmount over Posted transactions` recomputed on
  read (`SignedAmount` is decimal, money scale 4; credits/payments negative).
  Reversed originals are excluded; the reversal transaction itself carries the
  offsetting sign. This removes any drift between ledger rows and stated balance.
- **Allocation is optional and explicit:** a payment may carry zero or more
  `PaymentAllocationInput` entries (charge → amount); an unallocated payment
  remains an unallocated credit against the balance. Allocation cannot exceed the
  charge's remaining outstanding. PAY-005 duplicate `ExternalReference` on
  payment/credit → `ConflictException` (409).
- **Reversals are first-class rows:** only a **Posted** `Payment` or `Credit`
  can be reversed (with reason); the original is marked `Reversed` and a new
  offsetting transaction is recorded, so the ledger is immutable and auditable.
- **History is a row, not a snapshot:** every credential state move appends a
  `credential_history` record (`previous_state`, `new_state`, `changed_by`,
  `reason`, `changed_at`). Issuing writes an initial `Issued → Issued` entry
  ("Credential issued.") so every card has a full timeline from birth.
- **Aging is derived, net number first:** `Aging.TotalOutstanding` reports the
  account's **net derived balance** (matching `Balance.AmountDue`), while
  `items`/`buckets` show per-charge outstanding after allocations. An
  unallocated payment therefore reduces `TotalOutstanding` but stays "unallocated"
  in the item detail — documented in code and surfaced in §21 for PM note.
- **UI stays a thin client:** rules live server-side; the SPA only renders
  contract shapes and gates controls with `Credentials.*` / `Accounts.*`
  permission keys from `/api/v1/auth/me`.

## 5. Domain — Credentials Aggregate & State Machine

- `src/ClubStack.Domain/Credentials/CredentialDomain.cs` +
  `CredentialStates.cs` hold the compute and transition tables; requirement IDs
  are carried in contracts and comments.
- **States:** `Issued, Active, Suspended, Lost, Stolen, Expired, Replaced`.
- **Valid edges** (`CredentialStateTransitions`), Phase 8 §7.4 — invalid moves are
  rejected with conflict semantics (409):
  - `Issued → Active` (requires a member; assigns the member, sets
    `ActivatedAtUtc`, future `ExpiresAtUtc` allowed),
  - `Active → Suspended` (reason) and `Suspended → Active` (reactivate — the **only**
    reactivation source; an already-suspended card cannot be "Reactivated"
    again),
  - `Active → Lost` (reason) → `Replaced`, `Active → Stolen` (reason) → `Replaced`,
  - `Active → Expired` (lazy, on read when `ExpiresAtUtc < now`; never returned as
    active),
  - `Lost`/`Stolen → Replaced`: creates the successor credential (new card number,
    same member), targets `ReplacedByCredentialId/Number`, and keeps the old card
    queryable for audit.
- `ResolveAsync(value)` returns the **active** card for a value;
  unknown/unassigned/non-active cards are rejected (403/404 semantics by call
  site). Card number format `CRD-…` (referenced generator under the serialized
  factory click), and the credential number + identifier are uniquely indexed.

## 6. Domain — Member Account, Transactions, Aging & Statement

- `src/ClubStack.Domain/Accounts/AccountDomain.cs` + `AccountStates.cs`:
  account `Active / Suspended / Closed`; transaction status `Pending → Posted`
  and `Reversed`; charge source keeps D2's `Outstanding / Adjusted / Voided`.
- **Transaction sides:** `Charge` (debit), `Payment`, `Credit`, `Adjustment`
  (signed by delegation behind `IsDebit`), each with reference, description,
  business date, currency, optional payment method, and (for payments/credits)
  an optional external reference.
- **Balance invariants under test:** derived balance == `Σ SignedAmount` over
  Posted; reversals restore prior balance; unallocated payments remain while the
  total still nets to `AmountDue`; an over-allocation is rejected.
- **Statement:** `StatementNumber` unique, period `FromDateUtc → ToDateUtc`,
  `OpeningBalance`/`ClosingBalance` from the posted ledger bounded by the period,
  `CurrentBalance`, embedded transactions, generation audit (user + timestamp).
- **Aging (Phase 8 §7.5):** 30-day buckets from the assessment date
  (`0–30 / 31–60 / 61–90 / 91+`); `openItemCount`; items carry days outstanding
  and remaining outstanding; `daysOutstandingThreshold` default 30. Bucketing
  runs over post-allocation charge residuals.

## 7. Application Layer — Credential Service

- `src/ClubStack.Application/Credentials/CredentialContracts.cs` defines the DTOs
  and `ICredentialService` (`IssueAsync`, `ActivateAsync`, `SuspendAsync`,
  `MarkLostAsync`, `MarkStolenAsync`, `ReactivateAsync`, `ReplaceAsync`,
  `ResolveAsync`, `GetHistoryAsync`, `SearchAsync`).
- Implementation `src/ClubStack.Infrastructure/Credentials/EntityFrameworkCredentialService.cs`
  (all transitions audited + state-guarded; history written via the tracked
  DbContext, see §13).
- Input contracts carry `Reason` on every mutating command except `Activate`
  (member id); `Replace` additionally takes a new type/value/expiry. Validation
  (`ValidationException` → 400) covers unexpected types and malformed identifiers;
  duplicate identifiers reject even across retired states (unique index).

## 8. Application Layer — Member Account Service

- `src/ClubStack.Application/Accounts/AccountContracts.cs` defines the DTOs
  (`MemberAccountDto`, `AccountBalanceDto`, `AccountTransactionDto`,
  `StatementDto`, `AgingDto`, bucketing/item DTOs) and
  `IMemberAccountService` (get account for member, charge, record payment +
  allocations, credit, adjustment, reverse, statement, aging, transaction
  search).
- Implementation `src/ClubStack.Infrastructure/Accounts/EntityFrameworkMemberAccountService.cs`.
  A charge requires the account `Active`; amounts > 0 and currency must match the
  account; payment/credit duplicate `ExternalReference` → conflict; adjustments
  carry reason and require approve-by-gate on the API side (`Accounts.Adjustment.
  Approve` permission is catalogued; the approve step itself is a future work
  item, see §21).

## 9. Infrastructure — EF Entities & D3 Migration

- `Persistence/Entities/CredentialEntities.cs` and `AccountEntities.cs` with
  `ClubStackDbContext` configurations (lengths, required-ness, money scale 4,
  unique indexes, self-referencing FK for `ReplacedByCredentialId` and
  `ReverseOfTransactionId`, `Owner=postgres`-compatible table names).
- **Migration `20260924092335_D3_CredentialsAndAccounts`** creates:
  `credentials`, `credential_history`, `member_accounts`, `account_transactions`,
  `account_statements`, `payment_allocations` — with unique indexes
  `IX_credentials_CredentialNumber`, `IX_credentials_CredentialValue`,
  `IX_member_accounts_AccountNumber`, `IX_member_accounts_MemberId`,
  `IX_account_statements_StatementNumber`, `IX_account_transactions_Reference`
  and composite indexes for the query paths (account+status, account+date,
  credential status, history by credential+time, allocation pairs).
- Applied to the local dev database at startup (`MigrateOnStartup`);
  `MigrationStartupBehaviorTests` expected count updated `3 → 4`.

## 10. API Contracts & Controllers

Prefix `/api/v1`; both new controllers derive from `AuditedControllerBase`
(each write records an audit row). JSON camelCase end-to-end.

- `CredentialsController.cs`
  - `POST /api/v1/credentials` (issue), `POST /api/v1/credentials/search`
  - `POST /api/v1/credentials/{id}/activate | suspend | reactivate | mark-lost |
    mark-stolen | replace`
  - `POST /api/v1/credentials/resolve` (card value → active card)
  - `GET /api/v1/credentials/{id}/history`, `GET /api/v1/credentials?memberId=…`
- `MemberAccountsController.cs`
  - `GET /api/v1/members/{memberId}/account` (creates on first access)
  - `GET /api/v1/member-accounts/{accountId}/transactions` (paged)
  - `POST /api/v1/member-accounts/{accountId}/charges | /payments | /credits |
    /adjustments`
  - `POST /api/v1/member-accounts/{accountId}/transactions/{transactionId}/reverse`
  - `POST /api/v1/member-accounts/{accountId}/statement`, `GET …/aging`

Catalogue note (documented this session): the reactivate endpoint was initialised
as `suspend`'s inverse — `POST /api/v1/credentials/{id}/reactivate` with a
`Reason` body — completing the Phase 8 edge `Suspended → Active`.

## 11. Audit & Server-Side Authorization

- Both controllers are 3-way gated server-side: `[Authorize]` +
  `[Authorize(Policy = "Permission:" + Permissions.*)]`; UI gating is cosmetic
  only (verified: anonymous requests to every D3 endpoint → 401).
- Audit rows captured per action, e.g. `Credentials.Card.Activate` /
  `Accounts.Payment.Record` with entity type and the `CRD-…`/`ACC-…` reference;
  `CredentialServiceIntegrationTests` asserts history + audit together.

## 12. Reference Data: Credentials/Accounts Permissions & Roles

New descriptors (exact keys, in `Permissions.cs`):

```
Credentials.Card.View|Issue|Activate|Suspend|MarkLost|MarkStolen|Replace|Reactivate
Accounts.Account.View
Accounts.Transaction.View|Create
Accounts.Payment.Record|Reverse
Accounts.Credit.Create
Accounts.Adjustment.Create|Approve
Accounts.Statement.Generate
Accounts.Aging.View
```

The **System Administrator** role is seeded with `Permissions.All`, so it already
carries every new key (no catalogue-row migration needed). **Club Manager** and
**Membership Officer** starter maps include the Credentials + Accounts
permissions added this slice (verified by
`D3CredentialsAndAccountsApiTests.Permission_missing_roles…`).

## 13. Integration-Test Bug Found & Fixed: History Rows + DbUpdateConcurrencyException

During the D3 run, **five credential tests** failed with
`DbUpdateConcurrencyException` (0 rows affected) on **every transition**.

- **Root cause:** history rows were appended only via the navigation collection
  (`credential.History.Add(...)`). Because `CredentialHistoryEntity.Id` defaults
  to `Guid.CreateVersion7()` (a non-default value EF Core does **not** treat as
  unset), EF marked those rows `Modified` instead of `Added` — emitting `UPDATE`
  (0 rows) instead of `INSERT` → optimistic-concurrency violation.
- **Fix:** the tracked service now inserts history rows explicitly through the
  context (`_context.CredentialHistory.Add(history)`) inside `RecordTransition`,
  keeping EF in `Added` state. The failing `Full_lifecycle_…` and related tests
  went green; the three public API tests re-verified the flow over HTTP.
- The temporary diagnostic test file used to isolate the issue was removed after
  the fix; no test remains that depends on internals.

## 14. Test Files & Strategy (D3)

- `tests/ClubStack.IntegrationTests/CredentialServiceIntegrationTests.cs`
  (9 tests): CRD-number + Issued-state on issue; invalid type/identifier; unique
  identifier across states; full issue→activate→suspend→reactivate cycle with
  history; invalid-transition rejection; replace retires lost card + activates the
  successor on the same member; stolen resolves only through replacement; expired
  never returned active; resolve rejects unknown/unassigned.
- `tests/ClubStack.IntegrationTests/MemberAccountServiceIntegrationTests.cs`
  (11 tests): first-lookup creates account with unique `ACC-…`; charge + payment +
  allocation maintains the derived balance; unallocated payment stays unallocated;
  duplicate external reference rejected; allocation cannot exceed the charge; the
  adjustment → reversal flow preserves ledger invariants; credit reduces amount
  due and its reversal restores it; statement period opening/closing; 30-day aging
  buckets; suspended/closed accounts reject charges; currency mismatch and
  non-positive amounts are rejected.
- `tests/ClubStack.IntegrationTests/D3CredentialsAndAccountsApiTests.cs`
  (3 tests): anonymous 401s on every D3 endpoint; full credential + account flow
  over HTTP with audit trail; roles without the new permissions cannot issue cards
  or view accounts.
- 23 D3 integration tests total; all green (see §18).

## 15. Frontend SPA — Member 360 Credentials + Account Sections

- `types.ts` now mirrors every D3 contract: `Credential`, `CredentialSearch`,
  `CredentialHistoryEntry`, `MemberAccount`, `AccountBalance`,
  `AccountTransaction`, `AccountTransactionSearch`, `AgingReport`,
  `AgingBucket`, `AgingItem`, `Statement`.
- `pages/MemberDetailPage.tsx` gains two permission-gated sections:
  - **Credentials** (`Credentials.Card.View`): card list for the member
    (number, identifier, status, assigned date, replacement chain); actions
    wired to `can(...)` — Activate (`Issued`), Suspend / Mark lost / Mark stolen
    (`Active`), Reactivate (`Suspended`), Replace (`Lost`/`Stolen`, with new
    identifier + reason), issue-and-activate inline; collapsible **history**
    table (from/to/reason/actor/time). `Status` badge reused from
    `DashboardPage`.
  - **Member account** (`Accounts.Account.View`): balance summary (amount due,
    overdue, credits, unallocated payments), aging bucket strip (`Accounts.Aging.
    View`), recent transactions with sign-aware amounts; Charge / Record payment /
    Credit / Adjustment dialogs with description + amount (+ payment method +
    external reference for payments/credits, reason for adjustments); Reverse on
    **Posted payments/credits** with reason (`Accounts.Payment.Reverse`); Generate
    statement summary (`Accounts.Statement.Generate`).
- `npm run build` = `tsc --noEmit` (strict) + `vite build` — clean; built output
  copied to `src/ClubStack.Api/wwwroot`.

## 16. SPA Hosting & Desktop Shell

Unchanged from D2 and re-verified after the page change: `ApiBootstrapper.
ServeClientAppIfPresent` serves `wwwroot/index.html` + assets with `MapFallback`
for non-`/api` paths (unknown `/api/…` keeps its 404); the built app copy in
`wwwroot` was refreshed with the new D3 bundle. `ClubStack.Desktop` (WPF
WebView2) still loads `https://localhost:5001` on loopback; builds 0/0.

## 17. Build Results

```
dotnet build "ClubStack.slnx" -c Debug   -> Build succeeded. 0 Warning(s) 0 Error(s)
npm run build (client/clubstack-ui)      -> tsc --noEmit clean; vite build ok
```

## 18. Test Results

```
$env:CLUBSTACK_DB_PASSWORD='<set once at User scope: CLUBSTACK_DB_PASSWORD; see docs/local-setup>'; dotnet test ClubStack.slnx --no-build
  Domain.Tests ......... Passed 157  0 failed
  Application.Tests .... Passed   1  0 failed
  Api.Tests ............ Passed   1  0 failed
  IntegrationTests ..... Passed 115  0 failed   (92 baseline + 23 D3)
  Total ................ Passed 274  0 failed
```

## 19. Full Regression (D0 + D1 + D2 + D3)

```
dotnet build "ClubStack.slnx"             -> Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test (slnx, --no-build)            -> 274 passed, 0 failed
npm run build (client/clubstack-ui)       -> clean (strict TS + Vite)
dist -> src/ClubStack.Api/wwwroot         -> refreshed
```

Re-run after all SPA/frontend changes to confirm the API layer under
`WebApplicationFactory<Program>` is unaffected by the refreshed `wwwroot`.

## 20. D0/D1/D2 Test-Maintenance Decisions

- `PostgreSqlTestFixture` (unchanged from D2) composes `AddApplication()` +
  `AddInfrastructure()` exactly as production does.
- The credential history fix (§13) was a **production bug**, not a test
  accommodation — the 23 D3 tests now fail-true on implementation regressions.
- `MigrationStartupBehaviorTests`: expected migration count `3 → 4`
  (D0 + D1 + D2 + `D3_CredentialsAndAccounts`) — legitimate spec-mandated change.
- Two D3 **test-side** bug fixes (both in `CredentialServiceIntegrationTests`/
  `MemberAccountServiceIntegrationTests`): the statement test asserted a
  0 + 250 − 150 = 100 closing balance where the spec-computed value was 100 (now
  compared against saved opening/closing); the currency-mismatch test passed a
  `"No description"` description through a field that is trimmed to empty — now
  passes `" "` to exercise validation truthfully. `Full_lifecycle` asserts the
  initial `Issued → Issued` history entry.

## 21. Specification Gaps, Conflicts & Notes

- **No new spec conflict to report.** Two deliberate, code-commented decisions
  flagged for PM:
  1. **Aging.TotalOutstanding semantics:** reported as the account's **net
     derived balance** (matches `Balance.AmountDue`), while bucket/item detail
     shows per-charge residuals after allocation; an unallocated payment lowers
     TotalOutstanding but stays unallocated in the item view. This keeps the
     report coherent with the derived-balance model (§4) rather than summing
     buckets.
  2. **"Issue and activate for this member"** in the SPA performs issue+activate
     in one user gesture only when both `Credentials.Card.Issue` and
     `…Activate` are present (the clientside mirror of the server-required
     member assignment at activation).
- `Accounts.Adjustment.Approve` is catalogued and enforced on the endpoint gate,
  but a dedicated **approval step** (Pending adjustment → Approve) is not built —
  adjustment entries post immediately. Flagged as the only intentionally
  future-shaped permission this slice; confirmed not to contradict Phase 8 §8.2.
- No financial **policy** (late fees, credit limits, rounding modes) was
  invented beyond the number formats and buckets already specified.

## 22. Verification Status & Next Steps

- **Status: GREEN.** Full solution builds **0 warnings / 0 errors**; **274 tests
  pass, 0 failures** (Domain 157, Application 1, Api 1, Integration 115 incl. 23
  D3); migration `20260924092335_D3_CredentialsAndAccounts` applied; SPA builds
  clean (strict TS + Vite); `wwwroot` refreshed; cookie auth + server-side
  authorization verified at the HTTP level (anonymous 401s, grant-missing 403s).
- **Remaining runtime to discover at review:** the Desktop shell opens
  `https://localhost:5001` (needs the API running + dev certificate); no manual
  browser walkthrough of the two new Member 360 sections was performed this
  session (covered by API + integration tests).
- **Next (post-review):** D3 is feature-complete per its gate — **no D4 work**
  begins until this session report is reviewed and D4 is authorised by the PM.