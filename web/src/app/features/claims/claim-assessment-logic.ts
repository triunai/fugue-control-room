import { cellToMinor } from './reserve-sheet-logic';
import { XlsxCell } from './xlsx-export';

/** The worksheet's rows (0-based; row 0 is the header). Inputs are editable, Gross and Net are formulas. */
export const ASSESSMENT_INPUTS = [
  'Repair or rebuild',
  'Parts',
  'Labour',
  'Rental or temporary accommodation',
  'Other',
] as const;
export const FIRST_INPUT_ROW = 1;
export const GROSS_ROW = FIRST_INPUT_ROW + ASSESSMENT_INPUTS.length; // 6
export const EXCESS_ROW = GROSS_ROW + 1; // 7
export const NET_ROW = EXCESS_ROW + 1; // 8
export const AMOUNT_COL = 1;

/** Excel/Univer A1 formulas (the sheet's rows are 1-based in A1 notation). */
export const GROSS_FORMULA = `=SUM(B${FIRST_INPUT_ROW + 1}:B${GROSS_ROW})`;
export const NET_FORMULA = `=B${GROSS_ROW + 1}-B${EXCESS_ROW + 1}`;

/** Whether a cell of the worksheet may be edited: the five inputs and the excess, in the amount column. */
export function isAssessmentInput(row: number, col: number): boolean {
  return col === AMOUNT_COL && ((row >= FIRST_INPUT_ROW && row < GROSS_ROW) || row === EXCESS_ROW);
}

export type Assessment =
  { ok: true; grossMinor: number; excessMinor: number; netMinor: number } | { ok: false; message: string };

/**
 * The estimate from the sheet's six input cells (five items then the excess), in minor units, exactly (no float sums).
 * Empty cells are 0; a cell that is not a non-negative amount with at most two decimals makes the whole estimate invalid.
 */
export function assess(items: readonly unknown[], excess: unknown): Assessment {
  const minor = (cell: unknown): number | undefined => {
    if (cell === 0 || cell === '0') return 0;
    const m = cellToMinor(cell);
    return m === null ? 0 : m;
  };
  const parts = items.map(minor);
  const ex = minor(excess);
  if (parts.some((p) => p === undefined) || ex === undefined) {
    return { ok: false, message: 'Every amount must be a number, zero or more, with at most two decimals.' };
  }
  const grossMinor = (parts as number[]).reduce((a, b) => a + b, 0);
  return { ok: true, grossMinor, excessMinor: ex, netMinor: grossMinor - ex };
}

/** A worksheet cell as a number of major units for the export (empty or invalid is 0; callers validate with `assess` first). */
export function cellToNumber(cell: unknown): number {
  return (cellToMinor(cell) ?? 0) / 100;
}

/** The grid written to the .xlsx: labels, the user's amounts, and live formulas (with their results) for Gross and Net. */
export function assessmentGrid(currency: string, items: readonly number[], excess: number): XlsxCell[][] {
  const cents = (n: number) => Math.round(n * 100);
  const gross = items.reduce((a, b) => a + cents(b), 0) / 100;
  const net = (cents(gross) - cents(excess)) / 100;
  return [
    ['Item', `Amount (${currency})`],
    ...ASSESSMENT_INPUTS.map((label, i): XlsxCell[] => [label, items[i]]),
    ['Gross estimate', { formula: GROSS_FORMULA.slice(1), result: gross }],
    ['Less: excess', excess],
    ['Net estimate', { formula: NET_FORMULA.slice(1), result: net }],
  ];
}
