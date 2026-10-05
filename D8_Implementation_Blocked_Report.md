# D8 Implementation Blocked Report

> **STATUS: D8 NOT IMPLEMENTED. STOPPED BEFORE WRITING ANY D8 CODE.**
>
> The D8 mandate (`docs/pm-prompts/D8-tournaments-scoring-implementation.md`, saved verbatim
> per the archive convention) is in force and authorizes implementation. Implementation was not
> started because the authoritative ClubStack specification set defines **no tournament lifecycle
> and no scoring model**, and the mandate, Phase 8 §32 and Phase 9 §47 all independently forbid
> the coding agent from inventing either.
>
> This is the same class of stop as the first D6 attempt. That session correctly reported
> "STOPPED — D6 NOT IMPLEMENTED" because the specification package did not define the finance
> decisions; the PM Decision Pack `docs/pm-prompts/D6-finance-integration-implementation.md`
> (B1–B19) subsequently supplied them and D6 was implemented. **D8 needs the equivalent
> decision pack.** The exact missing decisions, and the exact implementation each one blocks,
> are enumerated in §3 below.

Nothing in D1–D7 was touched. No D8 file, migration, entity, endpoint, permission or screen was
created. The working tree is unchanged apart from two documentation artefacts: this report and the
saved D8 mandate.

---

## 1. What I did

1. Read the D8 mandate in full.
2. Read the authoritative specification set in the order the mandate lists, concentrating on
   every tournament and scoring statement: Phase 0 §9.7/§19, Phase 1 §23, Phase 2 §16
   (TOUR-001…TOUR-010), Phase 3 §45/§46, Phase 4 §6.9, Phase 5 §51, Phase 6 §15, Phase 7 P7-B,
   Phase 8 §2/§4.2/§5/§7/§8.9/§9/§10/§12/§13/§29/§31/§32, Phase 9 §3/§12/§20/§47/§49, plus
   `docs/pm-prompts/README.md`, the D6 report and the D7 report.
3. Ran exhaustive negative searches for any scoring mathematics and any tournament state
   enumeration across every specification document (§2 and §3 below).
4. Confirmed the D7 baseline is intact and needs no action: 913/913, build clean.
5. Saved the mandate verbatim, updated the prompt archive, and wrote this report.

## 2. What the specification DOES authorize

This is a capability list, not an implementation contract. Every item below is stated as a
capability somewhere in the set, and none of them is stated as a rule with states, fields,
transitions or calculations.

| Authorized capability | Source | Status |
| --- | --- | --- |
| Create a tournament | TOUR-001 "Authorized users MUST be able to create a tournament" | Requires tournament `Status` values — undefined |
| Tournament registration | TOUR-002 "The system MUST support tournament registration" | Requires a registration-eligible state — undefined |
| Optional configured entry fee | TOUR-003 | Requires knowing what "configured" means — contradicted (§3.6) |
| Registered player list | TOUR-004 | Implementable only once a registration exists |
| Configurable tournament categories | TOUR-005 | Category semantics undefined (§3.4) |
| Player flights | TOUR-006 (**SHOULD**) | Flight semantics undefined (§3.5) |
| Tournament tee allocation | TOUR-007 (**SHOULD**) | Relationship to D7 capacity contradictory (§3.7) |
| Scoring | TOUR-008 | **Deferred to a document that does not define it** (§3.2) |
| Retain tournament results | TOUR-009 "MUST retain tournament results once recorded" | Retention only; no result content or computation |
| Tournament charges traceable | TOUR-010 | Mechanism is derivable; the fee itself is not (§3.6) |
| "Tournament records must connect to members, guests, payments, and accounts" | Phase 6 §15 | Integration target agreed; the rules around it are not |

Supporting D8 material that **is** explicit:

- **Phase 8 §8.9** names the six D8 tables: `golf_tournaments`, `tournament_players`,
  `tournament_categories`, `tournament_flights`, `tournament_scores`, `tournament_results`.
- **Phase 3 §46** gives the only Tournament field list: `TournamentId`, `Name`, `Date`,
  `CourseId`, `RegistrationOpen`, `RegistrationClose`, `EntryFee`, `Status`, `Configuration`,
  and states "Tournament participants are separate records linked to people/guests".
