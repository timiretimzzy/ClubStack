# ClubStack D8 — Tournaments + Scoring
## Authoritative implementation session

D7 IS COMPLETE AND ACCEPTED.

D7 status:
- 913/913 tests passed
- 0 failed
- 0 skipped
- build: 0 warnings / 0 errors
- TypeScript clean
- SPA builds
- D2/D3/D4/D5/D5.5/D6 remain green
- D7 Golf is CLOSED

DO NOT reopen D7.
DO NOT modify the FiscalStackZW DLL.
DO NOT revisit D6 accounting decisions.
DO NOT start D9 or any later phase.

You are now implementing D8: TOURNAMENTS + SCORING.

--------------------------------------------------
## 1. FIRST READ THE AUTHORITATIVE D8 MATERIAL
--------------------------------------------------

There is currently no dedicated D8 PM prompt.

Read the authoritative specification set before writing code:

- ClubStack Phase 0
- Phase 1 Product Bible
- Phase 2 Requirements Specification
- Phase 3 Domain and Data Model
- Phase 4 Technical Architecture
- Phase 5 UX/UI Specification
- Phase 6 Development Planning and Implementation Specification
- Phase 7 Pre-Coding Review and Specification Gap Closure
- Phase 8 Detailed Pre-Coding Specifications
- Phase 9 Final Pre-Coding Consistency Audit
- D6 Implementation Session Report
- D7 Implementation Session Report
- docs/pm-prompts/README.md

D8 is explicitly the tournament/scoring phase.

The D7 report identifies these already-seeded permissions waiting for D8:

- Golf.Tournament.View
- Golf.Tournament.Manage
- Golf.Scoring.Manage

Read the exact tournament/scoring requirements and implement only what they authorize.

Do not infer tournament rules from generic golf software.

--------------------------------------------------
## 2. D8 SCOPE
--------------------------------------------------

Implement ONLY:

- tournaments
- tournament entries
- tournament/player participation
- scoring
- scorecards
- scoring workflows
- tournament-specific golf integrations defined by the specification

Do NOT implement:

- D9 Procurement
- D10 Food & Beverage
- D11 Pro Shop
- D12 Assets
- D13 Facilities
- D14 Events
- D15 Reporting
- D16 Hardware Integration
- D17 Full-System QA
- D18 Production Readiness

Do not expand the phase because nearby functionality appears useful.

--------------------------------------------------
## 3. EXTEND D7, DO NOT DUPLICATE IT
--------------------------------------------------

D7 already owns:

- golf bookings
- tee slots
- capacity
- players
- golf guests
- charges
- product-based fee resolution
- D6 finance posting
- idempotency
- booking state machine

D8 must EXTEND those systems.

A tournament entry is built on the existing golf booking/player machinery where the authoritative specification calls for that relationship.

Do NOT create a second:

- booking model
- capacity engine
- golf guest model
- green-fee engine
- finance posting engine
- payment system
- product catalogue
- member/customer master

The D7 report explicitly identifies the existing booking/capacity/fee/posting machinery as the D8 integration surface.

--------------------------------------------------
## 4. DO NOT INVENT TOURNAMENT POLICY
--------------------------------------------------

Read the authoritative D8 requirements carefully.

Do not invent:

- tournament formats
- handicapping rules
- scoring calculations
- tie-break rules
- qualification rules
- entry eligibility
- player limits
- cut rules
- score validation
- score approval rules
- leaderboard behavior
- competition fee rules

unless the authoritative specification explicitly defines them.

If the specifications contain a genuine unresolved business rule:

STOP.

Document the exact missing decision and the exact implementation it blocks.

Do not silently choose an interpretation.

--------------------------------------------------
## 5. DOMAIN
--------------------------------------------------

Model D8 according to the authoritative specification.

Expected conceptual areas may include tournament configuration, entries, participants, rounds, scorecards and scores, but DO NOT create these merely because they sound reasonable.

Use only entities required by the specification.

Follow existing ClubStack domain rules:

- UUIDv7 IDs
- business references separate from PKs
- explicit lifecycle states
- concurrency tokens
- immutable records where required
- no Infrastructure dependency in Domain

--------------------------------------------------
## 6. TOURNAMENT ENTRY
--------------------------------------------------

Where the D8 specification requires tournament entry:

reuse D7:

- Member
- Golf Guest
- Golf Booking
- TeeSlot
- capacity
- Golf Charge
- D6 finance posting

Do not create duplicate participant identity.

Where entry creates a booking or consumes capacity, use the D7 mechanisms rather than reimplementing them.

Where entry creates money movement, use D7/D6 posting architecture.

--------------------------------------------------
## 7. SCORING
--------------------------------------------------

Implement the exact scoring model in the authoritative specification.

Score records must:

- preserve the relevant tournament/round/player relationship
- respect required immutability or edit rules
- support the specified validation
- be auditable
- prevent unauthorized modification
- preserve the historical result

