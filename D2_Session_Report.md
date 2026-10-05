# ClubStack D2 Session Report

**D2 Membership (end-to-end: Database → Domain → Application → API → Frontend → Testing)**

## 1. Session Objective

Complete the D2 Membership deliverable as a full vertical slice per the approved
specification chain (Phase 6 §9, Phase 8 §7.1–§7.3/§8.3, Phase 5 §25–30, Phase 2
MEM-0xx requirements): membership categories and fee schedules, membership
applications through submission/review/approval, member and membership-period
lifecycle with status history, favourable numbers, households and dependants,
traceable fee assessments with proration, and a working client UI over a real
cookie-authenticated HTTPS session — while keeping the D0 + D1 regression suites
green and the project at 0 warnings / 0 errors.

## 2. Scope & Boundary

- **In scope (D2):** categories + fees, applications, approval (creates member +
  pending membership), activation, suspend/reinstate, renew, cancel, transfer,
  member numbers (`MEM-…`), households, dependants, fee assessment/adjust/void
  with proration, audit + server-side authorization on every new endpoint,
  React/TS SPA and Desktop WebView2 shell wiring.
- **Explicitly out of scope:** fee **settlement** (`Paid` state, receipts,
  statements) is deferred to D3; no reservations; no invoice generation. The
  assessment ledger is D2's boundary — assessments are recorded, never settled.
- PM verdict stands: D1 accepted/closed, D2 Membership authorised. Implementation
  role only; spec documents are the authority — never invent states or rules.

## 3. Specification Sources

- `ClubStack_Phase_6_*.md` §9 (membership scope), Phase 8 §7 (state machines),
  §8.3 (tables: categories, applications, members, memberships, memberships_audit,
  households, dependants, fee_configuration, fee_assessments), §21/§23 (bootstrap
  gate, sessions), Phase 5 §25–30 (client UX), Phase 2 MEM-021/022/040/041/042/
  043/044 (categories/fees/assessments).
- `ClubStack_D1_Session_Report.md` (baseline: 78 tests, 0/0 build, permission
  catalogue, roles, cookie/session plumbing).
- PM decision "D2 Membership"; D1 baseline build 0 warnings / 0 errors.

## 4. Design Decisions

- **Aggregate ownership:** `MembershipApplication`, `Member`, `Membership`,
  `MembershipCategory` and their fee/assessment/dependant/household aggregates are
  persisted through EF entities; all transitions go through the Application layer
  (`IMembershipLifecycleService`, `IMembershipApplicationService`,
  `IMembershipDirectoryService`, `IMembershipFeeService`,
  `IMembershipCategoryService`) — no repository bypass.
- **Member number is an identity singleton:** unique `IX_members_MemberNumber`
  database index + `EfReferenceGenerator` under a serialized
  `IDbContextFactory` click (concurrency-safe; verified by a 16-way concurrent
  approval test). Format `MEM-000124` (prefix `MEM`, width 6). Never a primary key.
- **Approval is a transaction:** approve ⇒ Member (`Prospect`) + Pending
  membership + application `Approved` in one committed unit; activation is a
  separate command (application `Approved → Activated`, membership `Pending →
  Active` with `StartDateUtc`/`EndDateUtc`, member `Prospect → Active`).
- **Status history is a first-class row:** every membership transition writes a
  `membership_audit`-style status-history record (`previous_state`, `new_state`,
  `changed_by`, `reason`, `changed_at`); history is queryable per membership.
- **Expiry is lazy:** a membership whose `EndDateUtc < now` is surfaced as
  `Expired` on read (no background tick); renew only from `Expired`.
- **Money stays decimal:** amounts scaled with money scale (4) via
  `FeeProration`; no double/float anywhere in the domain compute.
- **Proration is explicit:** `amount × remainingDays / periodDays` only when the
  category fee has `IsProrationEnabled`; same-day assessment yields
  `remainingDays < periodDays` (sub-day clock deltas) and therefore prorates —
  the API-level full-amount assertion therefore uses `IsProrationEnabled = false`.
- **UI is a thin client:** all state rules live server-side; the SPA only
  re-renders contract shapes and gates navigation with `Membership.*` permission
  keys read from `/api/v1/auth/me`.

## 5. Domain — Purpose Codes & Aggregates

