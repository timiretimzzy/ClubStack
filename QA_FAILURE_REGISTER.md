# QA Failure Register

Every failure gets: ID, Module, Workflow, Classification, Severity, Expected, Actual, Root cause, Prerequisite, Reproduction steps, Correlation ID, Fix status, Regression status.

---

## QA-001 — Session cookie validation fails outside browser

- **Module**: Identity / D1
- **Workflow**: Login → authenticated requests
- **Classification**: P2 — Configuration / Test Data (test tooling limitation)
- **Severity**: Medium
- **Expected**: After login, subsequent authenticated requests succeed
- **Actual**: Login returns 200 OK with user profile, but all authenticated requests return HTTP 401 when tested via curl/Invoke-RestMethod
- **Root cause**: The authentication cookie uses ASP.NET Core chunked cookie mechanism (`ClubStack.Auth` with `chunks-N` placeholder and `ClubStack.AuthC1/C2/C3` shards). Browsers reconstruct this correctly; command-line HTTP clients (curl, PowerShell WebRequestSession) do not reconstruct the chunked cookie, so the session claim is never sent with the request.
- **Prerequisite**: None — this is a test tooling limitation, not a product defect.
- **Reproduction steps**:
  1. POST `/api/v1/auth/login` with valid credentials → 200 OK
  2. GET `/api/v1/auth/me` with the returned cookies → 401 Unauthorized
  3. Same request in browser → 200 OK (browser reconstructs chunked cookie)
- **Correlation ID**: N/A (test tooling)
- **Fix status**: NOT A FIXABLE DEFECT — browser-only behavior. QA must use the browser.
- **Regression status**: No regression — this is expected behavior.

---

## QA-002 — GolfIdempotency.MarkFailedAsync silently swallows exceptions

- **Module**: Golf (D7)
- **Workflow**: Idempotent command retry after failure
- **Classification**: P3 — Product Defect
- **Severity**: High
- **Expected**: If `store.FailAsync` fails, the original exception is preserved and the failure is logged
- **Actual**: `catch { }` on `MarkFailedAsync` silently discards all exceptions. If `FailAsync` fails (database error), the idempotency key remains permanently in "InProgress" state, blocking all future retries of the same operation indefinitely.
- **Root cause**: Empty catch block in `GolfIdempotency.MarkFailedAsync` (line 103-105). The code comment states "A key claimed for a request that then failed must be released, or the client's retry would be refused as 'in progress' forever." The empty catch defeats this guarantee.
- **Prerequisite**: Requires an idempotency key to be claimed and `FailAsync` to be called (currently `MarkFailedAsync` is not called in the codebase, so this is latent).
- **Reproduction steps**: Simulate a database failure during `FailAsync` → key stuck in InProgress forever.
- **Correlation ID**: N/A
- **Fix status**: FIXED — `catch (Exception ex) when (ex is not OperationCanceledException)` added; requires adding `ILogger` for diagnostics.
- **Regression status**: To be verified after fix.

---

## QA-003 — Session expiry does not revoke server-side session record

- **Module**: Identity / D1
- **Workflow**: Absolute session lifetime expiry
- **Classification**: P3 — Product Defect
- **Severity**: Low
- **Expected**: When a session expires (`ExpiresAtUtc <= utcNow`), the `user_sessions` row is updated (`RevokedAtUtc` set, `IsTerminated` set) before the client is signed out.
- **Actual**: `UserSessionDto.IsActive` computes to `false` for expired sessions (`e is { IsTerminated: false, RevokedAtUtc: null } && e.ExpiresAtUtc > DateTimeOffset.UtcNow`). The first check in `SessionEnforcementMiddleware` (`!session.IsActive`) catches expired sessions and calls `TerminateAsync` WITHOUT calling `RevokeAsync`. The subsequent `if (session.ExpiresAtUtc <= utcNow)` block is dead code — never reached for expired sessions. The `user_sessions` row remains with `IsTerminated = false` and `RevokedAtUtc = null`.
- **Root cause**: The `IsActive` computed property already evaluates `ExpiresAtUtc`, making the explicit `ExpiresAtUtc` check dead code. The `RevokeAsync` call inside that block is never executed for expired sessions, creating zombie session records.
- **Prerequisite**: None — occurs naturally over time.
- **Reproduction steps**: Create a session with a short lifetime → wait for expiry → check `user_sessions` table → row has `IsTerminated = false`.
- **Correlation ID**: N/A
- **Fix status**: OPEN — to be fixed by restructuring the expiry check to call `RevokeAsync` before `TerminateAsync`.
- **Regression status**: To be verified after fix.

