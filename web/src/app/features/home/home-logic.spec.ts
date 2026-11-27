import { bySlaRisk, claimantHero, landingKind, needsMyAction, officerPrimary } from './home-logic';

const has =
  (...codes: string[]) =>
  (c: string) =>
    codes.includes(c);

describe('landingKind', () => {
  it('picks the persona from permission codes, broadest first', () => {
    expect(landingKind(has('MODULES_MANAGE', 'CLAIMS_HANDLE', 'CLAIMS_WORKLOAD', 'CLAIMS_EXPOSURE'))).toBe('admin');
    expect(landingKind(has('CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_WORKLOAD', 'CLAIMS_EXPOSURE', 'CLAIMS_SETTLE'))).toBe(
      'manager',
    );
    expect(landingKind(has('CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_WORKLOAD'))).toBe('officer');
    expect(landingKind(has('CLAIMS_SUBMIT', 'CLAIMS_READ', 'CLAIMS_RESPOND'))).toBe('claimant');
  });

  it('anyone else gets no persona landing', () => {
    expect(landingKind(has('CUSTOMERS_VIEW'))).toBe('other');
    expect(landingKind(has())).toBe('other');
  });
});

describe('claimantHero', () => {
  const c = (
    id: string,
    status: 'Submitted' | 'InfoRequested' | 'Settled' | 'Rejected' | 'UnderReview',
    updatedAt: string,
  ) => ({
    id,
    status,
    updatedAt,
    submittedAt: updatedAt,
  });

  it('the claim with an open question comes first', () => {
    const h = claimantHero([c('a', 'UnderReview', '2026-11-26'), c('b', 'InfoRequested', '2026-11-01')]);
    expect(h).toMatchObject({ needsYou: true });
    expect(h.claim?.id).toBe('b');
  });

  it('otherwise the most recently updated open claim; closed claims never lead', () => {
    const h = claimantHero([
      c('a', 'Settled', '2026-11-27'),
      c('b', 'Submitted', '2026-11-01'),
      c('d', 'UnderReview', '2026-11-20'),
    ]);
    expect(h).toMatchObject({ needsYou: false });
    expect(h.claim?.id).toBe('d');
  });

  it('no claims, or only closed ones: no hero', () => {
    expect(claimantHero([]).claim).toBeNull();
    expect(claimantHero([c('a', 'Rejected', '2026-11-27')]).claim).toBeNull();
  });
});

describe('officer work', () => {
  const k = (id: string, submittedAt: string, reserveMinor: number | null = 5) => ({
    id,
    number: `N-${id}`,
    submittedAt,
    reserveMinor,
  });

  it('sorts by SLA risk, oldest first, without mutating', () => {
    const input = [k('a', '2026-11-20'), k('b', '2026-11-01')];
    expect(bySlaRisk(input).map((x) => x.id)).toEqual(['b', 'a']);
    expect(input[0].id).toBe('a');
  });

  it('the primary is my riskiest claim, else the oldest to pick up, else nothing', () => {
    expect(officerPrimary([k('a', '2026-11-20'), k('b', '2026-11-01')], [k('u', '2026-10-01')])).toMatchObject({
      kind: 'work',
      claim: { id: 'b' },
    });
    expect(officerPrimary([], [k('u', '2026-10-01')])).toMatchObject({ kind: 'pickup', claim: { id: 'u' } });
    expect(officerPrimary([], [])).toBeNull();
  });

  it('needs-my-action puts claims without a reserve first, then oldest', () => {
    const out = needsMyAction([k('a', '2026-11-01'), k('b', '2026-11-20', null), k('c', '2026-11-05', null)]);
    expect(out.map((x) => x.id)).toEqual(['c', 'b', 'a']);
  });
});
