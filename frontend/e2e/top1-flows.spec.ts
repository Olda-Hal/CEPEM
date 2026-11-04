import { test, expect, Page, APIRequestContext } from '@playwright/test';

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;
const NEW_PASSWORD = 'E2ePass123!';
const FINAL_PASSWORD = 'E2ePass456!';

const login = async (page: Page, email: string, password: string) => {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/heslo|password/i).fill(password);
  await page.getByRole('button', { name: /přihlásit|sign in|login/i }).click();
};

const authHeaders = async (page: Page) => {
  const token = await page.evaluate(() => localStorage.getItem('authToken'));
  expect(token).toBeTruthy();
  return { Authorization: `Bearer ${token}` };
};

const createEmployeeWithRole = async (
  request: APIRequestContext,
  adminAuthorization: string,
  roleName: string,
  countryCode: string,
  hospitalIds: number[],
  suffix: string
) => {
  const rolesResponse = await request.get('/api/admin/roles', {
    headers: { Authorization: adminAuthorization }
  });
  expect(
    rolesResponse.ok(),
    `${rolesResponse.status()} ${await rolesResponse.text()}`
  ).toBeTruthy();
  const roles = (await rolesResponse.json()) as Array<{ id: number; name: string }>;
  const role = roles.find((entry) => entry.name === roleName);
  expect(role, `seeded ${roleName} role`).toBeTruthy();

  const email = `e2e-${roleName.toLowerCase().replace(/[^a-z]/g, '')}-${countryCode.toLowerCase()}-${suffix}@cepem.local`;
  const response = await request.post('/api/auth/create-employee', {
    headers: { Authorization: adminAuthorization },
    data: {
      firstName: 'E2E',
      lastName: `Admin ${countryCode}`,
      email,
      phoneNumber: '+420111222333',
      UID: `E2E-${countryCode}-${suffix}`,
      gender: 'F',
      password: NEW_PASSWORD,
      active: true,
      roleIds: [role!.id],
      countryCode,
      hospitalIds
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();

  // New accounts must change the first-login password before any business
  // endpoint is available. Change it once so the scoped-admin checks can run.
  const firstLogin = await request.post('/api/auth/login', {
    data: { email, password: NEW_PASSWORD }
  });
  expect(firstLogin.ok(), await firstLogin.text()).toBeTruthy();
  const firstSession = (await firstLogin.json()) as { token: string };

  const changeResponse = await request.post('/api/auth/change-password', {
    headers: { Authorization: `Bearer ${firstSession.token}` },
    data: {
      currentPassword: NEW_PASSWORD,
      newPassword: FINAL_PASSWORD,
      confirmPassword: FINAL_PASSWORD
    }
  });
  expect(changeResponse.ok(), await changeResponse.text()).toBeTruthy();

  return email;
};

test.describe('TOP 1 flows', () => {
  test.skip(!EMAIL || !PASSWORD, 'E2E_EMAIL / E2E_PASSWORD not set');

  test('country admin only sees employees from their own country', async ({ page, request }) => {
    await login(page, EMAIL!, PASSWORD!);
    await expect(page).toHaveURL(/patients/, { timeout: 15_000 });

    const headers = await authHeaders(page);
    const suffix = Date.now().toString();
    const czEmail = await createEmployeeWithRole(
      request,
      headers.Authorization,
      'Country Admin',
      'CZ',
      [],
      suffix
    );
    const nlEmail = await createEmployeeWithRole(
      request,
      headers.Authorization,
      'Country Admin',
      'NL',
      [],
      suffix
    );

    const loginResponse = await request.post('/api/auth/login', {
      data: { email: czEmail, password: FINAL_PASSWORD }
    });
    expect(loginResponse.ok(), await loginResponse.text()).toBeTruthy();
    const czSession = (await loginResponse.json()) as { token: string };

    const employeesResponse = await request.get('/api/admin/employees', {
      headers: { Authorization: `Bearer ${czSession.token}` }
    });
    expect(employeesResponse.ok()).toBeTruthy();
    const employees = (await employeesResponse.json()) as Array<{ email: string }>;
    const emails = employees.map((employee) => employee.email);

    expect(emails).toContain(czEmail);
    expect(emails).not.toContain(nlEmail);

    // The menu entry is usable for a scoped admin and the page renders.
    await page.evaluate(() => localStorage.clear());
    await login(page, czEmail, FINAL_PASSWORD);
    await page.goto('/admin/employees');
    await expect(page.locator('table.data-table')).toBeVisible({ timeout: 20_000 });
  });

  test('admin can create a patient with an intake and start a visit', async ({ page, request }) => {
    await page.addInitScript(() => localStorage.setItem('preferred-language', 'en'));
    await login(page, EMAIL!, PASSWORD!);
    await expect(page).toHaveURL(/patients/, { timeout: 15_000 });
    const headers = await authHeaders(page);

    const hospitalsResponse = await request.get('/api/hospitals', { headers });
    expect(hospitalsResponse.ok(), await hospitalsResponse.text()).toBeTruthy();
    const hospitals = (await hospitalsResponse.json()) as Array<{ id: number }>;
    expect(hospitals.length).toBeGreaterThan(0);

    const digitiserEmail = await createEmployeeWithRole(
      request,
      headers.Authorization,
      'Digitiser',
      'CZ',
      [hospitals[0]!.id],
      `digitiser-${Date.now()}`
    );

    // A GDPR wording must be published before the intake accepts consent.
    const gdprVersion = `e2e-${Date.now()}`;
    const gdprResponse = await request.post('/api/gdpr/consent-templates', {
      headers,
      data: {
        consentType: 'gdpr',
        version: gdprVersion,
        language: 'en',
        title: 'E2E GDPR',
        text: 'E2E consent wording.',
        isActive: true
      }
    });
    expect([200, 409]).toContain(gdprResponse.status());

    // Creating a patient requires an account assigned to a center, so use
    // the freshly created Digitiser instead of the center-less SysAdmin.
    await page.evaluate(() => localStorage.clear());
    await login(page, digitiserEmail, FINAL_PASSWORD);
    await expect(page).toHaveURL(/patients/, { timeout: 15_000 });
    const digitiserHeaders = await authHeaders(page);

    await page.goto('/patients/add');
    const lastName = `E2e${Date.now()}`;

    await page
      .locator('.form-field')
      .filter({ has: page.locator('label', { hasText: /^Last name/ }) })
      .locator('input')
      .fill(lastName);
    await page
      .locator('.form-field')
      .filter({ has: page.locator('label', { hasText: /^First name/ }) })
      .locator('input')
      .fill('Patient');
    await page.locator('input[type="date"]').first().fill('1990-01-15');

    await page.getByLabel(/I confirm that the information provided/i).check();
    await page.getByLabel(/I agree to the CPM terms/i).check();
    await page.getByLabel(/I agree to the processing of my personal data/i).check();

    await page.getByRole('button', { name: /create patient/i }).click();

    await expect(page).toHaveURL(/\/patients\/\d+/, { timeout: 30_000 });
    const patientId = Number(page.url().match(/\/patients\/(\d+)/)?.[1]);
    expect(patientId).toBeGreaterThan(0);
    await expect(page.locator('.patient-detail-page')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(lastName).first()).toBeVisible();

    // The atomic create endpoint returns the patient and its intake.
    const detailResponse = await request.get(`/api/patients/${patientId}`, {
      headers: digitiserHeaders
    });
    expect(detailResponse.ok()).toBeTruthy();
    const detail = (await detailResponse.json()) as {
      formSubmission?: { id: number };
    };
    expect(detail.formSubmission?.id).toBeGreaterThan(0);

    // Visits start from the new patient and are listed back by the API.
    const visitResponse = await request.post(`/api/patients/${patientId}/visits`, {
      headers: digitiserHeaders,
      data: { visitDate: new Date().toISOString(), note: 'E2E visit' }
    });
    expect(visitResponse.ok(), await visitResponse.text()).toBeTruthy();

    const visitsResponse = await request.get(`/api/patients/${patientId}/visits`, {
      headers: digitiserHeaders
    });
    expect(visitsResponse.ok()).toBeTruthy();
    const visits = (await visitsResponse.json()) as Array<{ note?: string }>;
    expect(visits.some((visit) => visit.note === 'E2E visit')).toBeTruthy();
  });
});
