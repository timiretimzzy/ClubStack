import { test, expect } from '@playwright/test';

const UI = 'http://localhost:5173';

test('capture console errors', async ({ page }) => {
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
  await page.waitForTimeout(3000);

  console.log('=== CONSOLE ERRORS ===');
  if (errors.length === 0) {
    console.log('No console errors found');
  } else {
    for (const err of errors) {
      console.log(err);
    }
  }
  console.log('=== END ===');

  const title = await page.title();
  console.log('Page title:', title);

  const rootHtml = await page.locator('#root').innerHTML();
  console.log('Root HTML length:', rootHtml.length);
  console.log('Root HTML (first 500):', rootHtml.substring(0, 500));
});
