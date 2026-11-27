import { ASSESSMENT_INPUTS, assess, assessmentGrid, isAssessmentInput } from './claim-assessment-logic';

describe('assess', () => {
  it('sums the five items and subtracts the excess exactly, in minor units', () => {
    const a = assess([1000.1, 250, 300.2, 0, null], 100);
    expect(a).toEqual({ ok: true, grossMinor: 155030, excessMinor: 10000, netMinor: 145030 });
  });
  it('treats empty cells as zero and allows a net at or below zero (the sheet decides what to do)', () => {
    expect(assess([null, null, null, null, null], null)).toMatchObject({ ok: true, grossMinor: 0, netMinor: 0 });
    expect(assess([50, 0, 0, 0, 0], 80)).toMatchObject({ ok: true, netMinor: -3000 });
  });
  it('refuses text, negatives and more than two decimals', () => {
    for (const bad of ['abc', -5, 1.234]) {
      expect(assess([bad, 0, 0, 0, 0], 0).ok).toBe(false);
      expect(assess([0, 0, 0, 0, 0], bad).ok).toBe(false);
    }
  });
});

describe('isAssessmentInput', () => {
  it('allows the five items and the excess in the amount column, nothing else', () => {
    const editable: number[] = [];
    for (let r = 0; r <= 9; r++) if (isAssessmentInput(r, 1)) editable.push(r);
    expect(editable).toEqual([1, 2, 3, 4, 5, 7]);
    expect(isAssessmentInput(1, 0)).toBe(false);
    expect(isAssessmentInput(6, 1)).toBe(false); // gross
    expect(isAssessmentInput(8, 1)).toBe(false); // net
  });
});

describe('assessmentGrid', () => {
  it('writes labels, amounts and live formulas with their results', () => {
    const grid = assessmentGrid('SGD', [100, 20.5, 0, 0, 0], 10);
    expect(grid[0]).toEqual(['Item', 'Amount (SGD)']);
    expect(grid.slice(1, 6).map((r) => r[0])).toEqual([...ASSESSMENT_INPUTS]);
    expect(grid[6]).toEqual(['Gross estimate', { formula: 'SUM(B2:B6)', result: 120.5 }]);
    expect(grid[7]).toEqual(['Less: excess', 10]);
    expect(grid[8]).toEqual(['Net estimate', { formula: 'B7-B8', result: 110.5 }]);
  });
});
