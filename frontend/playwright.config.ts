import { defineConfig } from '@playwright/test';

/**
 * E2E tests run against a live stack (frontend on localhost:3000 by
 * default). Credentials come from the environment so no secrets live
 * in the repo:
 *   E2E_BASE_URL  (default http://localhost:3000)
 *   E2E_EMAIL     (an account with patients.read, e.g. the admin)
 *   E2E_PASSWORD
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:3000',
    headless: true,
    locale: 'cs-CZ',
  },
  webServer: undefined,
});
