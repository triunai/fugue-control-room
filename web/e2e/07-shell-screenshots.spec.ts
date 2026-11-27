import { expect, Page, test } from '@playwright/test';
import { SHOTS, signIn } from './support';

/** Shell, reviewer tour and roles matrix: reference screenshots, plus the checks that the shell layout holds. */

async function startTour(page: Page): Promise<void> {
  await page.locator('.acct').click();
  await page.getByRole('menuitem', { name: 'Take the tour' }).click();
  await expect(page.locator('.driver-popover-title')).toBeVisible();
}

/** Press Next until the popover shows `title`. */
async function tourTo(page: Page, title: string): Promise<void> {
  for (let i = 0; i < 8; i++) {
    if ((await page.locator('.driver-popover-title').textContent()) === title) break;
    await page.locator('.driver-popover-next-btn').click();
    await page.waitForTimeout(400);
  }
  await expect(page.locator('.driver-popover-title')).toHaveText(title);
  await page.waitForTimeout(500);
}

test.describe('shell', () => {
  test('an admin on a short viewport can scroll the nav to Platform above the account card', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 640 });
    await signIn(page);
    await page.goto('/claims/queue');
    const scroll = page.locator('.nav-scroll');
    await expect(scroll.getByRole('link', { name: 'Claims queue' })).toBeVisible();
    await scroll.evaluate((e) => (e.scrollTop = e.scrollHeight));
    const webhooks = scroll.getByRole('link', { name: 'Webhooks' });
    await expect(webhooks).toBeInViewport();
    const link = (await webhooks.boundingBox())!;
    const foot = (await page.locator('.side-foot').boundingBox())!;
    expect(link.y + link.height).toBeLessThanOrEqual(foot.y + 1);
    await page.screenshot({ path: `${SHOTS}/shell-short-viewport.png` });
  });

  test('expanded, account menu, rail with tooltip', async ({ page }) => {
    await signIn(page);
    await page.goto('/claims/queue');
    await expect(page.getByRole('heading', { name: 'Claims queue' })).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/shell-expanded.png` });

    await page.locator('.acct').click();
    await expect(page.getByRole('menuitem', { name: 'Sign out' })).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/shell-account-menu.png` });
    await page.keyboard.press('Escape');

    await page.getByRole('button', { name: 'Collapse sidebar' }).click();
    await page.getByRole('link', { name: 'Workload' }).hover();
    await expect(page.locator('fg-sidebar.rail')).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/shell-rail.png` });
    await page.getByRole('button', { name: 'Expand sidebar' }).click();
  });
});

test.describe('reviewer tour', () => {
  test('starts by itself on first sign-in and is remembered', async ({ page }) => {
    await signIn(page, 'self@fugue.test', { tour: true });
    await expect(page.locator('.driver-popover-title')).toHaveText('Built from your permissions');
    await page.locator('.driver-popover-close-btn').click();
    await expect(page.locator('.driver-popover')).toHaveCount(0);
    await page.reload();
    await page.waitForTimeout(1500);
    await expect(page.locator('.driver-popover')).toHaveCount(0);
  });

  const personas: [string, string, string][] = [
    ['self@fugue.test', 'The timeline', 'tour-claimant'],
    ['staff@fugue.test', 'Only the actions valid for the status', 'tour-officer'],
    ['manager@fugue.test', 'Why there is no grand total', 'tour-manager'],
    ['admin@fugue.test', 'Roles and permissions', 'tour-admin'],
  ];
  for (const [email, step, shot] of personas) {
    test(`${email} walks to "${step}"`, async ({ page }) => {
      await signIn(page, email);
      await page.goto('/');
      await startTour(page);
      await tourTo(page, step);
      await page.screenshot({ path: `${SHOTS}/${shot}.png` });
    });
  }
});

test('access matrix', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1500 });
  await signIn(page);
  await page.goto('/access');
  const matrix = page.getByTestId('roles-matrix');
  await expect(matrix.getByRole('columnheader', { name: 'ADMIN' })).toBeVisible();
  await page.screenshot({ path: `${SHOTS}/access-matrix.png` });
});
