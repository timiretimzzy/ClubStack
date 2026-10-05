import { test, expect } from '@playwright/test';

const UI = 'http://localhost:5173';

test('login flow in browser', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      errors.push(msg.text());
    }
  });
  page.on('pageerror', err => {
    errors.push('PAGEERROR: ' + err.message);
  });

  await page.goto(UI + '/');
  await page.waitForTimeout(1000);

  await page.fill('input[aria-label="User name"]', 'admin@localhost');
  await page.fill('input[aria-label="Password"]', 'GolfAdmin2026!');
  await page.click('button[type="submit"]');
  await page.waitForTimeout(3000);

  console.log('=== AFTER LOGIN ===');
  console.log('URL:', page.url());
  console.log('Title:', await page.title());

  const rootHtml = await page.locator('#root').innerHTML();
  console.log('Root HTML length:', rootHtml.length);
  console.log('Root HTML (first 1000):', rootHtml.substring(0, 1000));

  if (errors.length > 0) {
    console.log('=== CONSOLE ERRORS ===');
    for (const err of errors) {
      console.log(err);
    }
  } else {
    console.log('No console errors');
  }
  console.log('=== END ===');
});
