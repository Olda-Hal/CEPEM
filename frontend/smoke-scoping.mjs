/* Ad-hoc scoping UI smoke: employees page + edit/create modals. Deleted after use. */
import { chromium } from 'playwright';
import fs from 'node:fs';

const BASE = 'http://localhost:3000';
const OUT = '/tmp/cepem-shots-2';
fs.mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ executablePath: '/usr/bin/chromium', headless: true, args: ['--no-sandbox'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'cs-CZ' });
const page = await context.newPage();

await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
await page.fill('input[type="email"]', 'claude@test.test');
await page.fill('input[type="password"]', 'claude1claude');
await page.click('button.login-button');
await page.waitForURL((url) => !url.pathname.includes('/login'), { timeout: 15000 });
await page.waitForTimeout(1000);

await page.goto(`${BASE}/admin/employees`, { waitUntil: 'networkidle' });
await page.waitForTimeout(1200);
await page.screenshot({ path: `${OUT}/10-employees.png` });

// edit modal of the signed-in user row (test frontend / claude@test.test)
const row = page.locator('tr', { hasText: 'claude@test.test' });
const editBtn = row.locator('button', { hasText: 'Upravit' }).first();
if (await editBtn.count()) {
  await editBtn.click();
  await page.waitForTimeout(800);
  await page.screenshot({ path: `${OUT}/10b-edit-employee-modal.png`, fullPage: true });
  await page.keyboard.press('Escape');
  await page.locator('.close-button').first().click().catch(() => {});
  await page.waitForTimeout(300);
} else {
  console.log('EDIT BTN NOT FOUND');
}

const createBtn = page.locator('.page-heading-actions .btn-primary').first();
await createBtn.click();
await page.waitForTimeout(1000);
await page.screenshot({ path: `${OUT}/10c-create-employee-modal.png`, fullPage: true });

const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
console.log('DONE');

await browser.close();
