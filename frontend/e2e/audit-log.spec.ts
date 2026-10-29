import { test, expect } from '@playwright/test';

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;

test.beforeEach(async ({ page }) => {
  test.skip(!EMAIL || !PASSWORD, 'E2E_EMAIL / E2E_PASSWORD not set');

  await page.goto('/login');
  await page.getByLabel(/email/i).fill(EMAIL!);
  await page.getByLabel(/heslo|password/i).fill(PASSWORD!);
  await page.getByRole('button', { name: /přihlásit|login/i }).click();
  await expect(page).toHaveURL(/patients/, { timeout: 15_000 });
});

test('audit log page loads with filters', async ({ page }) => {
  await page.goto('/admin/activity-log');

  await expect(page.locator('table.data-table')).toBeVisible({ timeout: 20_000 });
  // method + status filter selects are present
  await expect(page.locator('.activity-log-filters select')).toHaveCount(2);
});

test('method filter narrows the request', async ({ page }) => {
  await page.goto('/admin/activity-log');
  await expect(page.locator('table.data-table')).toBeVisible({ timeout: 20_000 });

  const requestPromise = page.waitForRequest(req => req.url().includes('/api/activitylogs') && req.url().includes('method='));
  await page.locator('.activity-log-filters select').first().selectOption('GET');
  await requestPromise;
});

test('clicking a row opens the detail modal', async ({ page }) => {
  await page.goto('/admin/activity-log');

  const firstRow = page.locator('table.data-table tbody tr').first();
  await expect(firstRow).toBeVisible({ timeout: 20_000 });
  await firstRow.click();

  await expect(page.locator('.modal-overlay')).toBeVisible({ timeout: 10_000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('.modal-overlay')).toHaveCount(0);
});

test('language switch changes login page text', async ({ page }) => {
  // Auth token lives in localStorage (not cookies), so clear both before
  // asserting that the login page renders signed out.
  await page.context().clearCookies();
  await page.goto('/login');
  await page.evaluate(() => localStorage.clear());
  await page.goto('/login');

  const select = page.locator('select').first();
  await expect(select).toBeVisible({ timeout: 10_000 });
  await select.selectOption('en');

  await expect(page.locator('button.login-button')).toHaveText(/sign in|log in/i, { timeout: 10_000 });
});

test('invalid intake link shows a controlled error, not a crash', async ({ page }) => {
  await page.context().clearCookies();
  await page.goto('/intake/definitely-not-a-real-token');

  // The page must not white-screen: either an error message or the
  // (expired/invalid-link) notice becomes visible.
  await page.waitForTimeout(3000);
  await expect(page.locator('body')).not.toBeEmpty();
  await expect(
    page.locator('.error-message, .intake-error, [class*="error"]').first()
  ).toBeVisible({ timeout: 15_000 });
});
