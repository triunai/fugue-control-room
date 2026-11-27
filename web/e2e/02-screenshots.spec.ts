import { expect, test } from '@playwright/test';
import { apiToken, findCustomer, SHOTS, signIn } from './support';

/** Reference screenshots for docs and review. Runs after 01-smoke.spec.ts (it leaves a fresh audit diff behind). */
test.describe('screenshots', () => {
  test('sign-in', async ({ page }) => {
    await page.goto('/sign-in');
    await page.waitForTimeout(2400); // let the three voices finish entering
    await page.screenshot({ path: `${SHOTS}/sign-in.png` });
  });

  test('customers, audit diff, modules, webhooks dev sink', async ({ page, request }) => {
    await signIn(page);
    await expect(page.getByRole('table')).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/customers.png` });

    const token = await apiToken(request);
    const seed = await findCustomer(request, token, 'Sample Customer 07');
    await page.goto(`/audit?entityId=${seed.id}`);
    await expect(page.getByRole('heading', { name: 'Field changes' })).toBeVisible();
    const update = page.locator('button.entry', { hasText: 'customers.update' }).first();
    await update.click();
    await page.screenshot({ path: `${SHOTS}/audit-diff.png` });

    await page.getByRole('link', { name: 'Modules' }).click();
    await expect(page.getByRole('switch', { name: 'Switch Customers' })).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/modules.png` });

    await page.goto('/webhooks?tab=sink');
    const connect = page.getByRole('button', { name: /Connect the sink|Reconnect/ });
    await connect.click();
    await expect(page.getByRole('status').filter({ hasText: /Sink (re)?connected/ })).toBeVisible();
    await page.getByRole('button', { name: 'Create a test customer' }).click();
    await expect(page.locator('li.event').first()).toContainText('Signature verified', { timeout: 20_000 });
    await page.waitForTimeout(600);
    await page.screenshot({ path: `${SHOTS}/webhooks-dev-sink.png` });
  });
});
