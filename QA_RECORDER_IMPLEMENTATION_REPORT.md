# QA Recorder Implementation Report

**Date:** 2026-09-28
**Scope:** Development-only QA session recorder for manual testing traceability

---

## Files Created

| File | Purpose |
|---|---|
| `src/qa/types.ts` | TypeScript interfaces for all recorder events |
| `src/qa/recorder.ts` | Core recorder: session management, fetch/XHR interception, DOM observation, console capture |
| `src/qa/QaDashboard.tsx` | Live QA dashboard UI with session controls |
| `src/qa/persistence.ts` | NDJSON + JSON session export |
| `src/qa/report-generator.ts` | Markdown report generation |
| `src/vite-env.d.ts` | Vite client types reference |
| `tests/e2e/qa-recorder.spec.ts` | Playwright tests for the recorder |

## Files Modified

| File | Change |
|---|---|
| `src/main.tsx` | Attach recorder listeners, restore session on page load, render QaDashboard |
| `src/styles.css` | QA recorder panel styles |

---

## Recorder Architecture

### Core (`src/qa/recorder.ts`)

The `QaRecorder` class is a module-level singleton (`qaRecorder`) that:

1. **Session Management** — `start()`, `stop()`, `pause()`, `resume()`, `clear()`
2. **Network Interception** — Patches `window.fetch` and `XMLHttpRequest.prototype.open/send`
3. **DOM Observation** — `MutationObserver` on `document.body` for UI changes
4. **Console Capture** — Patches `console.error` and `console.warn`
5. **Route Tracking** — `popstate` and `hashchange` listeners
6. **State Persistence** — `sessionStorage` for cross-navigation session recovery

### Event Flow

```
User Action → recordAction() → emit(action event)
  ↓
Fetch/XHR Interceptor → emit(network event) [correlated by actionId]
  ↓
MutationObserver → queueUiChange() → emit(ui_change event) [correlated by actionId]
  ↓
Console Patch → emit(console event) [correlated by actionId]
  ↓
finalizeAction() → emit(action_end event) [result + classification]
```

### Action ID Correlation

Every action gets a unique `actionId` (e.g., `ACT-000001`). All network requests, UI changes, and console events within the action window reference this `actionId`. This enables the critical correlation:

```
ACT-000237 → click "Confirm booking"
  → REQ-000891: POST /api/v1/golf/bookings/123/confirm → 409
  → CHG-000123: toast "Booking exceeds available capacity"
  → CON-000045: console.error "TypeError: ..."
  → Result: FAIL
```

---

## Event Schema

### ActionEvent
```json
{
  "actionId": "ACT-000001",
  "sessionId": "QA-SESSION-...",
  "type": "click",
  "timestamp": "2026-09-28T12:00:00.000Z",
  "route": "/golf/bookings/123",
  "pageTitle": "ClubStack",
  "element": {
    "tagName": "button",
    "role": "button",
    "text": "Confirm booking",
    "ariaLabel": "Confirm booking",
    "name": "",
    "id": "",
    "testId": "booking-confirm",
    "accessibleName": "Confirm booking",
    "parent": "div.booking-actions",
    "selector": "div.booking-actions > button.btn-primary",
    "enabled": true,
    "visible": true,
    "viewportX": 120,
    "viewportY": 340
  }
}
```

### NetworkEvent
```json
{
  "requestId": "REQ-000001",
  "actionId": "ACT-000001",
  "sessionId": "QA-SESSION-...",
  "method": "POST",
  "url": "/api/v1/golf/bookings/123/confirm",
  "route": "/golf/bookings/123",
  "timestamp": "2026-09-28T12:00:01.000Z",
  "duration": 245,
  "status": 409,
  "responseContentType": "application/json",
  "responseBody": "{\"code\":\"CAPACITY_EXCEEDED\",...}",
  "errorBody": "{\"code\":\"CAPACITY_EXCEEDED\",...}",
  "correlationId": "abc-123",
  "businessErrorCode": "CAPACITY_EXCEEDED",
  "retryCount": 0
}
```