- **Phase 8 §4.2** confirms exactly three D8 permissions, all already seeded in D7:
  `Golf.Tournament.View`, `Golf.Tournament.Manage`, `Golf.Scoring.Manage`.
- **Phase 5 §51** lists the required screens and states registration connects to member, guest,
  account/payment and tournament.

Note what is absent from that "explicit" list: no field list for `tournament_scores`,
`tournament_results`, `tournament_flights` or `tournament_categories`; no state machine; no
scoring formula; no results or leaderboard calculation; no seed data (Phase 8 §29 Golf seeds
only a course, tees and tee slots).

## 3. The missing decisions, and exactly what each one blocks

Each entry cites the specification that creates the obligation and the specification that fails
to discharge it. This is a gap report, not a contradiction between two rules — except §3.6,
which is a genuine contradiction.

### 3.1 No tournament lifecycle — BLOCKS the entire phase

- **Phase 3 §46** defines `Status` on Tournament but gives **no values**.
- **Phase 7 P7-B** explicitly promised it would: "The detailed specification will define state
  machines for: member, membership, membership application, credential, member account
  transaction, POS sale, sale payment, cashier session, **golf booking, tournament**, stock
  movement, purchase requisition, purchase order, goods receipt, supplier invoice, asset,
  maintenance work order, facility reservation, event, guest visit, financial journal, financial
  period."
- **Phase 8 §7 "State Machine Specification"** discharges that promise for 16 modules —
  §7.1 member, §7.2 membership application, §7.3 membership, §7.4 credential, §7.5 POS sale,
  §7.6 cashier session, §7.7 golf booking, §7.8 purchase requisition, §7.9 purchase order,
  §7.10 goods receipt, §7.11 supplier invoice, §7.12 asset, §7.13 maintenance work order,
  §7.14 facility reservation, §7.15 event, §7.16 financial period — and **contains no tournament
  state machine.** §7.7 is golf booking; §7.8 jumps to procurement.
- **Phase 9 §20** makes the omission binding: "The state machines in Phase 8 are authoritative.
  No coding agent may invent additional business states without an approved change."
- The mandate agrees: "Implement exactly the defined tournament state machine. Do not add states.
  Do not remove states."

**Blocks:** the `golf_tournaments.status` column type and check constraint; the domain state enum;
every transition and its guard; whether `RegistrationOpen`/`RegistrationClose` are enforced
against a status or are independent dates; the database transition trigger; audit of transitions;
the lifecycle controls on the UI; the initial status a tournament is created in; the legal state
for a tournament that may accept a scoring correction; and the status filter on the tournament
list. **There is no D7-style precedent to copy, because D7's `GolfBookingStatus` came from Phase 8
§7.7.** Every D8 artifact terminates here, including the tournament list screen.

**Decision required:** the exact tournament states, the exact legal transitions between them, and
the initial state on creation.

### 3.2 No scoring model — BLOCKS scoring, results, leaderboard and the scorecard

- **Phase 2 TOUR-008** is the whole of the scoring requirement, and it is a deferral:
  "The detailed scoring capability will be implemented according to the approved golf
  requirements."
- **Phase 0 §19** repeats the same deferral: "The exact scoring capabilities will be implemented
  according to the approved golf requirements."
- **The "approved golf requirements" are GOLF-001…GOLF-017 and GOLF-020…GOLF-022** (Phase 2
  §15). Read in full, they define courses, availability, tee times, capacity, the tee sheet,
  bookings, players, guests, visitors, availability validation, overbooking, restrictions,
  cancellation, no-show, check-in, fees, charges, states, state validation and booking history.
  **Not one of them defines a score, a hole result, a round, a card, a total, a ranking, a
  handicap or a scoring mathematics.** The deferral is circular: it points at a document that
  does not contain the answer.
- **Phase 0 §1477** independently removes it from this release: the approved-but-deferred list
  of "future areas [that] are not allowed to destabilize the first release" begins with
  "**Advanced tournament scoring**".
