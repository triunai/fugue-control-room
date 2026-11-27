import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { apiToken, SHOTS, signIn } from './support';

/** Pixel positions of the amount cells at 1440x900: B2 (repair), B3 (parts), B8 (excess). */
const AMOUNT_X = 640;
const ROW_Y = { repair: 340, parts: 364, excess: 484 };

async function enter(page: Page, y: number, text: string) {
  await page.mouse.dblclick(AMOUNT_X, y);
  await page.keyboard.press('Control+A');
  await page.keyboard.type(text);
  await page.keyboard.press('Enter');
}

test('assessment worksheet: formulas, use as reserve, open in Excel', async ({ page, request }) => {
  const token = await apiToken(request, 'staff@fugue.test');
  const list = await request.get('/api/v1/claims?limit=100&status=UnderReview', {
    headers: { Authorization: `Bearer ${token}` },
  });
  const claim = ((await list.json()) as { id: string; number: string }[]).find(
    (c) => c.number === 'CLM-MY-2026-900003',
  )!;
  await signIn(page, 'staff@fugue.test');
  await page.goto(`/claims/${claim.id}/assessment`);
  await expect(page.getByTestId('assess-net')).toContainText('MYR 0.00');
  await page.waitForTimeout(1500);

  await enter(page, ROW_Y.repair, '1000.5');
  await enter(page, ROW_Y.parts, '200');
  await enter(page, ROW_Y.excess, '100');
  await expect(page.getByTestId('assess-net')).toContainText('MYR 1,100.50');
  await page.screenshot({ path: `${SHOTS}/claim-assessment.png` });

  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Open in Excel' }).click();
  const file = await (await download).path();
  expect(readFileSync(file!).subarray(0, 2).toString()).toBe('PK'); // an .xlsx is a zip

  await page.getByRole('button', { name: 'Use as reserve' }).click();
  await expect(page.getByTestId('assess-message')).toContainText('Reserve set to MYR 1,100.50');
  const after = await request.get(`/api/v1/claims/${claim.id}`, { headers: { Authorization: `Bearer ${token}` } });
  expect((await after.json()).reserveMinor).toBe(110050);
});
