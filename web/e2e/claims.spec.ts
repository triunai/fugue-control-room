import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * Claims smoke against the REAL API (seeded Development data): a claimant reports an incident and finds it in My claims;
 * an officer finds it in the queue and picks it up; the claimant then sees the new status and timeline entry.
 * The sign-in limiter allows 20 a minute per IP: start the API with RateLimiting__Auth__PerEmailPermits=60 if you repeat runs.
 */
test('claimant reports, staff picks up, claimant sees it move', async ({ browser }) => {
  const policy = `POL-E2E-${Date.now()}`;
  const claimant = await (await browser.newContext()).newPage();
  const staff = await (await browser.newContext()).newPage();

  // Claimant: report an incident.
  await signIn(claimant, 'self@fugue.test');
  await claimant.goto('/claims/new');
  await claimant.getByRole('combobox', { name: 'Market' }).click();
  await claimant.getByRole('option', { name: /^Singapore/ }).click();
  await expect(claimant.getByLabel('Currency')).toHaveValue('SGD');
  await claimant.getByRole('combobox', { name: 'Line of cover' }).click();
  await claimant.getByRole('option', { name: /^Motor/ }).click();
  await claimant.getByLabel('Policy number').fill(policy);
  const yesterday = new Date(Date.now() - 86_400_000);
  await claimant.getByLabel('Incident date').fill(yesterday.toISOString().slice(0, 10));
  await claimant.getByLabel('What happened').fill('Rear-ended at the traffic lights on the way to work.');
  await claimant.getByRole('button', { name: 'Submit claim' }).click();
  const heading = claimant.getByRole('heading', { level: 1 });
  await expect(heading).toHaveText(/^CLM-SG-\d{4}-\d+$/);
  const number = (await heading.textContent())!.trim();
  await expect(claimant.getByTestId('next-instruction')).toHaveText('We have your claim.');

  // It is in My claims.
  await claimant.getByRole('link', { name: 'My claims' }).first().click();
  await expect(claimant.getByRole('link', { name: number })).toBeVisible();

  // Staff: find it by number and pick it up. Search is indexed after the claim is written, so retry on a fresh load.
  await signIn(staff, 'staff@fugue.test');
  await expect(staff).toHaveURL(/claims\/queue/);
  await expect(async () => {
    await staff.reload();
    await staff.getByPlaceholder('Search by claim number, policy, place or description').fill(number);
    await expect(staff.getByRole('link', { name: number })).toBeVisible({ timeout: 2_500 });
  }).toPass({ timeout: 30_000 });
  await staff.getByRole('link', { name: number }).click();
  await expect(staff.getByRole('heading', { level: 1 })).toHaveText(number);
  await staff.getByRole('button', { name: 'Pick up' }).click();
  await expect(staff.getByText('Claim picked up')).toBeVisible();
  await expect(staff.getByTestId('handling')).toContainText('Dev Staff');
  await expect(staff.getByRole('button', { name: 'Set reserve' })).toBeVisible();

  // Claimant: the claim moved, in plain words.
  await claimant.getByRole('link', { name: number }).click();
  await expect(claimant.getByTestId('next-instruction')).toHaveText('An officer is reviewing your claim.');
  await expect(claimant.getByTestId('timeline')).toContainText('A claims officer has started reviewing your claim.');
});

test('a claimant cannot reach staff screens', async ({ page }) => {
  await signIn(page, 'self@fugue.test');
  await expect(page.getByRole('link', { name: 'Claims queue' })).toHaveCount(0);
  await page.goto('/claims/queue');
  await expect(page).toHaveURL(/unavailable/);
});