- `src/ClubStack.Domain/Membership/MembershipDomain.cs` / `MembershipStates.cs`
  hold the compute and transition tables. Phase 2 MEM requirement IDs are carried
  into method contracts and comments (MEM-021/022 categories, MEM-040 fee
  configuration, MEM-041 assess, MEM-042 adjust, MEM-043 proration, MEM-044 void).
- Aggregates: `MembershipCategory` (period months, fee schedule), `FeeRule`
  (`FeeType`, amount, currency, proration flag), `MembershipApplication`
  (person, category, sponsor/referrer, required documents, review/approval audit
  fields), `Member` (person link, number, status), `Membership` (member, category,
  period, status), `Household` (primary member + member roles), `Dependant`
  (relation type — explicit, never inferred), `FeeAssessment` (charge ledger).

## 6. Domain — State Machines

Valid edges only (`MembershipStateTransitions`); invalid transitions are rejected
with conflict semantics rather than silently ignored:

- **Application:** `Draft → Submitted → UnderReview → Approved → Activated`;
  `Submitted/UnderReview → Rejected`.
- **Member:** `Prospect → Active → Suspended → Active → Cancelled`.
- **Membership:** `Pending → Active → Suspended → Active → Expired → Active`
  (renew); `Active → Cancelled`; `Pending/Active → Transferred` (source closed;
  successor **Pending** membership created with `PreviousMembershipId`).

## 7. Member Number Generation

- `EfReferenceGenerator.GenerateAsync("MEM", 6)` under the serialised
  `IDbContextFactory`, backed by the unique `IX_members_MemberNumber` index.
- `Concurrent_approvals_allocate_unique_member_numbers` (16 parallel approvals)
  verifies uniqueness end-to-end; `Database_rejects_a_duplicate_member_number`
  and `Member_number_registry_is_the_identity_singleton` pin the invariant.

## 8. Fee Assessment & Proration

- Assess requires an `Outstanding`-eligible assessment date; stores
  `assessment_date`, `due_date = assessment + 30 days`, `source`, `amount`,
  `currency`, proration flag and status.
- Status flow: `Outstanding → Adjusted` (new amount + reason) or
  `Outstanding → Voided` (reason). Assessment commands require
  `Membership.Fee.Assess` / `Membership.Fee.Adjust`.
- Proration math (`FeeProration`): full when `remainingDays >= periodDays`,
  otherwise `amount × remainingDays / periodDays` rounded at money scale 4.

## 9. Application Layer

- Four audited services under `src/ClubStack.Application/Membership/`:
  - `IMembershipCategoryService` — create/update category, configure fees.
  - `IMembershipApplicationService` — countersigned submit / start-review /
    approve (person must not already be an active member) / reject.
  - `IMembershipLifecycleService` — activate, suspend, reinstate, renew, cancel,
    transfer, membership query, status history.
  - `IMembershipDirectoryService` — member/person search, member 360, households,
    dependants; dupe hints on person create.
  - `IMembershipFeeService` — assess, adjust, void, search by membership/status,
    paged.
- Every write passes through `ValidationException` / `NotFoundException` /
  `ConflictException` / `ConcurrencyException` mapping to 400/404/409/409 by
  `GlobalExceptionHandler` (concurrency produces `409 ConcurrencyConflict`).

## 10. Infrastructure — EF & Migration

- Entities under `Infrastructure/Persistence/Entities/MembershipEntities.cs` with
  `ClubStackDbContext` configurations (lengths, required-ness, unique indexes,
  money scale, audit columns) and `Owner=postgres`-compatible table names.
- **Migration `20260924064216_D2_Membership`** adds the D2 schema; applied to the
  local dev database at startup (`MigrateOnStartup`). Migration count test updated
  `2 → 3` accordingly.

## 11. API Contracts & Controllers

Prefix `/api/v1` (`ApiConstants`); all new controllers derive from
`AuditedControllerBase` (each write records an audit row). Surface:

- `membership-categories` (+ `…/fees`, `…/fees/{feeId}`)
- `membership-applications` (+ `submit`, `start-review`, `approve`, `reject`)
- `members`, `members/{id}`, `members/{id}/memberships`, `members/{id}/dependants`
- `memberships/{id}` (+ `history`, `activate`, `suspend`, `reinstate`, `renew`,
  `cancel`, `transfer`)
- `households` (+ `…/{id}/members`, remove member)
- `membership-fee-assessments` (+ `…/{id}/adjust`, `…/{id}/void`)

JSON uses camelCase (`PropertyNamingPolicy = CamelCase`), so the SPA posts
camelCase bodies directly.

