import { expect, test } from '@playwright/test';
import { SHOTS, signIn } from './support';

/** Reference screenshots of the claims screens (seeded Development data: self@ owns a claim waiting for an answer). */
test.describe('claims screenshots', () => {
  test('claimant: My claims and a claim waiting for an answer', async ({ page }) => {
    await signIn(page, 'self@fugue.test');
    await page.goto('/claims/mine');
    await expect(page.getByRole('table')).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/claims-my-claims.png` });

    await page
      .getByRole('row', { name: /Info requested/ })
      .getByRole('link')
      .first()
      .click();
    await expect(page.getByTestId('answer-panel')).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/claims-claimant-detail.png`, fullPage: true });
  });

  test('staff: queue and claim detail', async ({ page }) => {
    await signIn(page, 'staff@fugue.test');
    await expect(page.getByTestId('queue-table')).toBeVisible();
    await expect(page.getByTestId('kpis')).not.toContainText('…');
    await page.screenshot({ path: `${SHOTS}/claims-queue.png` });

    await page.getByRole('tab', { name: /Under review/ }).click();
    await page.getByTestId('queue-table').getByRole('link').first().click();
    await expect(page.getByTestId('handling')).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/claims-staff-detail.png`, fullPage: true });
  });

  test('manager: team and exposure', async ({ page }) => {
    await signIn(page, 'manager@fugue.test');
    await page.goto('/claims/team');
    await expect(page.getByTestId('team-table')).toBeVisible();
    await expect(page.getByTestId('exposure-table')).toBeVisible();
    await page.screenshot({ path: `${SHOTS}/team-exposure.png`, fullPage: true });
  });
});
