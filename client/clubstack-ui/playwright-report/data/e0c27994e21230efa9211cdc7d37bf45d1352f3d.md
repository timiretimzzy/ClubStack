# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: pm-ui-consolidation.spec.ts >> PM-Directed UI Consolidation >> Finance domain: all child destinations load
- Location: tests\e2e\pm-ui-consolidation.spec.ts:147:3

# Error details

```
Test timeout of 30000ms exceeded.
```

```
Error: page.click: Test timeout of 30000ms exceeded.
Call log:
  - waiting for locator('a:has-text("Finance Overview")')

```

# Page snapshot

```yaml
- generic [ref=f1e2]:
  - generic [ref=f1e3]:
    - complementary [ref=f1e4]:
      - generic [ref=f1e5]: ClubStack
      - navigation [ref=f1e6]:
        - link "Dashboard" [ref=f1e7] [cursor=pointer]:
          - /url: /
        - button "Membership ▸" [ref=f1e9] [cursor=pointer]:
          - generic [ref=f1e10]: Membership
          - generic [ref=f1e11]: ▸
        - button "Products & Inventory ▸" [ref=f1e13] [cursor=pointer]:
          - generic [ref=f1e14]: Products & Inventory
          - generic [ref=f1e15]: ▸
        - button "Point of Sale ▸" [ref=f1e17] [cursor=pointer]:
          - generic [ref=f1e18]: Point of Sale
          - generic [ref=f1e19]: ▸
        - button "Finance ▸" [active] [ref=f1e21] [cursor=pointer]:
          - generic [ref=f1e22]: Finance
          - generic [ref=f1e23]: ▸
        - button "Golf ▸" [ref=f1e25] [cursor=pointer]:
          - generic [ref=f1e26]: Golf
          - generic [ref=f1e27]: ▸
        - button "Administration ▸" [ref=f1e29] [cursor=pointer]:
          - generic [ref=f1e30]: Administration
          - generic [ref=f1e31]: ▸
        - button "Reports ▸" [ref=f1e33] [cursor=pointer]:
          - generic [ref=f1e34]: Reports
          - generic [ref=f1e35]: ▸
      - generic [ref=f1e36]:
        - generic [ref=f1e37]: Development Administrator
        - button "Sign out" [ref=f1e38] [cursor=pointer]
    - main [ref=f1e39]:
      - heading "Dashboard" [level=1] [ref=f1e40]
      - paragraph [ref=f1e41]: Welcome back, Development Administrator. Recent club activity.
      - generic [ref=f1e42]:
        - heading "At a glance" [level=3] [ref=f1e43]
        - generic [ref=f1e44]:
          - generic [ref=f1e46]:
            - generic [ref=f1e47]: Members
            - generic [ref=f1e48]: Recent
          - generic [ref=f1e50]:
            - generic [ref=f1e51]: Open applications
            - generic [ref=f1e52]: "0"
      - generic [ref=f1e53]:
        - heading "Recent members" [level=3] [ref=f1e54]
        - table [ref=f1e55]:
          - rowgroup [ref=f1e56]:
            - row [ref=f1e57]:
              - columnheader "Member" [ref=f1e58]
              - columnheader "Status" [ref=f1e59]
              - columnheader [ref=f1e60]
          - rowgroup [ref=f1e61]:
            - row [ref=f1e62]:
              - cell [ref=f1e63]:
                - link "MEM-000001" [ref=f1e64] [cursor=pointer]:
                  - /url: /members/01a0e7e6-0bc1-7fd4-b449-91f5744b1caa
                - text: — Tawanda Timire
              - cell "Active" [ref=f1e65]
              - cell [ref=f1e67]:
                - link "Open" [ref=f1e68] [cursor=pointer]:
                  - /url: /members/01a0e7e6-0bc1-7fd4-b449-91f5744b1caa
            - row [ref=f1e69]:
              - cell [ref=f1e70]:
                - link "MEM-000002" [ref=f1e71] [cursor=pointer]:
                  - /url: /members/01a0e8dc-4a9a-7acc-8b40-089b09eea2eb
                - text: — John Magaro
              - cell "Prospect" [ref=f1e72]
              - cell [ref=f1e74]:
                - link "Open" [ref=f1e75] [cursor=pointer]:
                  - /url: /members/01a0e8dc-4a9a-7acc-8b40-089b09eea2eb
      - generic [ref=f1e76]:
        - heading "Recent applications" [level=3] [ref=f1e77]
        - table [ref=f1e78]:
          - rowgroup [ref=f1e79]:
            - row [ref=f1e80]:
              - columnheader "Applicant" [ref=f1e81]
              - columnheader "Category" [ref=f1e82]
              - columnheader "Status" [ref=f1e83]
          - rowgroup [ref=f1e84]:
            - row [ref=f1e85]:
              - cell "John Magaro" [ref=f1e86]
              - cell "FULL MEMBER" [ref=f1e87]
              - cell "Approved" [ref=f1e88]
            - row [ref=f1e90]:
              - cell "John Magaro" [ref=f1e91]
              - cell "FULL MEMBER" [ref=f1e92]
              - cell "Rejected" [ref=f1e93]
            - row [ref=f1e95]:
              - cell "Tawanda Timire" [ref=f1e96]
              - cell "FULL MEMBER" [ref=f1e97]
              - cell "Activated" [ref=f1e98]
  - generic [ref=f1e100]:
    - button "QA REC ●" [ref=f1e101] [cursor=pointer]
    - generic [ref=f1e102]:
      - generic [ref=f1e103]:
        - generic [ref=f1e104]: QA Recorder
        - button "×" [ref=f1e105] [cursor=pointer]
      - generic [ref=f1e106]:
        - generic [ref=f1e107]:
          - generic [ref=f1e108]: "Session:"
          - generic [ref=f1e109]: QA-SESSION-MUUV0E53-SH1OY2
        - generic [ref=f1e110]:
          - generic [ref=f1e111]: "Actions:"
          - generic [ref=f1e112]: "2"
        - generic [ref=f1e113]:
          - generic [ref=f1e114]: "Elapsed:"
          - generic [ref=f1e115]: 00:26
        - generic [ref=f1e116]:
          - generic [ref=f1e117]: "Last Action:"
          - generic [ref=f1e118]: "click: Finance▾"
        - generic [ref=f1e119]:
          - generic [ref=f1e120]: "Last Result:"
          - generic [ref=f1e121]: PASS
        - generic [ref=f1e122]:
          - generic [ref=f1e123]: "Last API:"
          - generic [ref=f1e124]: —
        - generic [ref=f1e125]:
          - generic [ref=f1e126]: "Last Error:"
          - generic [ref=f1e127]: —
      - generic [ref=f1e128]:
        - button "Pause" [ref=f1e129] [cursor=pointer]
        - button "Stop & Save" [ref=f1e130] [cursor=pointer]
        - button "Clear" [ref=f1e131] [cursor=pointer]
```

