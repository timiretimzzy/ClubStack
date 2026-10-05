# PM Prompt Archive

Reusable record of Product Manager directives so they do not have to be repeated
between sessions. Each file is the prompt as issued for one phase/session.

| File | Phase | Status |
| --- | --- | --- |
| `D5-continuation-13-part-prompt.md` | D5 continuation | Applied |
| `D5-continuation-report-33-point-template.md` | D5 continuation | Applied |
| `D5.5-zimra-fiscalisation-integration.md` | D5.5 ZIMRA fiscalisation | Investigation only — blocked (see `D5.5_Session_Report.md`) |
| `D5.5-continuation-local-implementation.md` | D5.5 local implementation | Applied — see `D5.5_Continuation_Session_Report.md` |
| `D5.5-finalization-acceptance-hardening.md` | D5.5 finalization | Applied — see `D5.5_Finalization_Session_Report.md` |
| `D5.5-live-fdms-verification-and-final-acceptance-gate.md` | D5.5 live FDMS gate | **Blocked** — device not provisioned; see `D5.5_Live_Verification_Session_Report.md` |
| `D5.5-test-environment-fiscalisation-verification.md` | D5.5 test-environment verification | **Superseded** — endpoint root cause found and fixed; superseded by the final execution report |
| `D5.5-final-test-fiscalisation-execution.md` | D5.5 final TEST execution | **Applied** — live registration, certificates, license, day open, sale, duplicate refusal and refund all proven on TEST FDMS; see `D5.5_Final_Test_Fiscalisation_Execution_Report.md` |
| `D5.5-database-persistence-and-handoff.md` | D5.5 database persistence and handoff | **Applied** — D5.5 **ACCEPTED / PASS**; DB credential persisted at user scope, integration suite 189/189 green, 529/529 total; see `D5.5_Database_Persistence_and_Handoff_Session_Report.md` |
| `D6-finance-integration-implementation.md` | D6 Finance Integration | **AUTHORIZED** — PM Decision Pack B1–B19 resolved; implementation mandate in force; see `D6_Implementation_Session_Report.md` |
| `D6-finance-integration-implementation-original-unauthorized.md` | D6 Finance Integration (pre-decision) | **Superseded** — original undecided prompt, retained for the audit trail; it was the reason the first D6 attempt stopped |
| `D8-tournaments-scoring-implementation.md` | D8 Tournaments + Scoring | **Blocked** — mandate issued, but the specification set defines no tournament state machine and no scoring model; no implementation started. See `D8_Implementation_Blocked_Report.md` |

**Convention:** when the PM issues a new prompt, save it here verbatim before
starting work, and add a row to the table above. Do not paraphrase the PM's
wording in the saved copy — future sessions quote from these files directly.

Related deliverables live at the repo root: `D5_Session_Report.md` (original
18-section D5 report), `D5_Continuation_Session_Report.md` (33-point
continuation report), `D5.5_Session_Report.md` (41-point D5.5 investigation
report), `D5.5_Continuation_Session_Report.md` (D5.5 implementation report),
and `D5.5_Finalization_Session_Report.md` (D5.5 acceptance-hardening report), plus
`D5.5_Live_Verification_Session_Report.md` (live FDMS gate — blocked, device not
provisioned), `D5.5_Test_Environment_Verification_Session_Report.md` (earlier
test-environment verification — blocked, provisioning capability gap, since
resolved), `D5.5_Complete_Test-Environment_Provisioning_and_Fiscalisation_Session_Report.md`
(TEST endpoint map corrected and pinned by regression tests, provisioning implemented end to
end), and `D5.5_Final_Test_Fiscalisation_Execution_Report.md` (live registration, certificates,
license, day open, sale, duplicate refusal and refund proven on TEST FDMS), and
`D5.5_Database_Persistence_and_Handoff_Session_Report.md` (**current state** — D5.5
**accepted/pass**; database access unblocked and persisted, so the user does not need to
supply the credential again; full suite 529/529 green; `BadCertificateSignature` recorded
only as a deferred vendor/DLL issue; exact D6 handoff point stated).

**Current project state:** D5.5 is accepted and closed. D6 Finance Integration is
authorized for implementation by the PM Decision Pack (B1–B19) recorded in
`D6-finance-integration-implementation.md`; the pre-decision prompt is preserved
as `D6-finance-integration-implementation-original-unauthorized.md`. D7 Golf Operations is
accepted and closed (913/913). **D8 Tournaments + Scoring is BLOCKED**: the mandate in
`D8-tournaments-scoring-implementation.md` is in force, but the specification set authorizes
no tournament lifecycle and no scoring model, so no D8 code was written. The exact missing
decisions and the exact implementation each one blocks are recorded in
`D8_Implementation_Blocked_Report.md`. Local setup, including the persisted
`CLUBSTACK_DB_PASSWORD` mechanism, is documented in `docs/local-setup.md`.
