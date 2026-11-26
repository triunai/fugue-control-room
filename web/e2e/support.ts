import { APIRequestContext, Page, expect } from '@playwright/test';

/** Seeded Development-only password (runbook 05); override with E2E_PASSWORD. */
export const PASSWORD = process.env['E2E_PASSWORD'] ?? 'Fugue-Dev-Passw0rd!'; // secrets-scan:ignore (seeded dev-only password)
export const SHOTS = 'docs/screenshots';

export async function signIn(page: Page, email = 'admin@fugue.test'): Promise<void> {
  await page.goto('/sign-in');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).not.toHaveURL(/sign-in/);
}

/** A second client acting "elsewhere": its own token, straight to the API through the dev proxy. */
export async function apiToken(request: APIRequestContext, email = 'admin@fugue.test'): Promise<string> {
  const res = await request.post('/api/v1/auth/token', { data: { email, password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  return (await res.json()).accessToken as string;
}

export async function findCustomer(request: APIRequestContext, token: string, name: string) {
  const res = await request.get(`/api/v1/customers?limit=5&search=${encodeURIComponent(name)}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(res.ok()).toBeTruthy();
  const [customer] = (await res.json()) as { id: string; name: string; email: string; phone: string | null }[];
  expect(customer, `seeded customer "${name}" exists`).toBeTruthy();
  return customer;
}
