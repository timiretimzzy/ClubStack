# ClubStack D6 — Finance Integration
## Authoritative implementation session

You are now starting D6 Finance Integration for ClubStack.

D5.5 is CLOSED and ACCEPTED / PASS.

Do NOT reopen D5.5.
Do NOT work on the FiscalStackZW DLL.
Do NOT attempt to fix CloseDay / BadCertificateSignature.
Do NOT re-test the live fiscal device unless a D6 requirement explicitly depends on an existing fiscal record.
Do NOT ask for the local PostgreSQL password. The local credential is already persistently configured on the development machine and must continue to be used automatically.

Your role in this session is IMPLEMENTATION ONLY.

The PM/product/architecture decisions already exist in the authoritative ClubStack specification set.

You must read those specifications first and implement D6 exactly as specified.

Do not invent product requirements.
Do not silently change the architecture.
Do not introduce features from D7+.
Do not reinterpret existing D2/D3/D4/D5 behavior.

--------------------------------------------------
## 1. AUTHORITATIVE DOCUMENT ORDER
--------------------------------------------------

Before writing code, read the current authoritative ClubStack documentation available in the repository.

At minimum inspect:

- Product Bible
- Requirements Specification
- Domain and Data Model
- Technical Architecture
- UX/UI Specification
- Development Planning and Implementation Specification
- Pre-Coding Review / Gap Closure
- Detailed Pre-Coding Specifications
- Final Pre-Coding Consistency Audit
- D0 implementation/acceptance material
- D1 session/acceptance material
- D2 acceptance material
- D3 acceptance material
- D4 acceptance material
- D5 acceptance material
- D5.5 acceptance and database handoff material
- `docs/pm-prompts/README.md`

Then identify the authoritative D6 Finance Integration requirements.

Where documents overlap:

1. later authoritative specification wins
2. explicit PM decisions win
3. already accepted implementation behavior must be preserved unless the D6 specification explicitly changes it

Do not resolve a specification conflict by guessing.

If a genuine product/domain/architecture decision is required, STOP and report the conflict rather than inventing a decision.

--------------------------------------------------
## 2. D6 SCOPE
--------------------------------------------------

Implement ONLY D6 Finance Integration.

D6 must integrate the existing ClubStack operational modules with the finance/accounting capabilities specified by the authoritative D6 requirements.

Use the existing authoritative records.

Do NOT create duplicate financial versions of:

- members
- member accounts
- products
- sales
- refunds
- payments
- inventory transactions
- POS transactions

Where D6 needs financial effects, consume the authoritative operational records already established by D2-D5.

The accounting/finance layer must be downstream of those authoritative transactions unless the D6 specification explicitly states otherwise.

Do not create disconnected duplicate records merely to make D6 easier to implement.

--------------------------------------------------
## 3. PRESERVE EXISTING BOUNDARIES
--------------------------------------------------

Preserve the existing architecture:

Domain
→ Application
→ Infrastructure
→ API
→ Client

Domain must not depend on Infrastructure.

Application must not depend directly on EF Core, PostgreSQL, FiscalStackZW, or other infrastructure implementations.

Infrastructure owns persistence, external integrations and implementation details.

API owns HTTP contracts/controllers.

Client uses the API.

Follow the established ClubStack modular-monolith structure.

--------------------------------------------------
## 4. BUILD ON EXISTING D3/D5 FINANCIAL RECORDS
--------------------------------------------------

D3 already owns member-account financial transactions and balances.

D5 already owns:

- POS sales
- tender lines
- discounts
- refunds
- cashier sessions
- cash movements
- expected cash
- reconciliation

D6 must integrate these according to the authoritative finance specification.

Do NOT move ownership of those records into D6.

Do NOT duplicate balances.

Do NOT create an alternative member-account ledger.

Do NOT make POS responsible for general finance accounting.

Do NOT make D6 responsible for membership lifecycle.

--------------------------------------------------
## 5. TRANSACTIONAL INTEGRITY
--------------------------------------------------

Financial postings must be atomic according to the D6 specification.

Use the existing ClubStack transaction behavior and EF Core patterns.

Follow the established:

- unit-of-work/transaction behavior
- concurrency handling
- idempotency behavior
- audit behavior
- reference generation
- outbox behavior where asynchronous integration is specified

Do not create partially posted financial transactions.

Do not allow the same operational event to create duplicate financial effects.

Where a unique database constraint is required, use the database as the final line of defense.

Application checks alone are insufficient.

--------------------------------------------------
## 6. EVENT / OUTBOX INTEGRATION
--------------------------------------------------

Where the D6 specification requires asynchronous finance processing:

use the existing Outbox infrastructure.

Do NOT bypass the outbox with a direct service-to-service call where the specification expects an event.

Use the existing:

- OutboxMessages
- OutboxProcessorJob
- handler registration
- retry behavior
- processed/failed state handling
- provider scope factory

Follow the D5.5 proven pattern.

The integration tests must exercise the real outbox processor, not a mocked direct invocation of the final handler.

