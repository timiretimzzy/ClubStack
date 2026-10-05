# QA Prerequisite Map

For each module, this document states: "Before testing X, you must do Y."

---

## Platform / Identity (D0-D1)

**Prerequisites:**
1. `CLUBSTACK_DB_PASSWORD` set at user scope
2. PostgreSQL running on localhost:5432, role `clubstack` with `CREATEDB`
3. `EnableBootstrap: true`, `EnableDevelopmentSeed: true`
4. Database migrations applied at startup

**Before testing D1 auth:**
- Complete the bootstrap admin creation (POST `/api/v1/bootstrap/admin`)
- The dev seed creates "Development Administrator" with username `admin@localhost`
- Verify the `user_sessions` table has an active row after login

**Known issue:** Session cookie uses ASP.NET Core chunked cookies (works in browser, not in curl). QA must use the browser.

---

## Membership (D2)

**Prerequisites:**
1. D0-D1 green (platform + identity)
2. `CLUBSTACK_DB_PASSWORD` set
3. Migration `D2_Membership` applied
4. Membership categories seeded (or create via `/api/v1/membership-categories`)

**Before testing membership workflows:**
- Create at least one membership category with fee schedule
- Ensure the chart of accounts has a membership fee account mapped
- Verify a starter role (Membership Officer) has `Membership.View`, `Membership.Manage` permissions

**Known limitations:**
- Fee settlement (Paid state) is D3, not D2
- Assessments are recorded, never settled in D2

---

## Credentials + Member Accounts (D3)

**Prerequisites:**
1. D0-D2 green
2. At least one active member exists
3. `D3_CredentialsAndAccounts` migration applied
4. Chart of accounts configured (receivable account mapped)

**Before testing D3:**
- Create a member (D2 workflow) or use the dev seed member
- Verify the `member_accounts` table has a row for the member
- Configure posting-rule mappings so charges post to a receivable

**Known limitations:**
- `Accounts.Adjustment.Approve` permission exists but no approval workflow (adjustments post immediately)
- Aging shows net derived balance vs per-charge residuals (documented)

---

## Products + Inventory (D4)

**Prerequisites:**
1. D0-D3 green
2. `D4_ProductsAndInventory` migration applied
3. Product categories, locations, suppliers exist (or create them)

**Before testing D4:**
- Create product categories (via `/api/v1/product-categories`)
- Create inventory locations (via `/api/v1/inventory-locations`)
- Create at least one supplier (via `/api/v1/suppliers`)
- Configure D4 posting-rule mappings for stock movements

**Known limitations:**
- `Inventory.Category.*` permission keys do not exist (category mutations gated by `Inventory.Product.Create`/`Edit`)
- Wastage and Adjustment share one endpoint; wastage posts immediately

---

## POS + Cashiering (D5)

**Prerequisites:**
1. D0-D4 green
2. `D5_PosAndCashiering` migration applied
3. Products exist in inventory with stock
4. An outlet exists (via `/api/v1/outlets` or system configuration)
5. A payment method exists

**Before testing D5:**
- Create/configure a POS outlet
- Create a register
- Ensure products have prices and stock quantities
- Configure D5 posting-rule mappings

**Known limitations:**
- D5 backend is complete but SPA frontend is NOT built (no `/pos`, `/sales`, `/cashier-sessions` routes)
- Reference generation inside a transaction (HIGH RISK)
- Concurrency exceptions still return HTTP 500 (should be 409)
- No unique index for one-open-session-per-register
- D5 is NOT independently closed

---

## Fiscalisation (D5.5)

**Prerequisites:**
1. D5 complete
2. Fiscal device configured (TEST environment)
3. `D55_TEST_DEVICE_ID`, `D55_TEST_SERIAL`, `D55_TEST_ENVIRONMENT=TEST` set
4. Activation key and license loaded
5. Fiscal day opened

**Before testing D5.5:**
- Open a fiscal day via the fiscalisation endpoint
- Ensure the FDMS device is reachable on the TEST ZIMRA host
- Do NOT hammer the device — test idempotently

**Known issues:**
- `CloseDay` has `BadCertificateSignature` — classified P5 external vendor-engine defect, do not attempt to fix
- Live FDMS tests are opt-in (skip unless `D55_TEST_DEVICE_ID` is set)

---

## Finance (D6)

**Prerequisites:**
1. D0-D5 green
2. `D6_FinanceIntegration` migration applied
3. Chart of accounts created (at least one asset, liability, equity, income, expense account)
4. Posting-rule mappings configured
5. A financial period opened

**Before testing D6:**
- Create at least 4 accounts: Cash (asset), Receivables (asset), Revenue (income), Retained Earnings (equity)
- Map the `PosMemberAccountCharge` posting rule to the correct accounts
- Open a financial period
- Ensure D3/D5 outbox handlers can emit finance messages

**Critical:** Without chart of accounts + posting mappings, all finance posting is inert. This is a P1 prerequisite, not a defect.

---

## Golf (D7)

**Prerequisites:**
1. D0-D6 green
2. `D7_GolfOperations` migration applied
3. A club exists (the dev seed creates "Demo Country Club")
4. A golf course exists (the dev seed creates "Main Course")
5. Tees exist with green-fee and guest-fee D4 product links
6. D6 posting-rule mappings for golf charges
7. A D4 product configured as the green-fee product

**Before testing D7:**
- Verify a golf course exists (POST `/api/v1/golf/courses` or check dev seed)
- Verify tees exist and have D4 product links for green fees
- Configure posting-rule mappings so golf charges post to D6
- Ensure a member exists (for booking as member player)

**Known limitations:**
- Green fees are zero until a tee's green-fee/guest-fee product points at a priced D4 product
- No golf payment capture (settlement via D6 receivables)
- `GenerateTeeSlotsAsync` returns single-date sheet
- Idempotency key for `AddChargeAsync` uses generated timestamp, not request correlation
- Tournaments/scoring excluded (D8, blocked)

---

## Cross-Module Prerequisites

**Member journey (D2→D3→D6):**
- Membership category + fee schedule (D2)
- Active member + credential (D3)
- Chart of accounts + posting mapping (D6)
- Financial period open (D6)

**Retail/POS journey (D4→D5→D5.5→D6):**
- Product + stock (D4)
- Outlet + register + payment method (D5)
- Fiscal day open (D5.5)
- Posting rules (D6)

**Golf member journey (D2→D4→D7→D6):**
- Active member (D2)
- Course + tee + green-fee product (D4→D7)
- Posting rules for golf charges (D6)
- Financial period open (D6)

**Golf guest journey (D7→D6):**
- Course + tee (D7)
- Guest record (D7)
- Guest charge posting (D6)
- Posting rules (D6)