---

## QA-004 — D5 POS frontend not built

- **Module**: POS / Cashiering (D5)
- **Workflow**: Cashier operations through UI
- **Classification**: P4 — UX/UI Defect
- **Severity**: High
- **Expected**: D5 POS screens exist at `/pos`, `/sales`, `/cashier-sessions`
- **Actual**: D5 backend is complete (10 tables, 349 tests), but no SPA frontend was built. The routes `/pos`, `/sales`, `/cashier-sessions` are not registered in `App.tsx`. No navigation entries exist. The D5 report explicitly states "No SPA routes exist."
- **Root cause**: D5 was implemented as "Backend Only" per the session scope. The frontend was not built.
- **Prerequisite**: None — this is a scope gap.
- **Reproduction steps**: Navigate to `/pos` → 404 or blank page.
- **Correlation ID**: N/A
- **Fix status**: OPEN — D5 frontend not built. This is a scope decision, not a bug, but blocks UI testing.
- **Regression status**: N/A

---

## QA-005 — D6 Finance SPA never browser-tested

- **Module**: Finance (D6)
- **Workflow**: Finance operations through UI
- **Classification**: P4 — UX/UI Defect
- **Severity**: Medium
- **Expected**: All 9 finance SPA screens work correctly in the browser
- **Actual**: The D6 report states "SPA screens never exercised against a running API in a browser — only compile/build; field-level mismatches would only surface at runtime." The D6 report's known limitation section explicitly says this.
- **Root cause**: D6 was tested via automated tests and compilation only, not via browser execution.
- **Prerequisite**: None — needs browser testing.
- **Reproduction steps**: Navigate to `/finance` → verify all screens render correctly.
- **Correlation ID**: N/A
- **Fix status**: OPEN — needs browser verification.
- **Regression status**: To be verified after browser testing.

---

## QA-006 — D7 Golf SPA never browser-tested

- **Module**: Golf (D7)
- **Workflow**: Golf operations through UI
- **Classification**: P4 — UX/UI Defect
- **Severity**: Medium
- **Expected**: All 6 golf SPA screens work correctly in the browser
- **Actual**: The D7 report's known limitations state "The six SPA screens compile, build and follow the codebase's patterns, but they have never been exercised against a running API in a browser. Field-level mismatches would only surface at runtime."
- **Root cause**: D7 was tested via automated tests and compilation only, not via browser execution.
- **Prerequisite**: None — needs browser testing.
- **Reproduction steps**: Navigate to `/golf/courses` → verify screens render correctly.
- **Correlation ID**: N/A
- **Fix status**: OPEN — needs browser verification.
- **Regression status**: To be verified after browser testing.

---

## QA-007 — D5 idempotency key left incomplete on exception

- **Module**: POS (D5)
- **Workflow**: Sale completion with idempotency
- **Classification**: P3 — Product Defect
- **Severity**: Medium
- **Expected**: If `_idempotency.CompleteAsync` throws, the idempotency key is still marked complete or the original exception is preserved
- **Actual**: In `EntityFrameworkSaleService.CompleteAsync`, the `_idempotency.CompleteAsync` call is outside the `try/catch` block. If it throws, the sale is committed but the idempotency key remains permanently "InProgress," blocking future retries.
- **Root cause**: The idempotency completion is placed after the try/catch block, so exceptions from `CompleteAsync` are not handled. Compare with golf booking service where `CompleteAsync` IS inside the try block.
- **Prerequisite**: A sale with an idempotency key that fails during `CompleteAsync`.
- **Reproduction steps**: Simulate `CompleteAsync` throwing → sale committed but key stuck.
- **Correlation ID**: N/A
- **Fix status**: OPEN — to be fixed by moving `CompleteAsync` inside the try block with proper exception handling.
- **Regression status**: To be verified after fix.
