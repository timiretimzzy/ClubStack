# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: pm-ui-consolidation.spec.ts >> PM-Directed UI Consolidation >> POS domain: POS, Sales, Cashier Sessions
- Location: tests\e2e\pm-ui-consolidation.spec.ts:120:3

# Error details

```
Test timeout of 30000ms exceeded.
```

```
Error: page.click: Test timeout of 30000ms exceeded.
Call log:
  - waiting for locator('a:has-text("Sales")')

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
        - button "Point of Sale ▸" [active] [ref=f1e17] [cursor=pointer]:
          - generic [ref=f1e18]: Point of Sale
          - generic [ref=f1e19]: ▸
        - button "Finance ▸" [ref=f1e21] [cursor=pointer]:
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
      - heading "Point of Sale" [level=1] [ref=f1e40]
      - paragraph [ref=f1e41]: Search products, build the sale and take payment.
      - generic [ref=f1e42]:
        - generic [ref=f1e43]:
          - generic [ref=f1e44]: Register
          - combobox "Register" [ref=f1e45]:
            - option "Select a register…" [selected]
        - generic [ref=f1e46]:
          - generic [ref=f1e47]: Member card
          - generic [ref=f1e48]:
            - textbox "Member card" [ref=f1e49]:
              - /placeholder: Scan or type credential
            - button "Find" [ref=f1e50] [cursor=pointer]
        - generic [ref=f1e51]:
          - generic [ref=f1e52]: Member
          - generic [ref=f1e53]: Walk-in customer
      - generic [ref=f1e54]:
        - generic [ref=f1e55]:
          - heading "Product search" [level=3] [ref=f1e56]
          - textbox "Search by name, SKU or barcode" [ref=f1e58]
          - generic [ref=f1e59]: Select a register to search products.
        - generic [ref=f1e60]:
          - heading "Current sale" [level=3] [ref=f1e61]
          - generic [ref=f1e62]: No items on this sale yet.
          - generic [ref=f1e63]:
            - button "Create sale" [disabled] [ref=f1e64]
            - button "Start a new sale" [ref=f1e65] [cursor=pointer]
  - generic [ref=f1e66]:
    - button "QA REC ●" [ref=f1e67] [cursor=pointer]
    - generic [ref=f1e68]:
      - generic [ref=f1e69]:
        - generic [ref=f1e70]: QA Recorder
        - button "×" [ref=f1e71] [cursor=pointer]
      - generic [ref=f1e72]:
        - generic [ref=f1e73]:
          - generic [ref=f1e74]: "Session:"
          - generic [ref=f1e75]: QA-SESSION-MUUUZPLT-8XMK2T
        - generic [ref=f1e76]:
          - generic [ref=f1e77]: "Actions:"
          - generic [ref=f1e78]: "3"
        - generic [ref=f1e79]:
          - generic [ref=f1e80]: "Elapsed:"
          - generic [ref=f1e81]: 00:25
        - generic [ref=f1e82]:
          - generic [ref=f1e83]: "Last Action:"
          - generic [ref=f1e84]: "click: Point of Sale▾"
        - generic [ref=f1e85]:
          - generic [ref=f1e86]: "Last Result:"
          - generic [ref=f1e87]: PASS
        - generic [ref=f1e88]:
          - generic [ref=f1e89]: "Last API:"
          - generic [ref=f1e90]: "200"
        - generic [ref=f1e91]:
          - generic [ref=f1e92]: "Last Error:"
          - generic [ref=f1e93]: —
      - generic [ref=f1e94]:
        - button "Pause" [ref=f1e95] [cursor=pointer]
        - button "Stop & Save" [ref=f1e96] [cursor=pointer]
        - button "Clear" [ref=f1e97] [cursor=pointer]
```

# Test source

```ts
  36  |     await toggle.click();
  37  |     await page.waitForTimeout(500);
  38  |   }
  39  |   await page.click('text=Start QA Session');
  40  |   await page.waitForTimeout(500);
  41  | }
  42  | 
  43  | test.describe('PM-Directed UI Consolidation', () => {
  44  |   test('Membership domain: Members and Categories', async ({ page }) => {
  45  |     await page.goto(UI + '/');
  46  |     await page.waitForTimeout(1000);
  47  |     await login(page);
  48  |     await page.goto(UI + '/');
  49  |     await page.waitForTimeout(1000);
  50  |     await startQaSession(page);
  51  | 
  52  |     await page.click('text=Membership');
  53  |     await page.waitForTimeout(500);
  54  | 
  55  |     await page.click('text=Members');
  56  |     await page.waitForTimeout(2000);
  57  |     await expect(page.locator('nav')).toBeVisible();
  58  |     await expect(page.locator('.page-title')).toBeVisible();
  59  | 
  60  |     await page.click('text=Membership');
  61  |     await page.waitForTimeout(500);
  62  |     await page.click('text=Categories');
  63  |     await page.waitForTimeout(2000);
  64  |     await expect(page.locator('.page-title')).toBeVisible();
  65  |     const title = await page.locator('.page-title').textContent();
  66  |     expect(title).toContain('ategor');
  67  |   });
  68  | 
  69  |   test('Products & Inventory: Product Category creation workflow', async ({ page }) => {
  70  |     await page.goto(UI + '/');
  71  |     await page.waitForTimeout(1000);
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
> 136 |     await page.click('a:has-text("Sales")');
      |                ^ Error: page.click: Test timeout of 30000ms exceeded.
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
  172 |       await page.click(`a:has-text("${pageName}")`);
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
```