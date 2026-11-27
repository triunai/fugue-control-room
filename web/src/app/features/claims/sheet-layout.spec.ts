import { ASSESSMENT_INPUTS } from './claim-assessment-logic';
import { RESERVE_COL, SHEET_COLUMNS } from './reserve-sheet-logic';
import { ASSESSMENT_COLUMN_WIDTHS, RESERVE_COLUMN_WIDTHS, sheetHostHeight, sheetRowCount } from './sheet-layout';

describe('sheet layout', () => {
  it('has exactly the data rows plus the header', () => {
    expect(sheetRowCount(0)).toBe(1);
    expect(sheetRowCount(17)).toBe(18);
    // assessment: five inputs + gross + excess + net, plus the header
    expect(sheetRowCount(ASSESSMENT_INPUTS.length + 3)).toBe(9);
  });

  it('has one width per column, a wide label column and the reserve wide enough for amounts', () => {
    expect(RESERVE_COLUMN_WIDTHS).toHaveLength(SHEET_COLUMNS.length);
    expect(RESERVE_COLUMN_WIDTHS[RESERVE_COL]).toBeGreaterThanOrEqual(120);
    expect(ASSESSMENT_COLUMN_WIDTHS).toHaveLength(2);
    expect(ASSESSMENT_COLUMN_WIDTHS[0]).toBeGreaterThan(ASSESSMENT_COLUMN_WIDTHS[1]);
  });

  it('sizes the host to the chrome plus the rows', () => {
    expect(sheetHostHeight(9)).toBe(360);
    expect(sheetHostHeight(17)).toBeGreaterThan(sheetHostHeight(9));
  });
});