### UiChangeEvent
```json
{
  "changeId": "CHG-000001",
  "actionId": "ACT-000001",
  "sessionId": "QA-SESSION-...",
  "timestamp": "2026-09-28T12:00:01.500Z",
  "changeType": "element_added",
  "description": "Booking exceeds available capacity",
  "selector": "div.toast-container > div.toast-error"
}
```

### ConsoleEvent
```json
{
  "consoleId": "CON-000001",
  "actionId": "ACT-000001",
  "sessionId": "QA-SESSION-...",
  "timestamp": "2026-09-28T12:00:01.600Z",
  "level": "error",
  "message": "TypeError: Cannot read properties of undefined...",
  "stack": "Error: ..."
}
```

---

## Sensitive Data Redaction

The recorder redacts:
- Password fields (detected by `type`, `name`, `id`, `aria-label` containing "password", "token", "secret", "key", "auth", "cookie", "license", "credential")
- Authorization headers
- Cookie headers
- Access tokens
- API keys

Redaction format: `field=password, value=[REDACTED]`

---

## Persistence Format

### NDJSON Event Stream
```
qa-results/logs/QA-SESSION-....ndjson
```
Each line is a JSON event (action, network, ui_change, console, action_end).

### Session JSON
```
qa-results/sessions/QA-SESSION-....json
```
Machine-readable summary with all actions, results, and evidence.

### Screenshots
```
qa-results/screenshots/QA-SESSION-.../ACT-000001.png
```
Before/after screenshots for important actions (submit, save, delete, confirm, etc.).

### Markdown Report
```
QA_SESSION_REPORT_QA-SESSION-....md
```
Human-readable report with full action trace.

---

## Browser Verification

### Automated Tests (`tests/e2e/qa-recorder.spec.ts`)

| Test | Status |
|---|---|
| Start session and verify session ID | PASS |
| Click navigation item and verify action recorded | PASS |
| Trigger successful API request and verify correlation | PASS |
| Pause and resume recording | PASS |
| Stop session and verify summary | PASS |
| Clear session | PASS |
| Verify recorder does not interfere with normal operation | PASS |

**Result:** 7/7 tests pass.

### Manual Verification

The recorder works with real browser interaction:
1. Open `http://localhost:5173/`
2. Click `QA REC ○` toggle
3. Click `Start QA Session`
4. Use ClubStack normally (login, navigate, click buttons, fill forms)
5. Click `Stop & Save` to export session

---

## How the PM Starts/Stops a QA Session

1. **Open ClubStack** in Chrome/Edge
2. **Click `QA REC ○`** (bottom-right corner)
3. **Click `Start QA Session`**
4. **Use ClubStack normally** — all actions are recorded automatically
5. **Click `Stop & Save`** to export the session (JSON + NDJSON + screenshots)
6. **Click `Clear`** to reset and start fresh

The panel shows:
- Session ID
- Action count
- Elapsed time
- Last action
- Last result (PASS/FAIL/NO_EFFECT/BLOCKED/UNKNOWN)
- Last API status
- Last error

---

## Known Limitations

1. **Screenshots** — captured for important actions but require manual review to verify content
2. **Classification** — failures are marked `UNCLASSIFIED` until human review
3. **Cross-tab** — recorder works within a single tab; cross-tab correlation not supported
4. **File uploads** — binary file content is not captured (only metadata)
5. **WebSocket** — WebSocket messages are not intercepted (only fetch/XHR)

---

## Session Controls

| Control | Behavior |
|---|---|
| `Start QA Session` | Begins recording with unique session ID |
| `Pause` | Temporarily stops recording (events discarded) |
| `Resume` | Continues recording after pause |
| `Stop & Save` | Ends session, exports JSON + NDJSON + screenshots |
| `Clear` | Resets recorder state (does not delete saved sessions) |

---

## Integration

The recorder is enabled in development mode (`import.meta.env.DEV`) or when `VITE_QA_RECORDER=true`. It is **never** enabled in production.

The recorder does not modify business logic, API behavior, or UI rendering. It only observes and records.
