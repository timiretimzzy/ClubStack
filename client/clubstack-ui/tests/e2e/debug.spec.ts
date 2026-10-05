import { test, expect } from '@playwright/test';

const BASE = 'http://localhost:5000';
const UI = 'http://localhost:5173';

test('check page content', async ({ page }) => {
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

  await page.goto(UI + '/categories');
  await page.waitForTimeout(2000);
  const content = await page.content();
  console.log('CATEGORIES PAGE CONTENT (first 2000 chars):');
  console.log(content.substring(0, 2000));
  console.log('---END---');
});
