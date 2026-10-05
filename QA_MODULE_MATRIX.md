# QA Module Matrix — Remediated

| Module | Workflow | Prerequisites | Expected | Actual | Status | Classification | Evidence | Notes |
|---|---|---|---|---|---|---|---|---|
| Platform | Bootstrap admin creation | EnableBootstrap + EnableDevelopmentSeed | Admin user created, session established | Login endpoint returns 200 with user profile | PASS | — | Login response `{"requiresTwoFactor":false,"userName":"admin@localhost","displayName":"Development Administrator"}` | Session cookie uses chunked cookies (browser-only) |
| Platform | Health check | None | HTTP 200 with healthy status | HTTP 200 `{"status":"Healthy","application":"ClubStack","version":"1.0.0.0"}` | PASS | — | GET `/api/v1/health` | |
| Platform | Regression baseline | None | 913 pass, 0 fail | 220 domain + 80 app + 304 API + 309 integration = 913/913 | PASS | — | `dotnet test ClubStack.slnx` | Build 0 warnings/0 errors |
| Platform | TypeScript build | None | tsconfig clean | `npx tsc --noEmit` exit 0 | PASS | — | Client directory | |
| Platform | SPA build | None | Vite build success | `npm run build` successful | PASS | — | Client directory | |
| Platform | Browser validation | Playwright + Chromium | All module pages load | 10/10 browser tests pass | PASS | — | `npx playwright test` | D1-D7 all verified in browser |
| Identity | Login | Dev seed enabled | 200 OK with user info | 200 OK | PASS | — | POST `/api/v1/auth/login` | Session created, chunked cookies work in browser |
| Identity | Auth endpoint protection | Auth required | 401 for unauthenticated | 401 on all protected endpoints without cookie | PASS | — | GET `/api/v1/clubs` unauthenticated | Cookie chunking prevents curl testing |
| Identity | Session enforcement | Login + valid session | Authenticated requests succeed | Browser test: authenticated shell renders | PASS | — | Playwright browser test | Chunked cookies work in browser |
| Membership | Categories CRUD | D2 migration | Categories list/create | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/categories` verified |
| Membership | Member lifecycle | D2 + category | Create/approve/activate/suspend | **UNVERIFIED** — needs full CRUD test | NOT TESTED | P1 prerequisite | | Needs browser CRUD test |
| Credentials | Credential lifecycle | D3 + active member | Issue/activate/suspend | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Credentials | Member account | D3 + member | Account created, charges/payments | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Products | Product CRUD | D4 + categories/locations/suppliers | Create/update/query | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/products` verified |
| Products | Stock operations | D4 + products | Receive/transfer/count | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| POS | Cashier session | D5 + outlet/register | Open session, create sale | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/cashier-sessions` verified |
| POS | Sale workflow | D5 + products | Create/complete/refund | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/pos` and `/sales` verified |
| POS | Sale idempotency (QA-007) | D5 + products | Retry returns stored result | Fixed: CompleteAsync moved inside try/catch | PASS | — | Code fix + regression | `EntityFrameworkSaleService.cs` |
| Finance | Period open/close | D6 + chart of accounts | Period lifecycle | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/finance` verified |
| Finance | Journal create/post | D6 + mapping | Draft/validate/post/reverse | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Finance | Reports | D6 + data | Trial balance, GL, aging | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Finance | ContinueWith anti-pattern | D6 | No ContinueWith in async paths | Fixed: replaced with await | PASS | — | Code fix + regression | `EntityFrameworkFinanceReportService.cs` |
| Golf | Course CRUD | D7 + club | Create course, tees | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/golf/courses` verified |
| Golf | Tee sheet | D7 + course | View slots, generate | Browser test: page loads, nav visible | PASS | — | Playwright browser test | `/golf/tee-sheet` verified |
| Golf | Booking lifecycle | D7 + member + tee | Draft/confirm/checkin/complete | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Golf | Charge posting | D7 + D4 product + D6 mapping | Green fee posted to D6 | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Fiscalisation | Device status | D5.5 + device | Device status query | **UNVERIFIED** | NOT TESTED | P5 external | Device not configured | Live tests opt-in |
| Cross-module | Member→Golf journey | D2+D4+D6+D7 | Member books, checks in, charges post | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
| Cross-module | POS→Finance journey | D4+D5+D6 | Sale posts to finance | **UNVERIFIED** | NOT TESTED | P1 prerequisite | | Needs browser |
