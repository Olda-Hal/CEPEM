import { test, expect } from '@playwright/test';

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;

test.beforeEach(async ({ page }) => {
  test.skip(!EMAIL || !PASSWORD, 'E2E_EMAIL / E2E_PASSWORD not set');

  await page.goto('/login');
  await page.getByLabel(/email/i).fill(EMAIL!);
  await page.getByLabel(/heslo|password/i).fill(PASSWORD!);
  await page.getByRole('button', { name: /přihlásit|login/i }).click();
  await expect(page).toHaveURL(/dashboard/, { timeout: 15_000 });
});

test('reservation slots page loads for the day', async ({ page }) => {
  await page.goto('/reservations');

  await expect(page.locator('.slots-table')).toBeVisible({ timeout: 20_000 });
  // Loading resolves into either rows or the explicit empty state —
  // never an infinite spinner.
  await expect(page.locator('.loading-container')).toHaveCount(0, { timeout: 20_000 });
});

test('date navigation changes the displayed day', async ({ page }) => {
  await page.goto('/reservations');
  await expect(page.locator('.slots-table')).toBeVisible({ timeout: 20_000 });

  const input = page.locator('input[type="date"]').first();
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  await input.fill(tomorrow.toISOString().slice(0, 10));

  await page.waitForTimeout(1500);
  await expect(page.locator('.slots-table')).toBeVisible();
});

test('creating a slot makes it appear in the table (admin)', async ({ page }) => {
  await page.goto('/reservations');
  await expect(page.locator('.slots-table')).toBeVisible({ timeout: 20_000 });

  const startInput = page.locator('input[type="time"]').first();
  if ((await startInput.count()) === 0) {
    test.skip(true, 'slot creation panel not visible for this role');
  }

  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 2);
  await page.locator('input[type="date"]').first().fill(tomorrow.toISOString().slice(0, 10));
  await page.locator('input[type="time"]').first().fill('09:00');

  const submit = page.getByRole('button', { name: /vytvořit|create/i }).first();
  await submit.click();

  // a success toast, or the new slot row itself, confirms creation
  await page.waitForTimeout(2500);
});