--------------------------------------------------
## 7. DATABASE / MIGRATIONS
--------------------------------------------------

Add D6 migrations to:

`src/ClubStack.Infrastructure/Persistence/Migrations/`

There are currently 10 migrations.

`MigrationStartupBehaviorTests` currently asserts the exact migration count.

When you create the first D6 migration:

UPDATE that assertion from 10 to the new authoritative count.

Do not weaken the assertion.

Do not remove the test.

Do not replace migrations with `EnsureCreated`.

All schema changes must be represented by real EF Core migrations.

Use the established PostgreSQL patterns.

--------------------------------------------------
## 8. REFERENCE GENERATION
--------------------------------------------------

Any new D6 business references must use the existing ClubStack reference-generation mechanism.

Do not invent ad-hoc GUID-only user-facing references where the specifications require business references.

Follow the established prefixes, formatting and uniqueness rules from the authoritative D6 specification.

--------------------------------------------------
## 9. PERMISSIONS / AUTHORIZATION
--------------------------------------------------

Use the existing permission catalogue and authorization model.

Do not create ad-hoc role checks.

API endpoints must use the established:

`RequirePermission`

pattern.

Use the exact D6 permissions defined by the authoritative specification.

If a permission is not defined but appears necessary, STOP and report it rather than inventing its name.

Sensitive finance operations must be appropriately protected and audited according to the specification.

--------------------------------------------------
## 10. AUDIT
--------------------------------------------------

Finance operations that mutate financial state must produce the required audit trail.

Audit entries must identify the relevant:

- user/actor
- business reference
- operation
- entity
- timestamp
- correlation context

Do not store secrets in audit metadata.

Never store passwords, activation keys, fiscal licenses or other secret material.

--------------------------------------------------
## 11. IDEMPOTENCY
--------------------------------------------------

Every D6 operation that can be retried must have the correct idempotency behavior.

Follow the patterns already proven in D5/D5.5.

Test:

- same request repeated
- same idempotency key with same payload
- same idempotency key with different payload
- concurrent duplicate requests
- retry after partial processing
- retry after successful processing

Where D6 creates unique financial effects, enforce that invariant in PostgreSQL as well as in application logic.

--------------------------------------------------
## 12. CONCURRENCY
--------------------------------------------------

Follow the existing ClubStack concurrency strategy.

Do not silently broaden concurrency-token usage.

Where concurrent financial operations can target the same aggregate/resource, ensure the behavior matches the authoritative D6 specification.

Test real concurrency where required.

Do not simply assert that two sequential calls work.

--------------------------------------------------
## 13. API
--------------------------------------------------

Implement the D6 API exactly according to the authoritative route and contract specification.

Follow existing API conventions for:

- route naming
- versioning
- request DTOs
- response DTOs
- ProblemDetails
- validation
- authorization
- pagination/filtering
- correlation IDs
- idempotency
- error handling

Do not create undocumented endpoints just because they are convenient for testing.

--------------------------------------------------
## 14. CLIENT / SPA
--------------------------------------------------

Implement the D6 UI specified by the UX specification.

Do not add generic finance dashboards merely because they look useful.

Implement the exact D6 screens/workflows required by the specification.

Use the existing ClubStack UI conventions:

- routing
- permission-gated navigation
- API client patterns
- loading/error states
- validation
- table/list patterns
- detail pages
- mutation feedback

Maintain strict TypeScript.

Run:

`tsc --noEmit`

and:

`npm run build`

--------------------------------------------------
## 15. REPORTING / READ MODELS
--------------------------------------------------

If D6 specifies financial reporting/read models:

keep those read models derived from authoritative transactional records.

Do not create a second source of truth.

Any summary/balance/reporting values must have a clearly defined authoritative source.

Where a cached/derived financial value is specified, define and test its reconciliation behavior.

--------------------------------------------------
## 16. ACCOUNTING PERIOD / POSTING RULES
--------------------------------------------------

Implement exactly what the D6 specification defines around:

- posting dates
- transaction dates
- accounting periods
- posting states
- corrections
- reversals
- voids
- financial adjustments
- period locking
- reopening

Do NOT invent accounting policy.

Do NOT introduce period-close behavior if it is actually a later phase.

Read the D6 specification carefully before implementing any of these.

--------------------------------------------------
## 17. MONEY / CURRENCY
--------------------------------------------------

Use the project's established monetary representation.

Do not introduce floating-point arithmetic for persisted financial amounts.

Respect ClubStack's multi-currency requirements already established in the specifications.

Do not assume USD everywhere.

Do not hard-code Zimbabwean currency behavior beyond what the approved requirements specify.

Where exchange rates are required by D6, implement exactly the approved model and source.

--------------------------------------------------
## 18. TAX
--------------------------------------------------

Do not duplicate or hard-code ZIMRA tax configuration.

D5.5 established that device tax identifiers must come from the device configuration where fiscalisation requires them.

D6 must use the authoritative finance/tax rules specified by ClubStack.

Do not replace configurable tax definitions with magic numbers.

--------------------------------------------------
## 19. TEST DATABASE
--------------------------------------------------