- **Phase 1 §23** agrees: "Future releases may add deeper competition and scoring capabilities
  depending on the club's requirements."
- Exhaustive search of all eleven Phase documents, the D0 Implementation Package, the D2 Market
  Reconciliation and the Market Research document for `handicap`, `stableford`, `stroke play`,
  `net score`, `to par`, `relative to par`, `gross score`, `handicap index`, `allowance`,
  `points for`, `leaderboard order` and `scoring formula` returns **zero** scoring
  occurrences. The single hit is an unrelated D1/D2 event-infrastructure sentence.
- The mandate agrees: "Do not invent handicap calculations, stableford calculations,
  stroke-play calculations, or other scoring mathematics unless explicitly specified."

**Blocks:** the meaning and columns of `tournament_scores`; per-hole versus per-round storage;
valid score range; whether a score is gross or net; score immutability and the correction policy
(the mandate's own stop-condition list names "undefined score correction policy"); who may submit
a score and who may amend it; the meaning and computation of `tournament_results`; the entire
leaderboard, including ordering and tie-breaks; and every acceptance criterion in the mandate
that says "scoring rules are proven", "results/leaderboard", "duplicate score protection" and
"corrections/edits according to specification".

**Decision required:** the scoring format (stroke play, stableford, or both, and whether
handicapping applies), what one score row represents, valid ranges, the order and tie-break rules
for a leaderboard, and the correction/approval policy for an already-submitted score.

### 3.3 No round definition — BLOCKS the score schema and every score query

The mandate requires scores to "preserve the relevant tournament/round/player relationship".
No document defines a round, a round number, or whether D8 is single-round or multi-round.
Phase 3 §46 gives the tournament a single `Date` and nothing else; there is no `rounds` table in
Phase 8 §8.9.

**Blocks:** whether `tournament_scores` needs a round discriminator at all; the primary key and
uniqueness rule that prevents duplicate scores (the mandate requires "duplicate score
protection"); round-level and overall result computation; and the difference between a
"scorecard" and a "result". This decision changes the schema, so it cannot be deferred past
table design.

**Decision required:** is D8 single-round or multi-round, and if multi-round, what identifies a
round.

### 3.4 Category semantics undefined — BLOCKS TOUR-005

TOUR-005 requires "configurable tournament categories" and Phase 8 §8.9 provides
`tournament_categories`, but nothing states what a category is (age band, handicap division,
gender, net/gross, age+handicap), what fields it has, whether a player belongs to exactly one,
whether it affects eligibility, scoring or seeding, or whether it drives flight allocation.

**Blocks:** the `tournament_categories` schema; category CRUD; assigning a category at
registration; and any category-filtered result or leaderboard.

**Decision required:** the category dimensions, whether membership is single or multi-valued,
and whether a category has any effect on scoring, eligibility or allocation.

### 3.5 Flight semantics undefined — BLOCKS TOUR-006

TOUR-006 says the system **SHOULD** support player flights and `tournament_flights` exists, but
nothing defines a flight's fields, whether a player is in exactly one flight, how flights are
formed (manually, by category, by handicap — all of which need §3.2 and §3.4), or how a flight
maps to tee slots and start times.

**Blocks:** the `tournament_flights` schema, flight CRUD and assignment, and any automatic flight
formation.

**Decision required:** flight fields, the player-to-flight cardinality, and whether flight
formation is manual or derived.

### 3.6 Entry fee: a genuine contradiction between Phase 3 and the pricing authority

- **Phase 3 §46** gives Tournament a scalar field `EntryFee`.
- **Phase 9 §8 "Pricing and Tax Decision"** and the D7 implementation resolve every monetary
  amount from a configured D4 catalogue product, never from a scalar on the aggregate. Phase 8
  §30 states configuration values "are not hard-coded into the application", and §31 forbids the
  agent to "invent accounting behaviour".
- **TOUR-003** says only "A tournament MUST support an optional configured entry fee" — the word
  "configured" is satisfied by either reading.
- The mandate says to "use D4 product pricing where specified" and "If the D8 specification does
  not define a monetary behavior, do not add one."

**Blocks:** whether `golf_tournaments` carries `entry_fee_amount` (a hard-coded money amount on
the aggregate, which Phase 8 §30/§31 and Phase 9 §47 "new data ownership" appear to forbid) or
`entry_fee_product_id` (a pointer resolved through D4 and posted through D6, consistent with D7
and with TOUR-010 traceability). This is a data-ownership decision Phase 9 §47 reserves to the
PM, and it determines the charge type, the posting path and the D6 traceability test.

**Decision required:** is the tournament entry fee a configured D4 product, or a scalar amount,
and which charge type and posting rule apply.

### 3.7 Tee allocation: contradictory relationship to D7 capacity

- **TOUR-007** says the system **SHOULD** support tournament tee allocation.
- **Phase 3 §45** lists availability as `TeeSlot + Course Closure + Tournament Allocation + Golf
  Bookings + Capacity + Booking Restrictions`, which puts tournament allocation *alongside* golf
  bookings as a separate contributor to availability.
- **Phase 5 §48** shows a tournament appearing as a row on the tee sheet.
- The mandate requires entry to "use the D7 mechanisms rather than reimplementing them", i.e.
  allocation must consume D7 capacity through a D7 booking.

Whether a tournament tee allocation (a) creates a real `golf_booking` per flight and therefore
consumes slot capacity through the D7 engine, or (b) is a separate reservation that participates
in availability without a booking, is undefined. Option (b) would create the second capacity
engine the mandate forbids; option (a) determines whether a tournament can exceed a slot's
capacity and whether green fees are raised.

**Blocks:** whether `tournament_flights`/allocation writes `golf_bookings`; whether tournament
play consumes `tee_slots` capacity; whether the D7 `Golf.TeeSlotStatus.Blocked` closure and the
D7 capacity override apply to a tournament; and the concurrency tests for entry capacity.

**Decision required:** is tournament tee allocation a D7 booking (consuming capacity and raising
green fees), or an independent reservation, and how do the D7 capacity trigger and override
permission apply to it.

### 3.8 Entry eligibility and limits undefined — BLOCKS TOUR-002 rules

Phase 5 §51 says registration connects to "member, guest, account/payment, tournament", so both
members and guests may enter. Nothing states whether entry requires an active membership
status, whether a guest requires a host member, whether `RegistrationOpen`/`RegistrationClose`
are enforced, whether a player may enter more than one tournament or category, or whether a
maximum player count exists.

**Blocks:** the registration validation rules; the duplicate-entry rule the mandate requires
("duplicate entry prevention", "entry duplication" under concurrency); the registration window
enforcement; and the eligibility tests.

**Decision required:** the entry eligibility rules, the registration window semantics, and the
duplicate-entry key (tournament + player, presumably, but the specification does not say so).

### 3.9 Scoring authorization granularity undefined

Only three D8 permissions exist and all are already seeded: `Golf.Tournament.View`,
`Golf.Tournament.Manage`, `Golf.Scoring.Manage`. Phase 8 §5 lists the sensitive actions requiring
elevated authorization and audit, and Phase 9 §18/§19 govern approval and self-approval — but
neither distinguishes submitting a score from amending a score from finalizing a result. The
mandate forbids creating new permission names without specification support, and forbids
introducing a "finalize/publish" behaviour merely because a route wants to exist.

**Blocks:** whether `Golf.Scoring.Manage` covers submit, amend and finalize; whether a score can
be amended after the tournament reaches a terminal state; and the separation-of-duty tests the
mandate requires under "scoring authorization boundaries".

**Decision required:** whether one permission covers the whole scoring lifecycle, and what the
authorization rule is for amending an already-submitted score.

## 4. Why a partial implementation is not offered

I considered shipping the non-blocked portion and did not, for four reasons:

1. **There is no non-blocked slice.** Every D8 artefact terminates in one of the decisions above.
   The most trivial candidate — a tournament list screen — requires the `status` enum (§3.1), and
   tournament creation requires the initial state (§3.1) and the fee representation (§3.6).
2. **The acceptance standard cannot be met.** The mandate's §26 requires the tournament state
   machine, entry rules and scoring rules all to be *proven*. A partial slice would be incapable
   of acceptance, and would create a schema that the eventual decision pack would invalidate.
3. **Phase 9 §47 forbids it independently.** The agent "is not authorized to independently decide:
   new business rules … new states … new permissions … new data ownership … new APIs that change
   product behavior", and "If an implementation conflict is discovered, work stops at the affected
   decision and is escalated for resolution."
4. **The tables would be a guess.** `tournament_scores` and `tournament_results` exist as names
   only. Creating them without §3.2 and §3.3 would hard-code a scoring model into the schema
   where the D7 report's discipline is to leave configuration to D4.

I also did not infer a format from the market-research document. The mandate forbids it
explicitly ("Do not infer tournament rules from generic golf software"), and that document
contains no scoring format in any case.

## 5. Regression status at the point of stopping

D7's accepted baseline is intact and was not re-run, because no code changed. Last verified
before this session's stop:

| Check | Result |
| --- | --- |
| `dotnet build ClubStack.slnx` | Build succeeded, 0 warnings, 0 errors |
| `dotnet test ClubStack.slnx` | 913 passed, 0 failed, 0 skipped (220 domain / 80 application / 309 integration / 304 API) |
| `npx tsc --noEmit` | exit 0, no diagnostics |
| `npm run build` | built |

No D8 migration was created, so `MigrationStartupBehaviorTests` is untouched and its exact
migration count remains correct.

## 6. Exact next-session entry point

**D8 is blocked pending a PM Decision Pack.** Do not start D9. To unblock, supply a D8 decision
pack in the style of `docs/pm-prompts/D6-finance-integration-implementation.md` (B1–B19) that
answers, at minimum, decisions **D1–D9** in §3 above:

| # | Decision required | Blocks |
| --- | --- | --- |
| D1 | Tournament states, legal transitions, initial state | Everything; the whole phase |
| D2 | Scoring format (stroke play / stableford / both; handicapping yes/no) | `tournament_scores`, results, leaderboard |
| D3 | Single-round or multi-round; what identifies a round | Score schema and duplicate prevention |
| D4 | Category dimensions and cardinality | `tournament_categories`, TOUR-005 |
| D5 | Flight fields, cardinality, manual vs derived | `tournament_flights`, TOUR-006 |
| D6 | Entry fee: D4 product pointer or scalar amount; charge type and posting rule | Entry money, TOUR-003/TOUR-010 |
| D7 | Tee allocation: D7 booking (capacity + green fee) or independent reservation | Entry capacity, TOUR-007 |
| D8 | Entry eligibility, registration window, duplicate-entry key | TOUR-002 rules |
| D9 | Scoring authorization: submit vs amend vs finalize | Scoring permission tests |

A decision pack covering D1–D3 alone unblocks tournament setup, registration and the scorecard;
D4–D5 unblock categories and flights; D6–D8 unblock entry fees, tee allocation and eligibility;
D2 plus D9 unblock results and the leaderboard.

When the pack is issued:

1. Save it verbatim to `docs/pm-prompts/D8-tournaments-scoring-implementation.md` (replacing the
   current mandate copy, retained as `D8-tournaments-scoring-implementation-unresolved.md` per
   the D6 precedent) and add the archive row.
2. Implement D8 on the D7 machinery, without duplicating bookings, capacity, players, guests,
   charges, D6 finance, payments or idempotency.
3. Create a real EF migration under `src/ClubStack.Infrastructure/Migrations/`, add the D8
   database-level transition trigger for the state machine decided in D1, and update
   `MigrationStartupBehaviorTests` to the new exact migration count.
4. Reuse `IIdempotencyStore` for tournament entry, entry-fee charge, score submission and
   result finalization.
5. Implement the Phase 5 §51 screens, permission-gated on the three existing permissions.
6. Run the full regression, TypeScript and the SPA build; exercise the D8 screens against the
   running API where practical, and keep the D7 compilation-only limitation recorded as such.
7. Write `D8_Implementation_Session_Report.md` in the mandated 33-section structure.