# Test source

```ts
  72  |     await login(page);
  73  |     await page.goto(UI + '/');
  74  |     await page.waitForTimeout(1000);
  75  |     await startQaSession(page);
  76  | 
  77  |     await page.click('text=Products & Inventory');
  78  |     await page.waitForTimeout(500);
  79  |     await page.click('text=Products');
  80  |     await page.waitForTimeout(2000);
  81  | 
  82  |     await page.click('button.tab:has-text("Categories")');
  83  |     await page.waitForTimeout(1000);
  84  | 
  85  |     await page.click('text=New category');
  86  |     await page.waitForTimeout(500);
  87  | 
  88  |     const timestamp = Date.now().toString(36);
  89  |     await page.fill('#catCode', `CAT-${timestamp}`);
  90  |     await page.fill('#catName', `Test Category ${timestamp}`);
  91  |     await page.fill('#catDesc', 'Test category for QA');
  92  |     await page.click('button:has-text("Create category")');
  93  |     await page.waitForTimeout(2000);
  94  | 
  95  |     const categoryVisible = await page.locator(`text=Test Category ${timestamp}`).isVisible();
  96  |     expect(categoryVisible).toBe(true);
  97  | 
  98  |     await page.click('button.tab:has-text("Products")');
  99  |     await page.waitForTimeout(1000);
  100 | 
  101 |     await page.click('text=New product');
  102 |     await page.waitForTimeout(500);
  103 | 
  104 |     const categoryOption = await page.locator(`option:has-text("Test Category ${timestamp}")`).count();
  105 |     expect(categoryOption).toBeGreaterThan(0);
  106 | 
  107 |     const sku = `SKU-${timestamp}`;
  108 |     await page.fill('#sku', sku);
  109 |     await page.fill('#name', `Test Product ${timestamp}`);
  110 |     await page.selectOption('#categoryId', { label: `Test Category ${timestamp}` });
  111 |     await page.fill('#cost', '10.00');
  112 |     await page.fill('#price', '25.00');
  113 |     await page.click('button:has-text("Create product")');
  114 |     await page.waitForTimeout(2000);
  115 | 
  116 |     const productVisible = await page.locator(`text=Test Product ${timestamp}`).isVisible();
  117 |     expect(productVisible).toBe(true);
  118 |   });
  119 | 
  120 |   test('POS domain: POS, Sales, Cashier Sessions', async ({ page }) => {
  121 |     await page.goto(UI + '/');
  122 |     await page.waitForTimeout(1000);
  123 |     await login(page);
  124 |     await page.goto(UI + '/');
  125 |     await page.waitForTimeout(1000);
  126 |     await startQaSession(page);
  127 | 
  128 |     await page.click('button:has-text("Point of Sale")');
  129 |     await page.waitForTimeout(1000);
  130 |     await page.click('a:has-text("Point of Sale")');
  131 |     await page.waitForTimeout(2000);
  132 |     await expect(page.locator('nav')).toBeVisible();
  133 | 
  134 |     await page.click('button:has-text("Point of Sale")');
  135 |     await page.waitForTimeout(1000);
  136 |     await page.click('a:has-text("Sales")');
  137 |     await page.waitForTimeout(2000);
  138 |     await expect(page.locator('nav')).toBeVisible();
  139 | 
  140 |     await page.click('button:has-text("Point of Sale")');
  141 |     await page.waitForTimeout(1000);
  142 |     await page.click('a:has-text("Cashier Sessions")');
  143 |     await page.waitForTimeout(2000);
  144 |     await expect(page.locator('nav')).toBeVisible();
  145 |   });
  146 | 
  147 |   test('Finance domain: all child destinations load', async ({ page }) => {
  148 |     await page.goto(UI + '/');
  149 |     await page.waitForTimeout(1000);
  150 |     await login(page);
  151 |     await page.goto(UI + '/');
  152 |     await page.waitForTimeout(1000);
  153 |     await startQaSession(page);
  154 | 
  155 |     await page.click('text=Finance');
  156 |     await page.waitForTimeout(500);
  157 | 
  158 |     const financePages = [
  159 |       'Finance Overview',
  160 |       'Chart of Accounts',
  161 |       'Journals',
  162 |       'Accounts Receivable',
  163 |       'Accounts Payable',
  164 |       'Payments',
  165 |       'Financial Periods',
  166 |       'Reconciliation',
  167 |     ];
  168 | 
  169 |     for (const pageName of financePages) {
  170 |       await page.click('button:has-text("Finance")');
  171 |       await page.waitForTimeout(1000);
> 172 |       await page.click(`a:has-text("${pageName}")`);
      |                  ^ Error: page.click: Test timeout of 30000ms exceeded.
  173 |       await page.waitForTimeout(1500);
  174 |       await expect(page.locator('nav')).toBeVisible();
  175 |     }
  176 |   });
  177 | 
  178 |   test('Golf domain: all child destinations load', async ({ page }) => {
  179 |     await page.goto(UI + '/');
  180 |     await page.waitForTimeout(1000);
  181 |     await login(page);
  182 |     await page.goto(UI + '/');
  183 |     await page.waitForTimeout(1000);
  184 |     await startQaSession(page);
  185 | 
  186 |     await page.click('text=Golf');
  187 |     await page.waitForTimeout(500);
  188 | 
  189 |     const golfPages = ['Courses', 'Tee Sheet', 'Bookings', 'Guests', 'Charges'];
  190 | 
  191 |     for (const pageName of golfPages) {
  192 |       await page.click('button:has-text("Golf")');
  193 |       await page.waitForTimeout(1000);
  194 |       await page.click(`a:has-text("${pageName}")`);
  195 |       await page.waitForTimeout(1500);
  196 |       await expect(page.locator('nav')).toBeVisible();
  197 |     }
  198 |   });
  199 | 
  200 |   test('Administration domain: Fiscalisation loads', async ({ page }) => {
  201 |     await page.goto(UI + '/');
  202 |     await page.waitForTimeout(1000);
  203 |     await login(page);
  204 |     await page.goto(UI + '/');
  205 |     await page.waitForTimeout(1000);
  206 |     await startQaSession(page);
  207 | 
  208 |     await page.click('text=Administration');
  209 |     await page.waitForTimeout(500);
  210 |     await page.click('text=Fiscalisation');
  211 |     await page.waitForTimeout(2000);
  212 |     await expect(page.locator('nav')).toBeVisible();
  213 |   });
  214 | 
  215 |   test('Reports domain: report destinations load', async ({ page }) => {
  216 |     await page.goto(UI + '/');
  217 |     await page.waitForTimeout(1000);
  218 |     await login(page);
  219 |     await page.goto(UI + '/');
  220 |     await page.waitForTimeout(1000);
  221 |     await startQaSession(page);
  222 | 
  223 |     await page.click('text=Reports');
  224 |     await page.waitForTimeout(500);
  225 |     await page.click('text=Financial Reports');
  226 |     await page.waitForTimeout(2000);
  227 |     await expect(page.locator('nav')).toBeVisible();
  228 |   });
  229 | 
  230 |   test('Direct URL access: /products/categories redirects to /products', async ({ page }) => {
  231 |     await page.goto(UI + '/');
  232 |     await page.waitForTimeout(1000);
  233 |     await login(page);
  234 |     await page.goto(UI + '/products');
  235 |     await page.waitForTimeout(2000);
  236 |     await expect(page.locator('nav')).toBeVisible();
  237 |     await expect(page.locator('.page-title')).toBeVisible();
  238 |   });
  239 | 
  240 |   test('Navigation: collapsible groups work', async ({ page }) => {
  241 |     await page.goto(UI + '/');
  242 |     await page.waitForTimeout(1000);
  243 |     await login(page);
  244 |     await page.goto(UI + '/');
  245 |     await page.waitForTimeout(1000);
  246 | 
  247 |     const membershipBtn = page.locator('button:has-text("Membership")');
  248 |     await expect(membershipBtn).toBeVisible();
  249 | 
  250 |     await membershipBtn.click();
  251 |     await page.waitForTimeout(500);
  252 | 
  253 |     const membersLink = page.locator('a:has-text("Members")');
  254 |     await expect(membersLink).toBeVisible();
  255 | 
  256 |     await membershipBtn.click();
  257 |     await page.waitForTimeout(500);
  258 | 
  259 |     const membersLinkHidden = await page.locator('a:has-text("Members")').isVisible();
  260 |     expect(membersLinkHidden).toBe(false);
  261 |   });
  262 | });
  263 | 
```