Use the existing real PostgreSQL integration-test fixture.

Do NOT use:

- `EnsureCreated`
- SQLite substitutes
- in-memory EF as the only proof
- hand-written fake database behavior

Follow the established pattern:

- `[Collection("Database")]`
- `DatabaseTestBase`
- unique test prefixes
- scratch database creation
- real migrations
- provider scope factory
- real outbox processor where applicable

The local PostgreSQL credential is already configured persistently.

Do not ask the user for it.

--------------------------------------------------
## 20. REQUIRED TEST COVERAGE
--------------------------------------------------

Create real D6 tests covering the authoritative requirements.

At minimum, wherever applicable:

- entity creation
- validation
- authorization
- persistence
- migrations
- transaction boundaries
- idempotency
- concurrency
- audit
- reference generation
- API behavior
- outbox creation
- outbox processing
- retry behavior
- failure behavior
- reversal/correction behavior
- reporting/read model behavior
- duplicate prevention
- permission denial
- unauthorized access
- database constraints

Use real PostgreSQL for persistence/integration verification.

Do not inflate test counts with trivial tests.

Tests must prove business invariants.

--------------------------------------------------
## 21. REGRESSION
--------------------------------------------------

Before declaring D6 complete:

run:

`dotnet build ClubStack.slnx`

`dotnet test ClubStack.slnx`

and:

`npm run build`

Do not accept skipped tests as equivalent to passing tests.

Pay particular attention to D5/D5.5 regression.

D5.5 must remain PASS.

Do not modify existing D5.5 acceptance behavior merely to make D6 easier.

--------------------------------------------------
## 22. DOCUMENTATION
--------------------------------------------------

Update the appropriate project documentation with the D6 implementation state.

At minimum ensure:

- D6 implementation status is reflected
- migration count is correct
- new permissions are documented
- new routes are documented where required
- new configuration is documented
- setup instructions remain correct
- local DB credential setup remains documented without exposing the actual password

Never write the actual database password into documentation.

--------------------------------------------------
## 23. OUT OF SCOPE
--------------------------------------------------

Do not start:

D7 Golf
D8 Tournaments
D9 Procurement
D10 Food & Beverage
D11 Pro Shop
D12 Assets
D13 Facilities
D14 Events
D15 Reporting
D16 Hardware Integration
D17 Full-System QA
D18 Production Readiness

Do not implement unrelated future features because their models are nearby.

--------------------------------------------------
## 24. STOP CONDITIONS
--------------------------------------------------

STOP and report instead of guessing if you encounter:

- conflicting authoritative specifications
- missing D6 product decision
- undefined business rule
- undefined accounting policy
- undefined permission
- undefined ownership boundary
- undefined financial state transition

Do not silently decide these yourself.

--------------------------------------------------
## 25. SESSION REPORT IS MANDATORY
--------------------------------------------------

At the end of the session create:

`D6_Implementation_Session_Report.md`

The report must clearly separate CURRENT SESSION WORK from PRIOR ACCEPTED WORK.

Include:

1. Objective
2. Authoritative documents read
3. D6 scope implemented
4. Product/domain decisions relied upon
5. Architecture decisions relied upon
6. Files created
7. Files modified
8. Database schema changes
9. Migrations created
10. Domain changes
11. Application changes
12. Infrastructure changes
13. API changes
14. Permission changes
15. Audit changes
16. Outbox/event changes
17. Idempotency behavior
18. Concurrency behavior
19. UI changes
20. Security changes
21. Test coverage added
22. Exact test results
23. Build result
24. SPA result
25. Regression result
26. Defects discovered
27. Defects fixed
28. Known limitations
29. Deferred decisions/issues
30. D5.5 regression status
31. D6 acceptance status
32. Exact next-session entry point

Every test result must be factual.

Do not claim live behavior that was not actually executed.

Do not claim an integration path was proven if only a mock/fake was used.

--------------------------------------------------
## 26. FINAL ACCEPTANCE STANDARD
--------------------------------------------------

D6 is complete only when:

- the authoritative D6 requirements are implemented
- all required migrations exist
- permissions are enforced
- audit requirements are satisfied
- idempotency is proven
- concurrency behavior is proven where required
- real PostgreSQL integration tests pass
- outbox behavior is proven where required
- API tests pass
- UI builds
- full regression passes
- build has 0 warnings / 0 errors
- no D7+ work has leaked into the implementation
- no unresolved product/architecture decisions were silently invented

Do not declare D6 complete merely because the code compiles.

Do not stop after creating the schema.

Do not stop after unit tests.

Exercise the actual application path specified by D6.

--------------------------------------------------
## 27. FINAL INSTRUCTION
--------------------------------------------------

Start D6 now.

First read the authoritative specifications and existing accepted implementation patterns.

Then implement D6.

Use the existing persistent local PostgreSQL configuration.

Do not ask for the database password.

Do not reopen D5.5.

Do not touch the vendor DLL.

Do not start D7.

Produce the complete D6 session report.

STOP after the report.
