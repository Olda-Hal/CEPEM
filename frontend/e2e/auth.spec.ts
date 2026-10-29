import { test, expect } from '@playwright/test';

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;

/**
 * Shared precondition: a stack is running and E2E credentials are set.
 * Without credentials only the anonymous flows run.
 */
test.beforeEach(async ({ page }) => {
  if (!EMAIL || !PASSWORD) {
    test.skip(true, 'E2E_EMAIL / E2E_PASSWORD not set');
  }
});

test('login with valid credentials reaches the patients page', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(EMAIL!);
  await page.getByLabel(/heslo|password/i).fill(PASSWORD!);
  await page.getByRole('button', { name: /přihlásit|login/i }).click();

  await expect(page).toHaveURL(/patients/, { timeout: 15_000 });
});

test('login with a wrong password shows an error and stays on the page', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(EMAIL!);
  await page.getByLabel(/heslo|password/i).fill('definitely-wrong-password');
  await page.getByRole('button', { name: /přihlásit|login/i }).click();

  await expect(page.locator('.login-error, [class*="error"]')).toBeVisible({ timeout: 10_000 });
  await expect(page).toHaveURL(/login/);
});

test('unauthenticated access to protected pages redirects to login', async ({ page }) => {
  await page.goto('/patients');
  await expect(page).toHaveURL(/login/, { timeout: 10_000 });
});
