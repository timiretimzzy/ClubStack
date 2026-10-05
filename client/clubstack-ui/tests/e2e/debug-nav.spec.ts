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

test('debug nav group expansion', async ({ page }) => {
  await page.goto(UI + '/');
  await page.waitForTimeout(1000);
  await login(page);
  await page.goto(UI + '/');
  await page.waitForTimeout(2000);

  const navHtml = await page.locator('nav').innerHTML();
  console.log('=== NAV HTML ===');
  console.log(navHtml.substring(0, 3000));
  console.log('=== END ===');

  const groupButtons = await page.locator('.nav-group-toggle').all();
  console.log(`Found ${groupButtons.length} group toggle buttons`);
  for (const btn of groupButtons) {
    const text = await btn.textContent();
    console.log(`  Group: ${text}`);
  }

  const membershipBtn = page.locator('button:has-text("Membership")');
  const isVisible = await membershipBtn.isVisible();
  console.log(`Membership button visible: ${isVisible}`);

  if (isVisible) {
    await membershipBtn.click();
    await page.waitForTimeout(1000);

    const navHtmlAfter = await page.locator('nav').innerHTML();
    console.log('=== NAV HTML AFTER CLICK ===');
    console.log(navHtmlAfter.substring(0, 3000));
    console.log('=== END ===');

    const membersLink = page.locator('a:has-text("Members")');
    const membersVisible = await membersLink.isVisible();
    console.log(`Members link visible after expand: ${membersVisible}`);
  }
});
