import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { apiToken, SHOTS, signIn } from './support';

/** Pixel positions of the reserve cells at 1440x900 (the claims are seeded, oldest first): row 10 is an officer's own claim. */
const RESERVE_X = 1060;
const OWN_ROW_Y = 497;
const FOREIGN_ROW_Y = 305;

async function enter(page: import('@playwright/test').Page, y: number, text: string) {
  await page.mouse.dblclick(RESERVE_X, y);
  await page.keyboard.press('Control+A');
  await page.keyboard.type(text);
  await page.keyboard.press('Enter');
}

test.describe('reserve sheet and SLA calendar', () => {
  test('the sheet shows open claims, saves per row and maps a stale write to a reload', async ({ page, request }) => {
    await signIn(page, 'staff@fugue.test');
    await page.goto('/claims/sheet');
    await expect(page.getByTestId('sheet-pending')).toHaveText('0 unsaved changes');
    await page.waitForTimeout(2500); // the canvas paints after mount
    await page.screenshot({ path: `${SHOTS}/claims-sheet.png` });

    const amount = 1000 + Math.floor(Math.random() * 8000);

    // Someone else changes the claim after this page loaded: its row is stale (412) and the sheet reloads that row.
    const token = await apiToken(request, 'staff@fugue.test');
    const headers = { Authorization: `Bearer ${token}` };
    const list = await request.get('/api/v1/claims?limit=100&status=UnderReview', { headers });
    const claim = ((await list.json()) as { id: string; number: string; version: number }[]).find(
      (c) => c.number === 'CLM-MY-2026-900003',
    )!;
    const bump = await request.put(`/api/v1/claims/${claim.id}/reserve`, {
      headers: { ...headers, 'If-Match': `"${claim.version}"` },
      data: { amountMinor: 424200 + Math.floor(Math.random() * 1000) },
    });
    expect(bump.ok()).toBeTruthy();

    await enter(page, OWN_ROW_Y, `${amount}.5`);
    await enter(page, FOREIGN_ROW_Y, '1.234');
    await expect(page.getByTestId('sheet-pending')).toHaveText('2 unsaved changes');
    await page.getByTestId('sheet-save').click();
    const results = page.getByTestId('sheet-results');
    await expect(results).toContainText('Not a valid amount');
    await expect(results).toContainText('Changed by someone else');

    // Reload clears the results; now the row carries the current version and its save goes through.
    await page.getByRole('button', { name: 'Reload' }).click();
    await expect(page.getByTestId('sheet-pending')).toHaveText('0 unsaved changes');
    await page.waitForTimeout(2500);
    await enter(page, OWN_ROW_Y, `${amount + 1}.25`);
    await page.getByTestId('sheet-save').click();
    await expect(results.getByText('Saved', { exact: true })).toBeVisible();
  });

  test('Save to Excel downloads the sheet as an .xlsx', async ({ page }) => {
    await signIn(page, 'staff@fugue.test');
    await page.goto('/claims/sheet');
    await expect(page.getByRole('button', { name: 'Save to Excel' })).toBeEnabled();
    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Save to Excel' }).click();
    const d = await download;
    expect(d.suggestedFilename()).toBe('reserve-sheet.xlsx');
    expect(
      readFileSync((await d.path())!)
        .subarray(0, 2)
        .toString(),
    ).toBe('PK');
  });

  test('the SLA calendar lists open claims on their due dates and opens a claim', async ({ page }) => {
    await signIn(page, 'staff@fugue.test');
    await page.goto('/claims/calendar');
    await expect(page.getByTestId('sla-calendar')).toBeVisible();
    await expect(page.getByTestId('sla-count')).toContainText('open claims on the calendar');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: `${SHOTS}/claims-calendar.png` });
    await page.locator('.sla-ev').first().click();
    await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/);
  });
});
