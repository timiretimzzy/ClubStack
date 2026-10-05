# QA Readiness Matrix

For every completed module, current testability status.

| Module | Automated Tests | API Tested | UI Tested | Cross-module Tested | Configuration Verified | Known Blockers | Known Defects | PM Manual QA Ready |
|---|---|---|---|---|---|---|---|---|
| D0 Engineering Foundation | ✅ (baseline) | ✅ | ❌ | ❌ | ✅ | None | None | YELLOW |
| D1 Identity/Security | ✅ (913 total) | ✅ | ❌ | ❌ | ✅ | Session cookie chunked (browser-only); auth needs browser verification | QA-001, QA-003 | YELLOW |
| D2 Membership | ✅ | ✅ | ❌ | ❌ | ⚠️ Needs category/setup | Auth requires browser | None | YELLOW |
| D3 Credentials/Accounts | ✅ | ✅ | ❌ | ❌ | ⚠️ Needs member + chart of accounts | Auth requires browser | None | YELLOW |
| D4 Products/Inventory | ✅ | ✅ | ❌ | ❌ | ⚠️ Needs categories/locations/suppliers | Auth requires browser | None | YELLOW |
| D5 POS/Cashiering | ✅ | ✅ | ❌ | ❌ | ⚠️ Needs outlet/register/payment method | **No SPA frontend built** (QA-004); backend only | QA-004, QA-007 | RED |
| D5.5 Fiscalisation | ✅ (189/189) | ✅ | ❌ | ❌ | ❌ **Device not configured** | Device not provisioned; live tests opt-in | QA-005 (CloseDay vendor defect) | RED |
| D6 Finance | ✅ (707 total) | ✅ | ❌ | ❌ | ⚠️ Needs chart of accounts + mappings | SPA never browser-tested (QA-005) | QA-005 | YELLOW |
| D7 Golf | ✅ (913 total) | ✅ | ❌ | ❌ | ⚠️ Needs course/tee/D4 product | SPA never browser-tested (QA-006); D8 blocked | QA-006 | YELLOW |
| Cross-module | ❌ | ❌ | ❌ | ❌ | ⚠️ Needs all prerequisites configured | Auth requires browser; D5 no SPA | Multiple | RED |

**Legend**: ✅ = ready | ⚠️ = usable with setup | ❌ = not tested | GREEN/YELLOW/RED = readiness score

### Notes
- D5.5 fiscalisation is accepted as a module but requires the TEST FDMS device.
- D8 is BLOCKED and not tested.
- All SPA screens were compiled and built but never exercised in a browser (known limitation across D5, D6, D7).
- The session authentication cookie uses chunked cookies that work in browsers but not in command-line HTTP clients. All UI testing must use the browser.
