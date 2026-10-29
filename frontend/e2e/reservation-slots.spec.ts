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

  // Slots are created per hospital — pick the seeded demo hospital first.
  await page.getByLabel('Nemocnice').selectOption({ index: 1 });

  // Generation panel defaults: today, 09:00–16:00, 30-minute slots.
  const submit = page.getByRole('button', { name: /vygenerovat|vytvořit|create/i }).first();
  await submit.click();

  // The generated slots replace the empty-state row in the day table.
  await expect(
    page.locator('.slots-table tbody tr:not(:has(.empty-row))').first()
  ).toBeVisible({ timeout: 15_000 });
});
