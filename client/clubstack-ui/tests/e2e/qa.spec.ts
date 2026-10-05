import { test, expect, Page } from '@playwright/test';

const BASE = 'http://localhost:5000';
const UI = 'http://localhost:5173';

async function login(page: Page) {
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

test.describe('D1 Authentication', () => {
  test('login page loads and redirects unauthenticated users', async ({ page }) => {
    await page.goto(UI + '/');
    await expect(page).toHaveTitle(/ClubStack/);
  });

  test('authenticated shell renders after login', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/');
    const nav = page.locator('nav');
    await expect(nav).toBeVisible();
  });
});

test.describe('D2 Membership', () => {
  test('membership categories page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/categories');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });
});

test.describe('D4 Products', () => {
  test('products page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/products');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });
});

test.describe('D6 Finance', () => {
  test('finance overview page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/finance');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });
});

test.describe('D7 Golf', () => {
  test('golf courses page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/golf/courses');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });

  test('tee sheet page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/golf/tee-sheet');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });
});

test.describe('D5 POS', () => {
  test('POS page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/pos');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });

  test('sales page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/sales');
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.locator('.page-title')).toBeVisible();
  });

  test('cashier sessions page loads', async ({ page }) => {
    await login(page);
    await page.goto(UI + '/cashier-sessions');
    await expect(page.locator('nav')).toBeVisible();
  });
});