Do not invent handicap calculations, stableford calculations, stroke-play calculations, or other scoring mathematics unless explicitly specified.

--------------------------------------------------
## 8. TOURNAMENT LIFECYCLE
--------------------------------------------------

Implement exactly the defined tournament state machine.

Do not add states.

Do not remove states.

Do not create an implicit lifecycle through nullable fields.

Where the specification requires a state transition:

- validate it in application/domain logic
- enforce critical transitions at database level where feasible
- audit the transition
- enforce authorization

--------------------------------------------------
## 9. DATABASE
--------------------------------------------------

Use the real migration path:

src/ClubStack.Infrastructure/Migrations/

Do NOT use:

src/ClubStack.Infrastructure/Persistence/Migrations/

Create real EF Core migrations.

Do not use EnsureCreated.

After adding the D8 migration, update:

tests/ClubStack.IntegrationTests/MigrationStartupBehaviorTests.cs

from the current exact migration count to the new count.

Do not weaken or remove that assertion.

Use the persistent local PostgreSQL configuration.

DO NOT ASK THE USER FOR THE DATABASE PASSWORD.

--------------------------------------------------
## 10. CONCURRENCY
--------------------------------------------------

Pay particular attention to:

- tournament entry capacity
- entry duplication
- player registration
- score submission
- score updates
- concurrent scoring
- tournament state transitions
- finalization/publishing if specified

Use database constraints as the final line of defense.

Prove race-sensitive behavior with real PostgreSQL concurrency tests where required.

--------------------------------------------------
## 11. IDEMPOTENCY
--------------------------------------------------

Follow the D7/D6 pattern.

Any D8 command that:

- creates a tournament entry
- creates a financial charge
- consumes capacity
- submits a score
- finalizes a score
- publishes/finalizes a tournament

must use idempotency where the specification requires retry safety.

The D7 defect #10 is explicitly relevant here.

Use:

IIdempotencyStore

with operation-scoped request fingerprints and durable replay.

Do not create a second idempotency mechanism.

--------------------------------------------------
## 12. PERMISSIONS
--------------------------------------------------

Use:

Golf.Tournament.View
Golf.Tournament.Manage
Golf.Scoring.Manage

and any additional D8 permissions explicitly defined in the authoritative specification.

Do not create additional permission names unless the specification or an explicit PM decision requires them.

Use the real ClubStack pattern:

[Authorize(Policy = "Permission:" + Permissions.X)]

Do NOT introduce RequirePermission.

Test:

- anonymous -> 401
- authenticated without permission -> 403
- correct permission -> allowed
- separation of duties where required
- cashier isolation
- scoring authorization boundaries
- tournament management boundaries

--------------------------------------------------
## 13. API
--------------------------------------------------

Follow existing ClubStack API conventions.

Use:

/api/v1/golf/...

unless the authoritative D8 specification explicitly defines a different approved route family.

Use:

- GET collection/item
- POST commands
- action routes for state transitions
- RFC7807 ProblemDetails
- field-level validation
- correlation IDs
- authorization
- idempotency headers/fields where required

If the D8 route table is missing, derive route structure from existing `/api/v1/golf` conventions and explicitly record that interpretation in the report.

Do not invent business behavior merely because a route needs to exist.

--------------------------------------------------
## 14. AUDIT
--------------------------------------------------

Audit all tournament/scoring state changes and sensitive commands.

At minimum capture according to the established audit pipeline:

- actor
- action
- entity
- entity ID
- reference
- timestamp
- correlation ID
- reason/metadata where applicable

Never store secrets.

--------------------------------------------------
## 15. FINANCE
--------------------------------------------------

D6 is authoritative for accounting.

If tournament entry fees or other D8 charges are required:

- use D7 GolfCharge
- use D4 product pricing where specified
- use D6 posting mappings
- use existing AR/payment machinery

Do NOT create tournament-specific financial ledgers.

Do NOT hard-code account IDs.

Do not invent finance posting rules.

If the D8 specification does not define a monetary behavior, do not add one.

--------------------------------------------------
## 16. UI
--------------------------------------------------

Implement the D8 UI defined by the UX specification.

At minimum, verify what the specification actually requires for:

- tournament list
- tournament setup/detail
- entries/participants
- scoring
- scorecards
- leaderboard/results
- tournament lifecycle controls

Do not create generic golf screens beyond the authoritative D8 requirement.

Follow existing ClubStack UI patterns.

Every screen must:

- be permission-gated
- handle loading
- handle empty states
- handle API errors
- prevent illegal actions
- reflect server-side authorization

--------------------------------------------------
## 17. EXISTING D7 SPA LIMITATION
--------------------------------------------------

D7 documented that its six Golf SPA screens compile/build but had not been exercised against a running API.

That limitation remains recorded for D7.

For D8:

- implement D8 UI
- exercise D8 screens against the running ClubStack API where practical
- clearly distinguish browser/API execution from compilation-only proof

Do not falsely upgrade the D7 limitation into a runtime-tested claim.

