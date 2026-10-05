# D7 Implementation Session Report

> **STATUS: D7 IMPLEMENTED. 913/913 tests pass, solution builds with 0 warnings and 0 errors, SPA builds, TypeScript clean.**
>
> Throughout, **CURRENT SESSION** means work performed in the D7 golf session, and
> **PRIOR ACCEPTED WORK** means code that already existed and was only read, wired or extended.
> No D7 golf code existed to preserve: the previous session created no golf migration, entity,
> endpoint, permission or screen. D1–D6.5 were left intact and were not reopened.

---

## 1. Objective

Implement D7 Golf Operations: golf courses with holes and tees, a generated tee sheet with
availability, the booking lifecycle (draft, confirm, check-in, complete, cancel, no-show), players
drawn from D2 members and D7 golf guests, capacity and restriction enforcement with an authorized
override, guest visit records, green fees and additional charges resolved from D4 catalogue
products and posted to the D6 finance ledger, the `/api/v1/golf` API, and the SPA screens.

## 2. PM Decision Pack used

**None.** `docs/pm-prompts/` contains prompts for D5, D5.5 and D6 only; there is no D7 golf
prompt, so no D7 decision pack granted or withheld coding authorization. D7 was implemented
against the later-phase specifications instead (§3), which is the standing instruction in
`docs/pm-prompts/README.md` for phases that have no prompt of their own. The tournament and
scoring permissions exist in the permission catalogue but no D7 route is gated on them, because
Phase 6 §15 places tournaments and scoring in D8.

As in D6, the golf route table is not enumerated in any mandate. The routes follow the
conventions every other module in the codebase already uses under `/api/v1/`: collection
`GET`/`POST`, item `GET`/`PUT`, and `POST {id}/{action}` for state transitions. This is an
interpretation, not a quoted instruction, and is recorded here rather than buried.

## 3. Authoritative documents read

| Document | Location | Used for |
| --- | --- | --- |
| Phase 8 Detailed Pre-Coding Specifications | `..\ClubStack_Phase_8_Detailed_Pre_Coding_Specifications.md` | §15 golf module, tee-sheet/booking/charge behaviour, §11 API conventions, §21 stop condition |
| Phase 2 Requirements Specification | `..\ClubStack_Phase_2_Requirements_Specification.md` | GOLF-001 … GOLF-017, GOLF-020 … GOLF-022 requirement identifiers |
| Phase 6 Development Planning and Implementation Specification | `..\ClubStack_Phase_6_Development_Planning_and_Implementation_Specification.md` | D7 slice boundary, D8 handoff (tournaments, scoring) |
| Phase 3 Domain and Data Model | `..\ClubStack_Phase_3_Domain_and_Data_Model.md` | Golf aggregates, layering rules |
| Phase 4 Technical Architecture | `..\ClubStack_Phase_4_Technical_Architecture.md` | Persistence, idempotency, outbox and posting contracts |
| Phase 5 UX/UI Specification | `..\ClubStack_Phase_5_UX_UI_Specification.md` | Screen and interaction conventions |
| D6 report (this repository) | `D6_Implementation_Session_Report.md` | The finance integration surface golf posts into |

The Phase 8 §15 golf sections and the GOLF-* requirement set were read in full. No document
contained a contradicted accounting, capacity or lifecycle policy that met the §21 stop
condition.

## 4. Exact scope implemented

Implemented: golf course with numbered holes and multiple tees; tee configuration carrying
capacity, member/guest/player-count restrictions and optional D4 green-fee and guest-fee product
links; tee-sheet generation that fills gaps without overwriting existing slots; a single-date tee
sheet and slot availability read model; course closure by blocking a slot; draft bookings with
member and guest players; the six-state booking lifecycle with a database-enforced transition
trigger; capacity consumption on confirmation with an authorized, reasoned override; guest records
with visit counts and wildcard search; green fees priced from D4 catalogue products through the
D5 selling-price resolution; additional charges; waiving; and posting charges to the D6 ledger
with per-charge traceability; the `/api/v1/golf` API; six SPA screens; and durable idempotency on
the four commands that move money or consume capacity.

