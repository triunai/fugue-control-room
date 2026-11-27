import { letterFileName, letterKind } from './claim-letter-button';

describe('claim letter', () => {
  it('exists only for decided claims: a settlement letter when approved or settled, a decision letter when rejected', () => {
    expect(letterKind('Approved')).toBe('settlement');
    expect(letterKind('Settled')).toBe('settlement');
    expect(letterKind('Rejected')).toBe('decision');
    expect(letterKind('Submitted')).toBeNull();
    expect(letterKind('UnderReview')).toBeNull();
    expect(letterKind('InfoRequested')).toBeNull();
  });

  it('is named after the claim number and the kind, as the API names it', () => {
    expect(letterFileName('CLM-SG-2026-000123', 'settlement')).toBe('CLM-SG-2026-000123-settlement-letter.pdf');
    expect(letterFileName('CLM-SG-2026-000123', 'decision')).toBe('CLM-SG-2026-000123-decision-letter.pdf');
  });
});