--------------------------------------------------
## 18. TESTING
--------------------------------------------------

Use real PostgreSQL integration tests.

Follow:

[Collection("Database")]
DatabaseTestBase
scratch database
real EF migrations
real provider scope factory

Create D8 tests for:

### Tournament
- creation
- validation
- state transitions
- configuration
- authorization
- duplicate prevention

### Entries
- entry creation
- member/guest eligibility
- duplicate entry prevention
- capacity
- booking integration
- fee integration
- idempotency
- concurrency

### Scoring
- score creation
- validation
- authorization
- state transitions
- duplicate score protection
- corrections/edits according to specification
- concurrency
- finalization/publishing where required

### Integration
- D7 booking integration
- D7 players/guests
- D6 financial integration where required
- audit
- outbox if required

### API
- 401
- 403
- success
- validation errors
- state errors
- idempotency conflicts

--------------------------------------------------
## 19. OUTBOX
--------------------------------------------------

Use the existing transactional outbox where D8 requires asynchronous downstream processing.

Do not create a new event infrastructure.

Follow the established:

OutboxMessages
→ OutboxProcessorJob
→ provider scope factory
→ handler

pattern.

If the D8 specification does not require an asynchronous event, do not introduce one merely for architectural symmetry.

--------------------------------------------------
## 20. REPORTING
--------------------------------------------------

Do not create a general D8 reporting subsystem.

Implement leaderboard/results views only where the D8 specification defines them.

Do not duplicate D6 reporting infrastructure.

If tournament results are derived from score records, those score records remain the authoritative source.

--------------------------------------------------
## 21. TEST DATA / SEEDING
--------------------------------------------------

Use the existing development seed mechanism only where helpful and explicitly safe.

Do not create fake tournament results that could be confused with real data.

Any D8 seed data must be:

- development-only
- behind the existing development-seed flag
- clearly identifiable
- safe to rerun

--------------------------------------------------
## 22. STOP CONDITIONS
--------------------------------------------------

Stop for a genuine unresolved D8 business/domain/architecture decision.

Examples:

- undefined scoring formula
- undefined tournament state
- contradictory entry rule
- undefined eligibility rule
- conflicting handicap rule
- undefined score correction policy

Do NOT stop for ordinary technical implementation decisions that can be derived from existing ClubStack architecture.

Do NOT invent a business rule to make a test pass.

--------------------------------------------------
## 23. REGRESSION
--------------------------------------------------

D7 and all earlier phases must remain green.

Run:

dotnet build ClubStack.slnx

dotnet test ClubStack.slnx

npx tsc --noEmit

npm run build

Expected:

0 warnings
0 errors
0 failed tests
0 skipped tests, unless an explicitly external dependency requires one

Report exact numbers.

Do not count soft-skipped tests as meaningful execution.

--------------------------------------------------
## 24. MIGRATION REGRESSION
--------------------------------------------------

Confirm:

MigrationStartupBehaviorTests

proves the full migration chain from the initial database through the D8 head.

Do not use EnsureCreated.

Do not manually patch the database.

--------------------------------------------------
## 25. SESSION REPORT
--------------------------------------------------

Create:

D8_Implementation_Session_Report.md

Use this exact 33-section structure:

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
16. Posting/Finance integration
17. Tournament lifecycle
18. Entry management
19. Scoring
20. Results/Leaderboard
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
32. D8 acceptance status
33. Exact next-session entry point

Clearly distinguish:

CURRENT SESSION

from:

PRIOR ACCEPTED WORK

Do not claim old D7 work as newly implemented.

--------------------------------------------------
## 26. ACCEPTANCE STANDARD
--------------------------------------------------

D8 is accepted only when:

- authoritative D8 requirements are implemented
- tournament state machine is proven
- entry rules are proven
- scoring rules are proven
- concurrency is proven where required
- idempotency is proven where required
- database integrity is proven
- authorization is proven
- audit is proven
- D7 integration is proven
- D6 integration is proven where required
- API works
- UI builds
- TypeScript passes
- real PostgreSQL integration tests pass
- full regression passes
- build has 0 warnings / 0 errors
- no D9+ functionality has leaked into D8
- no unresolved business rule has been silently invented

--------------------------------------------------
## 27. FINAL INSTRUCTION
--------------------------------------------------

D8 implementation is authorized.

Read the authoritative D8 requirements first.

Implement D8 using the accepted D7 machinery.

DO NOT DUPLICATE:

- bookings
- capacity
- players
- golf guests
- golf charges
- D6 finance
- payments
- idempotency

Extend them.

Do not ask for the database password.

Do not touch the FiscalStackZW DLL.

Do not reopen D6 or D7.

Do not start D9.

When D8 is complete:

1. run the full regression suite
2. run TypeScript
3. run the SPA build
4. verify the D8 UI against the running API where practical
5. write D8_Implementation_Session_Report.md
6. state exact acceptance status
7. stop