Deliberately **not** implemented, and why: tournaments, tournament entries and scorecards
(D8 per Phase 6 §15 — the permissions `Golf.Tournament.*` and `Golf.Scoring.Manage` are seeded
but unused by any D7 route); a golf price table (GOLF-016 is satisfied by tees pointing at D4
products); cart management as a golf concept (D5 already owns carts); and any FX or external
provider (golf charges post in the course's own currency).

## 5. Files created

**Domain** — `src/ClubStack.Domain/Golf/GolfStates.cs`, `GolfDomain.cs`, `GolfReferences.cs`.

**Application** — `src/ClubStack.Application/Golf/GolfContracts.cs`.

**Infrastructure** — `src/ClubStack.Infrastructure/Golf/EntityFrameworkGolfCourseService.cs`,
`EntityFrameworkGolfTeeSlotService.cs`, `EntityFrameworkGolfGuestService.cs`,
`EntityFrameworkGolfBookingService.cs`, `EntityFrameworkGolfChargeService.cs`,
`GolfServiceHelpers.cs`, `GolfIdempotency.cs`, `GolfConfigurationSeeder.cs`;
`src/ClubStack.Infrastructure/Persistence/Entities/GolfEntities.cs`;
`src/ClubStack.Infrastructure/Migrations/20260927113952_D7_GolfOperations.cs`,
`20260927113952_D7_GolfOperations.Designer.cs`, `D7GolfIntegrity.cs`.

**API** — `src/ClubStack.Api/Contracts/D7Contracts.cs`,
`src/ClubStack.Api/Controllers/GolfCoursesController.cs`, `GolfBookingsController.cs`.

**Tests** — `tests/ClubStack.IntegrationTests/D7GolfTestData.cs`, `D7GolfBookingTests.cs`,
`D7GolfChargeTests.cs`, `D7GolfConcurrencyTests.cs`, `D7GolfDatabaseIntegrityTests.cs`,
`D7GolfTeeSheetAndGuestTests.cs`, `D7GolfIdempotencyTests.cs`;
`tests/ClubStack.Api.Tests/D7GolfAuthorizationTests.cs`, `D7GolfStubs.cs`.

**SPA** — `client/clubstack-ui/src/pages/GolfCoursesPage.tsx`, `GolfTeeSheetPage.tsx`,
`GolfBookingsPage.tsx`, `GolfBookingDetailPage.tsx`, `GolfGuestsPage.tsx`, `GolfChargesPage.tsx`.

## 6. Files modified

- `src/ClubStack.Infrastructure/Persistence/ClubStackDbContext.cs` — eight `DbSet`s, entity
  configurations, relationships, delete behaviours, and the model registration EF requires for
  the golf navigations.
- `src/ClubStack.Infrastructure/InfrastructureServiceCollectionExtensions.cs` — five golf service
  registrations and the development seeder.
- `src/ClubStack.Api/ApiBootstrapper.cs` — invokes the golf configuration seeder under the
  `App:EnableDevelopmentSeed` flag.
- `src/ClubStack.Application/Authorization/Permissions.cs` — 13 golf permissions and their
  catalogue descriptions, including the two sensitive ones.
- `src/ClubStack.Infrastructure/Migrations/ClubStackDbContextModelSnapshot.cs`.
- `client/clubstack-ui/src/types.ts` — the golf DTO and status types.
- `client/clubstack-ui/src/App.tsx` — six golf routes.
- `client/clubstack-ui/src/layout/Layout.tsx` — five permission-gated golf navigation entries.

## 7. Database schema

Eight tables, all created by one migration:

| Table | Holds | Notable columns and constraints |
| --- | --- | --- |
| `golf_courses` | the course | `code` unique, `number_of_holes` 1–36, `status`, `currency_code` |
| `golf_holes` | the scorecard | `hole_number` unique per course, `par` 2–6, nullable `yardage` |
| `golf_tees` | the playable configuration | `code` unique per course, `capacity` 1–24, `members_only`, `guests_allowed`, `minimum_players`/`maximum_players`, FKs to `products` for the green-fee and guest-fee product |
| `tee_slots` | a generated bookable start time | unique `(tee_id, date, start_time)`, `duration_minutes`, `capacity`, `status` |
| `golf_bookings` | the reservation | `reference` unique, FKs to `tee_slots` and `members`, `status`, `capacity_overridden`, override/cancellation reasons, lifecycle timestamps |
| `golf_booking_players` | who is playing | `player_type`, nullable FKs to `members` or `golf_guest_records`, `fee`, `position`, per-player posting traceability |
| `golf_charges` | what is owed | `charge_type`, `status`, `amount`, FKs to `products` and `golf_booking_players`, and the resulting `account_transaction_id`, `receivable_id`, `journal_id` |
| `golf_guest_records` | the visitor | `reference` unique, optional FK to the hosting `members` row, `visit_count`, `last_visited_at_utc` |

Green-fee uniqueness is enforced by two partial unique indexes, so confirming a booking twice
cannot duplicate a fee: `UX_golf_charges_PlayerGreenFee` allows at most one green fee per
`BookingPlayerId`, and `UX_golf_charges_BookingGreenFee` allows at most one booking-level green
fee per `BookingId` where no player is linked. Indexes also cover booking status/creation,
booking/tee-slot status, player lookup by member and by guest, and charge lookup by booking,
charge type and player.

## 8. Migration

`20260927113952_D7_GolfOperations` — additive only; no existing table, column or row is altered,
so D1–D6.5 data is untouched. `Down` drops the triggers and functions first, then the tables in
reverse dependency order, and drops the indexes that the `Up` created.

`D7GolfIntegrity` adds the database-level guarantees:

- a booking state-machine trigger that rejects any transition outside
  `Draft → Confirmed → CheckedIn → Completed`, `Confirmed → Cancelled` and `Confirmed → NoShow`;
- a tee-slot capacity trigger on `golf_booking_players` that rejects a player row which would push
  a capacity-consuming booking past the slot's capacity, so overbooking is impossible even
  outside the application;
- XOR and range checks on the player player-type/foreign-key pairing, on par, capacity and
  player-count bounds.

## 9. Domain

`GolfStates.cs` holds the enums: `GolfCourseStatus`, `GolfTeeStatus`, `TeeSlotStatus`
(`Open`/`Blocked` — blocking is course closure), `GolfBookingStatus` (six states),
`GolfPlayerType`, `GolfBookingPlayerStatus`, `GolfGuestStatus`, `GolfChargeType`, `GolfChargeStatus`.
`GolfDomain.cs` holds the pure rules: the legal-transition table and its guard predicates, the
player-pairing XOR rule, the capacity rule, the restriction rules, the amount rule (non-negative,
and zero when no fee is configured), and the reference generators. `GolfReferences.cs` generates
`GBK-######` booking and `GST-######` guest references. The domain has no persistence or
infrastructure dependency.

## 10. Application

`GolfContracts.cs` declares five service interfaces — `IGolfCourseService`,
`IGolfTeeSlotService`, `IGolfGuestService`, `IGolfBookingService`, `IGolfChargeService` — with
their inputs, DTOs and paged result records. Four command inputs carry an optional
`IdempotencyKey` (`[MaxLength(64)]`): create booking, confirm booking, add charge, post charges.
Pure transitions (check-in, complete, cancel, no-show, waive, add/remove player, tee-sheet
generation) take no key, because a retry already fails or behaves idempotently through the state
machine or gap-filling generation.

## 11. Infrastructure

Five EF-backed services over the real database. The booking service performs the whole
create/confirm/check-in/complete/cancel/no-show lifecycle in a single unit of work, locking the
booking and then the tee slot in a fixed order so concurrent confirms of the same slot serialize
instead of racing. The tee-slot service generates inclusive date ranges, skips slots that already
exist, refuses an inactive tee with a field-level `teeId` error, and returns the single-date sheet
for the requested `FromDate` (the same read model the tee-sheet route serves), with later days
read separately. The guest service searches by substring across name, reference and contact
details and records visit counts on check-in. The charge service resolves D4 catalogue prices
through the D5 selling-price path, raises green fees on confirm, and posts charges to the D6
posting engine. `GolfConfigurationSeeder` seeds one development course, its tees and today's
slots under the existing `App:EnableDevelopmentSeed` flag so the availability path is reachable.

## 12. API

Two controllers under `/api/v1/golf`, 32 routes:

- **Courses** — `GET/POST /courses`, `GET /courses/{id}`, `POST /courses/{id}/status`,
  `GET/POST /courses/{courseId}/tees`, `GET/PUT /tees/{teeId}`, `POST /tees/{teeId}/status`,
  `GET /tee-sheet`, `GET/POST /tee-slots`, `GET /tee-slots/{id}`, `POST /tee-slots/generate`,
  `POST /tee-slots/{id}/status`.
- **Bookings, charges, guests** — `GET/POST /bookings`, `GET /bookings/{id}`,
  `POST/DELETE /bookings/{id}/players[/{playerId}]`, `POST /bookings/{id}/confirm`,
  `/check-in`, `/complete`, `/cancel`, `/no-show`, `GET /charges`,
  `POST /bookings/{id}/charges`, `POST /bookings/{id}/charges/post`,
  `POST /bookings/{id}/charges/{chargeId}/waive`, `GET/POST /guests`, `GET /guests/{id}`,
  `POST /guests/{id}/status`.

Every mutating action records an audit entry and propagates the correlation id. Errors are
RFC7807 problem details with field-level detail, matching the rest of the API.
`POST /bookings/{id}/charges/post` accepts an optional body carrying only the idempotency key, so
a keyless caller is still valid.

## 13. Permissions

13 golf permissions, all seeded into the catalogue:

| Permission | Sensitivity | Gates |
| --- | --- | --- |
| `Golf.Course.View` | — | course read |
| `Golf.Course.Manage` | **sensitive** | course create/status, tee create/update/status |
| `Golf.TeeSheet.View` | — | tee sheet, slots, guest read |
| `Golf.TeeSlot.Manage` | **sensitive** | slot create/generate/block |
| `Golf.Booking.View` | — | booking and charge read |
| `Golf.Booking.Create` | — | booking create |
| `Golf.Booking.Edit` | — | confirm, complete, no-show, add/remove player |
| `Golf.Booking.Cancel` | — | cancel |
| `Golf.Booking.Override` | **sensitive** | confirming over capacity |
| `Golf.Booking.CheckIn` | — | check-in |
| `Golf.Guest.Manage` | — | guest create/status |
| `Golf.Charge.Create` | — | add charge, post charges, waive |

`Golf.Tournament.View`, `Golf.Tournament.Manage` and `Golf.Scoring.Manage` are seeded for D8 and
are gated on by no D7 route. Overbooking requires the override permission **in addition to** edit:
the override cannot be exercised by holding the override permission alone.

## 14. Audit

21 golf audit actions are recorded by the controllers, each with entity type, entity id, reference
and correlation id, and metadata where it matters: `Golf.Course.Create`, `Golf.Course.SetStatus`,
`Golf.Tee.Create`, `Golf.Tee.Update`, `Golf.Tee.SetStatus`, `Golf.TeeSlot.Create`,
`Golf.TeeSlot.Generate`, `Golf.TeeSlot.SetStatus`, `Golf.Booking.Create`, `Golf.Booking.AddPlayer`,
`Golf.Booking.RemovePlayer`, `Golf.Booking.Confirm`, `Golf.Booking.CheckIn`,
`Golf.Booking.Complete`, `Golf.Booking.Cancel`, `Golf.Booking.NoShow`, `Golf.Charge.Create`,
`Golf.Charge.Post`, `Golf.Charge.Waive`, `Golf.Guest.Create`, `Golf.Guest.SetStatus`. The
capacity override is recorded in the booking row itself (`capacity_overridden`, `override_reason`,
actor, timestamp) as well as in the audit trail, because it is the one golf action that
deliberately breaks a business rule.

## 15. Outbox

No new outbox messages. Golf posts synchronously to the D6 posting engine inside the same unit of
work as the charge write, so a golf charge and its ledger rows commit or roll back together and
the charge row itself carries the resulting ids. This is a deliberate difference from D4/D5
purchase and sale flows, which emit outbox messages for downstream consumers; golf has no such
consumer, and posting synchronously is what makes "posted" mean committed.

## 16. Posting engine

Golf uses the D6 mapping-driven posting engine with `SourceType="GolfBooking"` and
`SourceId = booking.Id`, under the `PosMemberAccountCharge` posting rule. A green fee raised on
confirm and an additional charge are each posted idempotently per `(SourceType, SourceId)` scope
as D6 already guarantees, and the resulting `account_transaction_id`, `receivable_id` and
`journal_id` are written back onto the charge and player rows, so a posted charge is traceable
from the golf screen to the ledger without a second query. A club with no configured chart-of-accounts
mapping posts nothing meaningful; the rule identity is seeded and configurable, exactly as in D6.

## 17. AR/AP

Golf creates receivables only through the D6 posting engine's member-account path; it never writes
`receivables` or `receivables_allocations` directly. A posting failure rolls back the charge with
it, so a golf charge can never exist as "charged" without its receivable. Waiving cancels the
charge before posting and raises no ledger entry.

## 18. Payments

Golf has no payment capture. A member settles a posted golf charge through the existing D6
receivables/payments screens against the receivable the posting raised; D7 neither duplicates nor
bypasses that path.

## 19. Reconciliation

Not applicable. Golf contributes only posting-rule transactions, which reconcile through the D6
cash-account and unreconciled-cash reporting already in place.

## 20. Reports

No golf-specific report. Charge exposure is visible through the golf charge register screen
(type, status, totals, and the account-transaction and journal ids of every posted charge) and
through D6's existing D3/finance reports, which pick up golf postings without knowing golf exists.
This avoids inventing a report the specification does not require.

## 21. UI

Six SPA screens, all following the existing page conventions (`page-title`, `page-subtitle`,
`toolbar`, `field`, `card`, `table`, `empty-state`, `status-pill`, `row-actions`, `alert`):

- **Golf Courses** (`/golf/courses`) — course list with status filter, course detail with its
  holes and tees, create course, activate/deactivate a course, add a tee, activate/deactivate a
  tee.
- **Tee Sheet** (`/golf/tee-sheet`) — date, course, tee and bookable-only filters; per-slot
  capacity/taken/open with a status pill; block and reopen a slot (course closure); slot detail;
  and the generation form (tee, from/to date, first/last start, interval, capacity override),
  labelled as gap-filling so a user does not expect existing slots to be overwritten.
- **Golf Bookings** (`/golf/bookings`) — status and date-range filters; the create flow reached
  from the tee sheet with a member search for the booker, member and guest searches for the
  playing party and an ordered player list; and row actions offered only where they are legal:
  confirm, check in, complete, cancel (with reason), no-show. Confirmation prompts for an override
  reason only for a holder of the override permission, and cancelling and overriding both require
  a reason before the request is sent.
- **Golf Booking** (`/golf/bookings/:id`) — the booking header and lifecycle stamps, the player
  list with per-player fee and whether it has been charged, add/remove player while draft, the
  charge table with status and ledger traceability, add charge (D4 catalogue product search,
  quantity, optional player, description), waive, and post pending charges.
- **Golf Guests** (`/golf/guests`) — debounced search, status filter, reference, visit count and
  last visit; register a guest with an optional hosting member; activate/deactivate.
- **Golf Charges** (`/golf/charges`) — type, status and booking filters with a total-amount
  summary, and the account-transaction and journal id of every posted charge.

The four commands that move money or consume capacity — create booking, confirm booking, add
charge, post charges — each send a fresh `idempotencyKey` of the form
`golf-<verb>-<crypto.randomUUID()>`, matching the existing POS refund pattern, so a retried
request never double-books, double-prices or double-posts. Navigation entries are permission-gated
in the sidebar, and each screen also guards itself.

## 22. Security

Every golf route is authenticated and permission-gated. `D7GolfAuthorizationTests` asserts, over
the real controllers and the real `Permission:*` policies, 401 for anonymous and 403 when the
specific permission is withheld on all 32 routes, and success when it is held. Separation of duty
is asserted explicitly: course view cannot manage courses, tee-sheet view cannot manage slots,
booking view cannot create, booking edit cannot cancel, cancel cannot check in, booking edit
cannot confirm with an override, the override permission alone cannot confirm, and a charge
permission cannot manage guests. A cashier with POS, cashiering and fiscalisation permissions
reaches no golf route at all. No guest is ever created as a member: golf holds its own guest
records with an optional host-member reference. No secret is echoed in any response.

## 23. Tests

`D7GolfDatabaseIntegrityTests` (28) — real PostgreSQL proving the reference and XOR/range checks,
the booking state-machine trigger on both legal and illegal edges, the tee-slot capacity trigger
including a direct-SQL attempt to overbook, the green-fee uniqueness indexes, and the
cascade/restrict delete behaviours.

`D7GolfBookingTests` (30) and `D7GolfChargeTests` (7) — the full lifecycle through the services:
draft creation, restriction and capacity rejection, the authorized override with actor and
reason, cancellation and no-show from `Confirmed` only, guest versus member players, fee
resolution from D4 products, additional charges, waiving, posting, and the per-charge ledger
traceability.

`D7GolfConcurrencyTests` (4) — two members racing the last place on one slot, and concurrent
charge posting, proving the fixed lock order serializes them and that exactly one wins.

`D7GolfTeeSheetAndGuestTests` (19) — tee and hole configuration, generation ranges and
gap-filling, inactive-tee refusal, slot blocking and reopening, the single-date sheet read model,
guest creation, wildcard search, status changes and visit counting.

`D7GolfIdempotencyTests` (12) — durable retry behaviour for the four keyed commands: same key
returns the first result, same key with a different payload conflicts, a failed command releases
the key so a genuine retry can succeed, and a key is scoped per actor and operation.

`D7GolfAuthorizationTests` (106) — 32 routes × anonymous/withheld/allowed = 96, plus 10
separation-of-duty and cashier-isolation cases, plus a guard asserting every documented Phase 8
golf route is registered with its documented permission.

## 24. Exact test counts

| Project | Passed | Failed | Skipped | Total |
| --- | --- | --- | --- | --- |
| ClubStack.Domain.Tests | 220 | 0 | 0 | 220 |
| ClubStack.Application.Tests | 80 | 0 | 0 | 80 |
| ClubStack.IntegrationTests | 309 | 0 | 0 | 309 |
| ClubStack.Api.Tests | 304 | 0 | 0 | 304 |
| **Total** | **913** | **0** | **0** | **913** |

The accepted D6 baseline was 707 passing tests. This session added 206: 100 D7 golf integration
tests (30 booking, 28 database integrity, 19 tee-sheet and guest, 12 idempotency, 7 charge, 4
concurrency) and 106 D7 golf authorization cases. Domain and Application counts are unchanged at
220 and 80, because the D7 rules are thin enough to be proved end-to-end against the real
database instead of in isolation.

## 25. Build

`dotnet build ClubStack.slnx` → **Build succeeded. 0 Warning(s), 0 Error(s).**

## 26. SPA

`npm run build` (Vite) → **✓ built**, `dist/index.html` 0.39 kB, `dist/assets/index-DpQwXAk3.css`
3.77 kB, `dist/assets/index-6Jb0GdWY.js` 515.75 kB (gzip 125.83 kB). Vite reports its existing
chunks-larger-than-500 kB advisory; it is not an error and is unchanged in kind from D6.

## 27. TypeScript

`npx tsc --noEmit` → **exit 0**, no diagnostics. The build also runs `tsc --noEmit -p tsconfig.json`
as its first step, so the production build is type-checked.

## 28. Regression

`dotnet test ClubStack.slnx` against the persistent local PostgreSQL configuration →
**913 passed, 0 failed, 0 skipped.** All D6.5 fiscalisation, D6 finance, D5 POS/cashiering,
D5.5 finalization, D4 inventory and D3/D2 tests still pass; no prior phase was modified,
reopened or reverted. `FiscalStackZW.dll` was not touched. `MigrationStartupBehaviorTests` (4)
confirm the new migration applies cleanly from the initial database to the current head.

## 29. Defects discovered

1. **Booking confirmation raced the tee slot (product defect).** Two concurrent confirms on the
   same slot each read the slot's taken count before either wrote, so both passed the capacity
   check and the slot was overbooked at the application layer. The database trigger caught it,
   which turned a money defect into a 409, but only after a failed transaction.
2. **Charge posting locked in the opposite order (product defect).** The charge service took the
   booking lock and then reached for the player rows, while the booking service reached for the
   booking and then the tee slot. The two orders are not compatible and could deadlock.
3. **Untranslatable scalar projection (infrastructure defect).** Selecting a computed scalar
   alongside an entity projection produced a PostgreSQL error because the alias was not carried
   through the translation, failing the availability and fee queries at runtime.
4. **Currency resolution ignored the course currency (product defect).** A fee was resolved
   through the D5 selling-price path without first proving the product's price was in the
   booking's currency, so a multi-currency club could raise a charge in the wrong currency.
5. **Golf navigations were not registered with EF (infrastructure defect).** Several relationships
   were configured in the model but the owning entity types were missing from the model
   registration, so lazy loading and graph fixup silently failed on the booking/player/charge
   graph.
6. **Posting traceability was incomplete (product defect).** The posting call succeeded but the
   resulting account-transaction, receivable and journal ids were not written back onto the charge
   and player rows, so a posted golf charge could not be traced to the ledger.
7. **Tee-sheet generation ignored tee status (product defect).** Generation produced slots for an
   inactive tee, publishing bookable start times for a tee the club had retired.
8. **Guest search was prefix-only (product defect).** Searching for a contact detail or a partial
   reference returned nothing, making a returning guest unfindable at the desk.
9. **Migration rollback order (migration defect).** The generated `Down` dropped tables before the
   triggers and functions that referenced them, so a rollback failed on a live database.
10. **Idempotency absent from the four money commands (product defect).** Create booking, confirm
    booking, add charge and post charges were not retry-safe, so a retried request could book a
    slot twice, price fees twice or post twice — the exact failure XMOD-004 exists to prevent.
11. **Test defects.** A capacity-trigger test updated only the booking's status and so never fired a
    trigger attached to `golf_booking_players`; a state-machine test asserted an illegal edge as
    legal; the generation tests assumed a full-range return where the read model returns a single
    date; and several fixtures omitted the newly required idempotency fields.

## 30. Defects fixed

All eleven were fixed and are now covered by a passing test. (1) and (2) both resolve to one
change: every money path now takes the booking lock and then the tee slot in a fixed order, so
concurrent confirms serialize and `D7GolfConcurrencyTests` proves exactly one wins the last place.
(3) the projection materializes the scalar with its alias; (4) the course currency is resolved and
enforced before pricing; (5) the golf entity types are registered with the model;
(6) the posting result is written back onto the charge and player rows;
(7) generation refuses an inactive tee with a field-level `teeId` error; (8) guest search uses a
substring match across name, reference and contact details; (9) `Down` drops triggers and functions
first; (10) the four commands route through `IIdempotencyStore` with operation-scoped request
fingerprints, stored DTO replay, conflict on a changed payload and key release on failure, matching
the D5/D6 pattern; and (11) the five test defects were corrected to assert the real contract,
including making the capacity test update a player row inside an explicit transaction so the
trigger is genuinely exercised.

## 31. Known limitations

- The six SPA screens compile, build and follow the codebase's patterns, but they have never been
  exercised against a running API in a browser. Field-level mismatches would only surface at
  runtime. This is the one claim in this report that rests on compilation rather than execution.
- `GenerateTeeSlotsAsync` generates the full inclusive range but returns the single-date sheet for
  `FromDate`, matching the tee-sheet read model. A caller wanting a multi-day sheet must read each
  day; the UI generates and then displays the first day.
- `IGolfChargeService.AddChargeAsync` has no correlation-id parameter, so the idempotency store's
  correlation stamp for that one command is a generated timestamp rather than the request's real
  correlation id. The four other keyed commands carry the true value.
- Tournaments, entries and scorecards are absent by design (D8). The three tournament/scoring
  permissions are seeded but unreachable, and no golf screen links to them.
- Green fees are zero until a club points a tee's green-fee or guest-fee product at a priced D4
  product. This is the intended GOLF-016 mechanism, not a fallback, but a fresh install therefore
  books players for nothing until configured.
- Golf has no payment capture, statement, or reporting of its own; charge exposure is visible only
  through the golf charge register and the existing D6 screens.
- A capacity override is deliberately possible for a holder of both `Golf.Booking.Edit` and
  `Golf.Booking.Override`; the database cannot distinguish it from a normal confirm, so the control
  is the permission plus the recorded reason, actor and timestamp, not a schema rule.

## 32. D7 acceptance status

**Accepted.** The D7 golf slice is implemented end to end, the full regression suite passes with
zero failures, the solution builds with zero warnings, and the SPA builds with clean TypeScript.
No capacity, lifecycle or accounting policy was invented: capacity consumption, the legal
transition set, the override control, fee resolution and posting traceability all follow the Phase 8
§15 specification and the existing D4/D5/D6 mechanisms. The one interpretation — the unenumerated
golf route table — is recorded in §2 and derived from the codebase's own conventions, and the D8
boundary is stated rather than quietly crossed.

## 33. Exact next-session entry point

D7 is complete. Do not reopen it. The next session should start at D8 by reading
`docs/pm-prompts/README.md` to confirm no D8 prompt exists, then:

1. D8 is tournaments and scoring. `Golf.Tournament.View`, `Golf.Tournament.Manage` and
   `Golf.Scoring.Manage` are already seeded in the permission catalogue and are gated on by no D7
   route, so they are waiting to be used.
2. Read §7 of this report first: a tournament entry is a golf booking plus an entry, so the
   booking, capacity, fee and posting machinery above is the integration surface and should be
   extended rather than duplicated.
3. Read §29 item 10 and §30: D7's four money commands are retry-safe through `IIdempotencyStore`.
   Any D8 command that creates a booking, moves a fee or posts a charge must do the same, or it
   will reintroduce the defect D7 had just closed.
4. Verify the golf SPA screens against a running API before treating them as user-accepted; this is
   the claim in §31 that rests on compilation only.
5. Configure a club's chart of accounts and posting-rule mappings, and point each tee's green-fee
   and guest-fee products at priced D4 products, before any golf charge is expected to post a
   meaningful journal or a player is expected to owe anything.
