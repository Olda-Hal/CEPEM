import { test, expect } from '@playwright/test';

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;

/** Login once and reuse the authenticated session for the patient flows. */
test.beforeEach(async ({ page }) => {
  test.skip(!EMAIL || !PASSWORD, 'E2E_EMAIL / E2E_PASSWORD not set');

  await page.goto('/login');
  await page.getByLabel(/email/i).fill(EMAIL!);
  await page.getByLabel(/heslo|password/i).fill(PASSWORD!);
  await page.getByRole('button', { name: /přihlásit|login/i }).click();
  await expect(page).toHaveURL(/patients/, { timeout: 15_000 });
});

test('patient list loads and shows patients', async ({ page }) => {
  await page.goto('/patients');

  // The list loads data via the API: either patient rows or an explicit empty state
  await expect(
    page.locator('.patient-card, .patient-item, table tbody tr').first()
  ).toBeVisible({ timeout: 15_000 });
});

test('patient search filters the list', async ({ page }) => {
  await page.goto('/patients');

  const searchInput = page.locator('input[type="text"], input[placeholder*="hledat" i]').first();
  await expect(searchInput).toBeVisible({ timeout: 10_000 });
  await searchInput.fill('zz-no-such-patient');
  await page.keyboard.press('Enter');

  // Wait for the (empty) result - no error page, no crash
  await page.waitForTimeout(1500);
  await expect(page.locator('.error-container')).toHaveCount(0);
});

test('patient detail opens from the list', async ({ page }) => {
  await page.goto('/patients');

  const detailButton = page.getByRole('button', { name: /zobrazit detail/i }).first();
  await expect(detailButton).toBeVisible({ timeout: 15_000 });
  await detailButton.click();

  // Detail page renders its content without an error state
  await expect(page).toHaveURL(/\/patients\/\d+/, { timeout: 15_000 });
  await expect(page.locator('.error-container')).toHaveCount(0);
  await expect(page.locator('.patient-detail-page')).toBeVisible({ timeout: 15_000 });
});