## 12. Audit & Server-Side Authorization

- Every membership controller is a 3-way-gated server-side controller:
  `[Authorize]` + `[Authorize(Policy = "Permission:" + Permissions.*)]` — UI gating
  is cosmetic only, never authoritative (verified: anonymous requests to all
  membership endpoints → 401).
- Completed audit rows captured in tests, e.g.
  `Membership.Application.Approve` / `Membership.Membership.Activate` with
  entity type and `MEM-…` reference.
- Membership permission keys enforced this slice: `Membership.Member.View/Create/
  Activate/Suspend/Reinstate/Renew/Cancel/Transfer (and Edit/Approve in the
  catalogue)`, `Membership.Category.View/Manage`, `Membership.Application.
  View/Create/Approve`, `Membership.Household.Manage`,
  `Membership.Dependant.Manage`, `Membership.Fee.View/Assess/Adjust`.

## 13. Reference Data: Membership Permissions & Roles

- The **System Administrator** role is seeded with `Permissions.All`, therefore
  already carries every `Membership.*` permission (no new catalogue rows needed —
  D1's 163-descriptor catalogue covers Membership). **Club Manager** and
  **Membership Officer** starter maps include the Membership module permissions.

## 14. Frontend SPA (`client/clubstack-ui`)

- React 19 + TypeScript (strict) + Vite. `npm run build` = `tsc --noEmit` (strict,
  `noUnusedLocals`/`noUnusedParameters`) then `vite build` — clean.
- `src/`: `types.ts` (contract mirror), `api/client.ts` (fetch wrapper, `ApiError`
  field errors, 401 → sign-out handler), `auth/AuthContext.tsx` (`/auth/me`,
  `login`, `logout`, `can(permission)`), `layout/Layout.tsx` (permission-gated
  nav), `pages/`:
  - `LoginPage` — client-side sign-in → server cookie.
  - `DashboardPage` — at-a-glance + recent members/applications.
  - `MembersPage` — search, status filter, add-person with duplicate warnings.
  - `MemberDetailPage` — **Member 360**: details, memberships with lifecycle
    actions (activate/suspend/reinstate/renew/cancel/transfer with reason),
    status history, dependants (add/remove).
  - `ApplicationsPage` — filter, create, submit, start-review, approve, reject.
  - `CategoriesPage` — categories + fee schedules (add fee, proration flag).
  - `HouseholdsPage` — create household, primary member, add/remove members.
  - `FeeAssessmentsPage` — assess (member → membership → fee type), adjust, void.
- Vite dev proxy `/api → https://localhost:5001` keeps one browser origin (cookies
  are SameSite=Lax + Secure; proxied requests are the https leg).

## 15. SPA Hosting & Desktop Shell

- `ApiBootstrapper.ServeClientAppIfPresent`: when `wwwroot/index.html` exists, the
  API serves default files + static assets and `MapFallback` returns `index.html`
  for non-`/api` paths (anonymous fallback so the shell can render the sign-in
  page). Unknown `/api/…` paths keep their contract **404** (never HTML).
- Built app copied to `src/ClubStack.Api/wwwroot` (Vite `dist`). Live smoke
  (http://localhost:5000): `/index.html` → 200 HTML (`<title>ClubStack`),
  `/members` deep link → SPA HTML, `/assets/index-*.js`/`.css` → 200,
  `/api/v1/does-not-exist` → 404. Manual cookie login over http is intentionally
  not smoke-tested (Secure cookie) — the real cookie+session flow is covered by
  the API-level tests.
- `ClubStack.Desktop` (WPF WebView2, `Microsoft.Web.WebView2` already referenced):
  loads `https://localhost:5001/` (override `CLUBSTACK_WEB_URL`), accepts the local
  dev certificate on loopback via `ServerCertificateErrorDetected`, shows load
  status in a status bar. Builds 0/0.

## 16. Test Files & Strategy

- `tests/ClubStack.IntegrationTests/MembershipLifecycleIntegrationTests.cs`
  (13 tests): category/fee config, application lifecycle, activation, concurrent
  member-number uniqueness, suspend/reinstate/cancel corrections, renewal, transfer
  successor, rejection dead-end, fee assessment/proration, duplicate-number index,
  person-with-active-member apply guard, dependants/households, number singleton.
- `tests/ClubStack.IntegrationTests/D2MembershipApiTests.cs` (4 tests) — hermetic
  `WebApplicationFactory<Program>` over the **real HTTP pipeline** with a scratch
  DB: anonymous 401s on every membership endpoint; full lifecycle over HTTP with
  audit trail; conflict semantics (duplicate category config, reject-after-activate,
  suspend-while-suspended); 400/404 contract statuses.
- Harness/tests maintenance (see §19): `PostgreSqlTestFixture` now composes both
  `AddApplication()` + `AddInfrastructure()` (mirrors `ApiBootstrapper`),
  directory-service `Include` fix, suspend-test reorder, concurrent-approval helper
  advances to `UnderReview` first, `MigrationStartupBehaviorTests` count `2 → 3`.

## 17. Test Results

```
$env:CLUBSTACK_DB_PASSWORD='<set once at User scope: CLUBSTACK_DB_PASSWORD; see docs/local-setup>'; dotnet test ClubStack.slnx -c Debug
  Domain.Tests ......... Passed  70  0 failed
  Application.Tests .... Passed   1  0 failed
  Api.Tests ............ Passed   1  0 failed
  IntegrationTests ..... Passed  92  0 failed   (75 baseline + 13 lifecycle + 4 API)
  Total ................ Passed 164  0 failed
```

## 18. Full Regression (D0 + D1 + D2)

```
dotnet build "ClubStack.slnx" -c Debug   -> Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  (after all SPA/hosting changes) -> 164 passed, 0 failed
```

Re-run after the static-hosting/`MapFallback` change to confirm the API layer under
`WebApplicationFactory<Program>` is unaffected by the presence of `wwwroot`.
Client build `npm run build` clean (strict TS + Vite).

## 19. D0/D1 Test-Maintenance Decisions

- `PostgreSqlTestFixture.TestServiceProvider.Build` was calling only
  `AddInfrastructure()`; production composes `AddApplication()` (registers
  `ISystemClock` + CQRS handlers) **then** `AddInfrastructure()`. Fixed to mirror
  production (`ISystemClock` was unresolvable in the fixture host).
- `EntityFrameworkMembershipDirectoryService`: household DTO projection threw on a
  null `PrimaryMember.Person` (nav not included); fixed with
  `.Include(h => h.PrimaryMember!.Person)` in `ListHouseholdsAsync` and
  `GetHouseholdAsync` (also satisfies `TreatWarningsAsErrors`).
- `Suspend_reinstate_and_cancel…`: test bug — the invalid repeat is
  suspend-while-suspended; suspend-after-reinstate is *valid*; reordered.
- `Concurrent_approvals…`: helper now calls `StartReviewApplicationAsync` so
  approvals run from `UnderReview`.
- `D2MembershipApiTests.Anonymous…`: `InitializeAsync` must not log in (login per
  test) — otherwise every test carried an auth cookie and the anonymous test was
  moot.
- `MigrationStartupBehaviorTests`: expected migration count `2 → 3`
  (D0 + D1 + `D2_Membership`) — legitimate spec-mandated change.

## 20. Specification Gaps, Conflicts & Notes

- **No new spec conflict to report.** The only deliberate boundary: household
  records are name + primary member + member roles (Phase 8 §8.3); the Phase 5
  “address block” expectations are not modeled as household columns this slice
  (persons already carry contact data). Flagged for PM confirmation, not silently
  invented.
- Fee **settlement** is intentionally absent (D3); the working agreement to use
  amount fields at money scale 4 and `Outstanding/Adjusted/Voided` states is
  unchanged.
- Marketing/legal verification of application content is expressed through the
  explicit review/reject flow rather than a bespoke workflow step, matching the
  Phase 8 §7.1 states.

## 21. Verification Status & Next Steps

- **Status: GREEN.** Full solution builds **0 warnings / 0 errors**; **164 tests
  pass, 0 failures** (Domain 70, Application 1, Api 1, Integration 92); migration
  `20260924064216_D2_Membership` applied; SPA builds clean; SPA static hosting +
  deep-link fallback + API 404 preservation verified live; cookie
  authentication/session enforcement for membership endpoints verified at the
  HTTP level by the API tests.
- **Remaining runtime to discover at review:** the Desktop shell opens
  `https://localhost:5001` (needs the API running + dev certificate).
- **Next (post-review):** D2 is feature-complete per its gate — **no D3 work**
  (fee settlement, receipts, statements) begins until the session report is
  reviewed and D3 is authorised by the PM.