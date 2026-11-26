import { expect, test } from '@playwright/test';
import { apiToken, findCustomer, SHOTS, signIn } from './support';

/**
 * The demo path: sign in as admin, open a customer, have someone else save it meanwhile (a 412 on our save),
 * see the banner, reload their version, save again.
 */
test('customers: a concurrent edit is caught by If-Match, explained, reloaded and saved', async ({ page, request }) => {
  const token = await apiToken(request);
  const seed = await findCustomer(request, token, 'Sample Customer 07');
  const stamp = Date.now().toString().slice(-6);

  await signIn(page);
  await expect(page.getByRole('heading', { name: 'Customers', level: 1 })).toBeVisible();

  await page.getByPlaceholder('Search name or email').fill(seed.name);
  await page.getByRole('button', { name: seed.name, exact: true }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer.getByLabel('Name')).toHaveValue(/Sample Customer 07/);
  await drawer.getByLabel('Name').fill(`Sample Customer 07 (mine ${stamp})`);
  await page.screenshot({ path: `${SHOTS}/customers-drawer.png` });

  // Someone else saves first, with the current ETag.
  const current = await request.get(`/api/v1/customers/${seed.id}`, { headers: { Authorization: `Bearer ${token}` } });
  const etag = current.headers()['etag'];
  const theirs = `Sample Customer 07 (theirs ${stamp})`;
  const put = await request.put(`/api/v1/customers/${seed.id}`, {
    headers: { Authorization: `Bearer ${token}`, 'If-Match': etag },
    data: { name: theirs, email: seed.email, phone: seed.phone },
  });
  expect(put.status()).toBe(204);

  // Our save goes out with the stale tag: 412, and the banner explains it.
  await drawer.getByRole('button', { name: 'Save changes' }).click();
  const banner = page.getByTestId('conflict-banner');
  await expect(banner).toContainText('Changed since you opened it');
  await expect(banner).toContainText(theirs);
  await page.screenshot({ path: `${SHOTS}/customers-412-banner.png` });

  await banner.getByRole('button', { name: 'Reload their version' }).click();
  await expect(drawer.getByLabel('Name')).toHaveValue(theirs);

  const final = `Sample Customer 07`;
  await drawer.getByLabel('Name').fill(final);
  await drawer.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Changes saved' })).toBeVisible();
  await expect(drawer).toBeHidden();

  const after = await request.get(`/api/v1/customers/${seed.id}`, { headers: { Authorization: `Bearer ${token}` } });
  expect((await after.json()).name).toBe(final);
});
