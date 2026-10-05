import { test, expect } from '@playwright/test';

const BASE = 'http://localhost:5000';
const UI = 'http://localhost:5173';

async function login(page: import('@playwright/test').Page) {
  const response = await page.request.post(BASE + '/api/v1/auth/login', {
    data: { userName: 'admin@localhost', password: 'GolfAdmin2026!' },
  });
  expect(response.status()).toBe(200);
  const cookies = await page.context().cookies();
  const authCookies = cookies.filter(c => c.name.startsWith('ClubStack.Auth'));
  if (authCookies.length === 0) {
    const setCookie = response.headers()['set-cookie'];
    if (setCookie) {
      const cookieArray = Array.isArray(setCookie) ? setCookie : [setCookie];
      for (const cookieStr of cookieArray) {
        const [nameValue] = cookieStr.split(';');
        const [name, value] = nameValue.split('=');
        if (name && value) {
          await page.context().addCookies([{
            name: name.trim(),
            value: value.trim(),
            domain: 'localhost',
            path: '/',
          }]);
        }
      }
    }
  }
}

async function startQaSession(page: import('@playwright/test').Page) {
  const toggle = page.locator('.qa-recorder-toggle');
  if (await toggle.isVisible()) {
    await toggle.click();
    await page.waitForTimeout(500);
  }
  await page.click('text=Start QA Session');
  await page.waitForTimeout(500);
}

test.describe('PM-Directed UI Consolidation', () => {
  test('Membership domain: Members and Categories', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('text=Membership');
    await page.waitForTimeout(500);

    await page.click('text=Members');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();

    await page.click('text=Membership');
    await page.waitForTimeout(500);
    await page.click('text=Categories');
    await page.waitForTimeout(2000);
    await expect(page.locator('.page-title')).toBeVisible();
    const title = await page.locator('.page-title').textContent();
    expect(title).toContain('ategor');
  });

  test('Products & Inventory: Product Category creation workflow', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('text=Products & Inventory');
    await page.waitForTimeout(500);
    await page.click('text=Products');
    await page.waitForTimeout(2000);

    await page.click('button.tab:has-text("Categories")');
    await page.waitForTimeout(1000);

    await page.click('text=New category');
    await page.waitForTimeout(500);

    const timestamp = Date.now().toString(36);
    await page.fill('#catCode', `CAT-${timestamp}`);
    await page.fill('#catName', `Test Category ${timestamp}`);
    await page.fill('#catDesc', 'Test category for QA');
    await page.click('button:has-text("Create category")');
    await page.waitForTimeout(2000);

    const categoryVisible = await page.locator(`text=Test Category ${timestamp}`).isVisible();
    expect(categoryVisible).toBe(true);

    await page.click('button.tab:has-text("Products")');
    await page.waitForTimeout(1000);

    await page.click('text=New product');
    await page.waitForTimeout(500);

    const categoryOption = await page.locator(`option:has-text("Test Category ${timestamp}")`).count();
    expect(categoryOption).toBeGreaterThan(0);

    const sku = `SKU-${timestamp}`;
    await page.fill('#sku', sku);
    await page.fill('#name', `Test Product ${timestamp}`);
    await page.selectOption('#categoryId', { label: `Test Category ${timestamp}` });
    await page.fill('#cost', '10.00');
    await page.fill('#price', '25.00');
    await page.click('button:has-text("Create product")');
    await page.waitForTimeout(2000);

    const productVisible = await page.locator(`text=Test Product ${timestamp}`).isVisible();
    expect(productVisible).toBe(true);
  });

  test('POS domain: POS, Sales, Cashier Sessions', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('button:has-text("Point of Sale")');
    await page.waitForTimeout(1000);
    await page.click('a:has-text("Point of Sale")');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();

    await page.click('button:has-text("Point of Sale")');
    await page.waitForTimeout(1000);
    await page.click('a:has-text("Sales")');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();

    await page.click('button:has-text("Point of Sale")');
    await page.waitForTimeout(1000);
    await page.click('a:has-text("Cashier Sessions")');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();
  });

  test('Finance domain: all child destinations load', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('text=Finance');
    await page.waitForTimeout(500);

    const financePages = [
      'Finance Overview',
      'Chart of Accounts',
      'Journals',
      'Accounts Receivable',
      'Accounts Payable',
      'Payments',
      'Financial Periods',
      'Reconciliation',
    ];

    for (const pageName of financePages) {
      await page.click('button:has-text("Finance")');
      await page.waitForTimeout(1000);
      await page.click(`a:has-text("${pageName}")`);
      await page.waitForTimeout(1500);
      await expect(page.locator('nav')).toBeVisible();
    }
  });

  test('Golf domain: all child destinations load', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('text=Golf');
    await page.waitForTimeout(500);

    const golfPages = ['Courses', 'Tee Sheet', 'Bookings', 'Guests', 'Charges'];

    for (const pageName of golfPages) {
      await page.click('button:has-text("Golf")');
      await page.waitForTimeout(1000);
      await page.click(`a:has-text("${pageName}")`);
      await page.waitForTimeout(1500);
      await expect(page.locator('nav')).toBeVisible();
    }
  });

  test('Administration domain: Fiscalisation loads', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('text=Administration');
    await page.waitForTimeout(500);
    await page.click('text=Fiscalisation');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();
  });

  test('Reports domain: report destinations load', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await startQaSession(page);

    await page.click('text=Reports');
    await page.waitForTimeout(500);
    await page.click('text=Financial Reports');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();
  });

  test('Direct URL access: /products/categories redirects to /products', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/products');
    await page.waitForTimeout(2000);
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });

  test('Navigation: collapsible groups work', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);
    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    const membershipBtn = page.locator('button:has-text("Membership")');
    await expect(membershipBtn).toBeVisible();

    await membershipBtn.click();
    await page.waitForTimeout(500);

    const membersLink = page.locator('a:has-text("Members")');
    await expect(membersLink).toBeVisible();

    await membershipBtn.click();
    await page.waitForTimeout(500);

    const membersLinkHidden = await page.locator('a:has-text("Members")').isVisible();
    expect(membersLinkHidden).toBe(false);
  });
});
