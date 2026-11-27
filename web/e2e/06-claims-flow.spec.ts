import { expect, test } from '@playwright/test';
import { apiToken, signIn } from './support';

/** The whole life of one claim through the console, then the 412 banner: report, pick up, reserve, ask, answer, approve, settle. */
test('a claim from report to settlement, and the changed-since-you-opened-it banner', async ({ browser, request }) => {
  const claimant = await (await browser.newContext()).newPage();
  const staff = await (await browser.newContext()).newPage();
  const manager = await (await browser.newContext()).newPage();

  await signIn(claimant, 'self@fugue.test');
  await claimant.goto('/claims/new');
  await claimant.getByLabel('Market').selectOption('ID');
  await claimant.getByLabel('Line of cover').selectOption('Property');
  await claimant.getByLabel('Policy number').fill(`POL-FLOW-${Date.now()}`);
  await claimant.getByLabel('Incident date').fill(new Date(Date.now() - 86_400_000).toISOString().slice(0, 10));
  await claimant.getByLabel('What happened').fill('A pipe burst and flooded the ground floor.');
  await claimant.getByRole('button', { name: 'Submit claim' }).click();
  await expect(claimant.getByRole('heading', { level: 1 })).toHaveText(/^CLM-ID-/);
  const claimUrl = claimant.url();

  // Officer: pick up, set a reserve, ask a question.
  await signIn(staff, 'staff@fugue.test');
  await staff.goto(claimUrl.replace('http://localhost:4200', ''));
  await staff.getByRole('button', { name: 'Pick up' }).click();
  await expect(staff.getByText('Claim picked up')).toBeVisible();
  await expect(staff.getByRole('button', { name: 'Approve' })).toBeDisabled(); // no reserve yet
  await staff.getByRole('button', { name: 'Set reserve' }).click();
  await staff.getByLabel(/^Amount/).fill('2,500.00');
  await staff.getByRole('dialog').getByRole('button', { name: 'Set reserve' }).click();
  await expect(staff.getByTestId('handling')).toContainText('IDR 2,500.00');
  await staff.getByRole('button', { name: 'Request info' }).click();
  await staff.getByLabel('Your question').fill('Please send photos of the damage.');
  await staff.getByRole('button', { name: 'Send question' }).click();
  await expect(staff.getByText('Waiting on the claimant')).toBeVisible();

  // Claimant: answers; the reserve is nowhere on their page.
  await claimant.reload();
  await expect(claimant.getByText('Please send photos of the damage.').first()).toBeVisible();
  await claimant.getByLabel('Your answer').fill('Photos are in the shared folder.');
  await claimant.getByRole('button', { name: 'Send answer' }).click();
  await expect(claimant.getByText('Answer sent')).toBeVisible();
  await expect(claimant.getByText('An officer is reviewing your claim')).toBeVisible();
  await expect(claimant.locator('body')).not.toContainText('2,500.00');

  // 412: the officer's page is now stale because the manager changes the claim through the API first.
  await staff.reload();
  await expect(staff.getByRole('button', { name: 'Set reserve' })).toBeVisible();
  const token = await apiToken(request, 'manager@fugue.test');
  const id = claimUrl.split('/').pop()!;
  const fresh = await request.get(`/api/v1/claims/${id}`, { headers: { Authorization: `Bearer ${token}` } });
  const put = await request.put(`/api/v1/claims/${id}/reserve`, {
    headers: { Authorization: `Bearer ${token}`, 'If-Match': fresh.headers()['etag'] },
    data: { amountMinor: 300000 },
  });
  expect(put.ok()).toBeTruthy();
  await staff.getByRole('button', { name: 'Set reserve' }).click();
  await staff.getByLabel(/^Amount/).fill('2,600.00');
  await staff.getByRole('dialog').getByRole('button', { name: 'Set reserve' }).click();
  await expect(staff.getByTestId('conflict-banner')).toContainText('Changed since you opened it');
  await staff.getByRole('button', { name: 'Reload the claim' }).click();
  await expect(staff.getByTestId('handling')).toContainText('IDR 3,000.00');

  // Approve over the reserve is refused on the form; within it is accepted.
  await staff.getByRole('button', { name: 'Approve' }).click();
  await staff.getByLabel(/^Amount/).fill('3,500.00');
  await staff.getByLabel('Reason').fill('Covered.');
  await staff.getByRole('dialog').getByRole('button', { name: 'Approve' }).click();
  await expect(staff.getByRole('dialog')).toContainText('more than the reserve');
  await staff.getByLabel(/^Amount/).fill('2,800.00');
  await staff.getByRole('dialog').getByRole('button', { name: 'Approve' }).click();
  await expect(staff.getByText('Decision recorded')).toBeVisible();

  // The claimant sees the decision; the manager settles.
  await claimant.reload();
  await expect(claimant.getByText('Your claim is approved')).toBeVisible();
  await signIn(manager, 'manager@fugue.test');
  await manager.goto(`/claims/${id}`);
  await manager.getByRole('button', { name: 'Settle' }).click();
  await manager.getByRole('alertdialog').getByRole('button', { name: 'Settle claim' }).click();
  await expect(manager.getByText('Claim settled')).toBeVisible();
  await claimant.reload();
  await expect(claimant.getByText('Your claim is settled and closed')).toBeVisible();
});
