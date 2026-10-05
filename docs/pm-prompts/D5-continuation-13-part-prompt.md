# D5 Continuation — 13-Part Work Directive

> **Provenance note (important).** This file is a *reconstruction* of the D5
> continuation directive, captured from the session working summary after the
> fact. It is **not** a verbatim copy of the PM's original message — the exact
> original wording was not retained. The substance and the ordering of the 13
> parts below are faithful to what was executed and verified; the phrasing is
> not guaranteed word-for-word. The 33-point report template
> (`D5-continuation-report-33-point-template.md`) **is** verbatim.
>
> If the PM reissues this directive, replace this file with the verbatim text.

## Standing constraints

- Do **NOT** start D6/D7.
- Do **NOT** redesign architecture.
- Do **NOT** invent product rules.
- Do **NOT** weaken or delete tests to reach green.
- The agent must not declare D5 "accepted"; the PM alone accepts.
- End the session with exactly one status line, then STOP.

## The 13 parts

1. **Reference generation before `BeginAsync`.** Sale/member-account references
   must be pre-allocated before the Npgsql transaction opens, per the D4 lesson
   ("unassigned command on an active transaction"). Honour caller-supplied
   references; otherwise use the documented prefixes.
2. **Concurrency → 409.** Map `DbUpdateConcurrencyException` to HTTP 409 with the
   established conflict code, not 500. Keep the existing pre-domain
   `ConcurrencyException` → 409 mapping.
3. **Idempotency hardening.** Scope keys to the authenticated actor and the
   operation context (not globally); detect payload mismatch; allow a failed
   record to be re-acquired; keep the context clean after rollback.
4. **FK configuration / nullability.** Configure the missing D5 foreign keys in
   the EF model and make `SaleLineEntity.DiscountType` optional, matching the
   entity and migration.
5. **Cashiering invariants.** Enforce one active session per register at the
   database level (partial unique index), and translate the resulting unique
   violation into a 409.
6. **Test expansion.** Add D5 integration coverage for idempotent replay,
   payload-mismatch rejection, concurrent same-key completion, failed-key retry,
   per-operation and per-actor key independence, concurrent voids, the
   one-active-session-per-register rule, and sale-row referential integrity.
7. **POS SPA — product search.** Build the `/pos` product search surface.
8. **POS SPA — current sale.** Build the cart, tendering and sale-completion surface.
9. **POS SPA — cashiering screens.** Build Current Session, Open Session, Cash
   Movements, Close Session, Reconciliation and History.
10. **POS SPA — sales history.** Build the `/sales` search/detail/receipt/void/refund surface.
11. **SPA wiring.** Add routes and permission-gated navigation; typecheck and build.
12. **Full regression.** Full solution build, full test run against real
    PostgreSQL, and SPA build.
13. **Report and stop.** Produce the mandatory 33-point report, then end with
    exactly one permitted status line and stop.

## Deliverable

`D5_Continuation_Session_Report.md` at the repo root, structured on the 33-point
template.
