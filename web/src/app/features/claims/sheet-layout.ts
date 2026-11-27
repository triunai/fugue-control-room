/** Layout facts shared by the two Univer sheets, kept apart from the Univer import so they unit-test without it. */

/** The sheet has exactly the data rows plus the header: no stray empty rows below the data. */
export function sheetRowCount(dataRows: number): number {
  return dataRows + 1;
}

/** Reserve sheet columns: claim, market, currency, line, status, officer, reserve, age (px). */
export const RESERVE_COLUMN_WIDTHS: readonly number[] = [200, 70, 80, 100, 130, 170, 130, 90];

/** Assessment sheet columns: a wide label column, then the amount (px). */
export const ASSESSMENT_COLUMN_WIDTHS: readonly number[] = [300, 170];

/** Pixel height that fits the Univer chrome (ribbon, toolbar, formula bar, column letters) plus every row, no dead space. */
export function sheetHostHeight(rowCount: number): number {
  return 124 + rowCount * 24 + 20;
}
