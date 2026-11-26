import { expect, test } from '@playwright/test';
import { apiToken, findCustomer, signIn } from './support';

/** "Overwrite with mine": refetch the current ETag, then resubmit the user's values (a deliberate last-writer-wins). */
test('customers: Overwrite with mine wins after a 412', async ({ page, request }) => {
  const token = await apiToken(request);
  const seed = await findCustomer(request, token, 'Sample Customer 08');
  const stamp = Date.now().toString().slice(-6);

  await signIn(page);
  await page.getByPlaceholder('Search name or email').fill(seed.name);
  await page.getByRole('button', { name: seed.name, exact: true }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer.getByLabel('Name')).toHaveValue(/Sample Customer 08/);
  await drawer.getByLabel('Name').fill(`Sample Customer 08 (mine ${stamp})`);

  const current = await request.get(`/api/v1/customers/${seed.id}`, { headers: { Authorization: `Bearer ${token}` } });
  await request.put(`/api/v1/customers/${seed.id}`, {
    headers: { Authorization: `Bearer ${token}`, 'If-Match': current.headers()['etag'] },
    data: { name: `Sample Customer 08 (theirs ${stamp})`, email: seed.email, phone: seed.phone },
  });

  await drawer.getByRole('button', { name: 'Save changes' }).click();
  await page.getByTestId('conflict-banner').getByRole('button', { name: 'Overwrite with mine' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Changes saved' })).toBeVisible();

  const after = await request.get(`/api/v1/customers/${seed.id}`, { headers: { Authorization: `Bearer ${token}` } });
  expect((await after.json()).name).toBe(`Sample Customer 08 (mine ${stamp})`);

  // Put the seed name back so the demo data stays tidy.
  await request.put(`/api/v1/customers/${seed.id}`, {
    headers: { Authorization: `Bearer ${token}`, 'If-Match': after.headers()['etag'] },
    data: { name: 'Sample Customer 08', email: seed.email, phone: seed.phone },
  });
});

/** A refused DELETE is replayed as a delete ("Delete anyway"), never turned into a save of the form. */
test('customers: a delete refused with 412 is retried as a delete', async ({ page, request }) => {
  const token = await apiToken(request);
  const auth = { Authorization: `Bearer ${token}` };
  const stamp = Date.now().toString().slice(-6);
  const created = await request.post('/api/v1/customers', {
    headers: auth,
    data: { name: `Delete Me ${stamp}`, email: `delete.${stamp}@example.test`, phone: null },
  });
  expect(created.status()).toBe(201);
  const c = await created.json();

  await signIn(page);
  await page.getByPlaceholder('Search name or email').fill(c.name);
  await page.getByRole('button', { name: c.name, exact: true }).click();
  const drawer = page.getByRole('dialog').filter({ hasText: 'Edit customer' });
  await expect(drawer.getByLabel('Name')).toHaveValue(c.name);

  await request.put(`/api/v1/customers/${c.id}`, {
    headers: { ...auth, 'If-Match': created.headers()['etag'] },
    data: { name: `${c.name} (renamed)`, email: c.email, phone: null },
  });

  await drawer.getByRole('button', { name: 'Delete', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Delete customer' }).click();
  const banner = page.getByTestId('conflict-banner');
  await expect(banner).toContainText('Nothing was changed');
  await banner.getByRole('button', { name: 'Delete anyway' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Customer deleted' })).toBeVisible();

  const gone = await request.get(`/api/v1/customers/${c.id}`, { headers: auth });
  expect(gone.status()).toBe(404);
});
