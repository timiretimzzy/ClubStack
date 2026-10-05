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

async function openPanel(page: import('@playwright/test').Page) {
  const toggle = page.locator('.qa-recorder-toggle');
  if (await toggle.isVisible()) {
    await toggle.click();
    await page.waitForTimeout(500);
  }
}

async function startSession(page: import('@playwright/test').Page) {
  await openPanel(page);
  await page.click('text=Start QA Session');
  await page.waitForTimeout(500);
}

test.describe('QA Recorder', () => {
  test('start session and verify session ID', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    const sessionText = await page.locator('.qa-recorder-row').first().textContent();
    expect(sessionText).toContain('QA-SESSION-');
  });

  test('click navigation item and verify action recorded', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    await page.click('text=Categories');
    await page.waitForTimeout(2000);

    await openPanel(page);
    const actionCount = await page.locator('.qa-recorder-row').nth(1).textContent();
    expect(actionCount).not.toContain('0');
  });

  test('trigger successful API request and verify correlation', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    await page.goto(UI + '/categories');
    await page.waitForTimeout(3000);

    await openPanel(page);
    const lastApiStatus = await page.locator('.qa-recorder-row').nth(5).textContent();
    expect(lastApiStatus).not.toContain('—');
  });

  test('pause and resume recording', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    await page.click('text=Pause');
    await page.waitForTimeout(500);

    const pauseBtn = await page.locator('.qa-recorder-controls button').first().textContent();
    expect(pauseBtn).toContain('Resume');

    await page.click('text=Resume');
    await page.waitForTimeout(500);

    const resumeBtn = await page.locator('.qa-recorder-controls button').first().textContent();
    expect(resumeBtn).toContain('Pause');
  });

  test('stop session and verify summary', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    await page.goto(UI + '/categories');
    await page.waitForTimeout(2000);

    await openPanel(page);
    await page.click('text=Stop & Save');
    await page.waitForTimeout(1000);

    const sessionText = await page.locator('.qa-recorder-row').first().textContent();
    expect(sessionText).toContain('Not started');
  });

  test('clear session', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    await page.click('text=Clear');
    await page.waitForTimeout(500);

    const sessionText = await page.locator('.qa-recorder-row').first().textContent();
    expect(sessionText).toContain('Not started');
  });

  test('verify recorder does not interfere with normal operation', async ({ page }) => {
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await login(page);
    await page.goto(UI + '/');
    await page.waitForTimeout(1000);

    await startSession(page);

    await page.click('text=Products');
    await page.waitForTimeout(2000);

    const nav = page.locator('nav');
    await expect(nav).toBeVisible();

    const pageTitle = await page.locator('.page-title').textContent();
    expect(pageTitle).toContain('Products');
  });
});
