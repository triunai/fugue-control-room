import {
  ExposureRowX,
  bandWindow,
  exposureQueueParams,
  byLoad,
  exposureMatrix,
  oldestPerOfficer,
  summariseOpen,
} from './team-logic';

const NOW = Date.parse('2026-11-27T00:00:00Z');
const day = (n: number) => new Date(NOW - n * 86_400_000).toISOString();
const claim = (
  id: string,
  ageD: number,
  officer: string | null,
  status: 'Submitted' | 'UnderReview' = 'UnderReview',
  reserveMinor: number | null = 5,
) => ({
  id,
  number: `N-${id}`,
  status,
  reserveMinor,
  submittedAt: day(ageD),
  assignedOfficerId: officer,
});

describe('summariseOpen', () => {
  it('counts claims past the 14 day SLA and finds the oldest', () => {
    const s = summariseOpen([claim('a', 3, 'o1'), claim('b', 14, 'o1'), claim('c', 20, null)], NOW);
    expect(s.pastSla).toBe(2);
    expect(s.oldest).toEqual({ id: 'c', number: 'N-c', ageDays: 20 });
  });

  it('counts late pickups, and open claims with no reserve whatever their status (the exposure definition)', () => {
    const s = summariseOpen(
      [
        claim('a', 3, null, 'Submitted'),
        claim('e', 1, null, 'Submitted', null),
        claim('b', 1, null, 'Submitted'),
        claim('c', 5, 'o1', 'UnderReview', null),
        claim('d', 5, 'o1'),
      ],
      NOW,
    );
    expect(s.unassignedLate).toBe(1);
    expect(s.noReserve).toBe(2);
  });

  it('is empty-safe', () => {
    expect(summariseOpen([], NOW)).toEqual({ pastSla: 0, unassignedLate: 0, noReserve: 0, oldest: null });
  });
});

describe('oldestPerOfficer', () => {
  it("keeps each officer's longest-waiting claim and skips unassigned ones", () => {
    const m = oldestPerOfficer([claim('a', 3, 'o1'), claim('b', 9, 'o1'), claim('c', 20, null), claim('d', 1, 'o2')]);
    expect(m.get('o1')).toEqual({ id: 'b', number: 'N-b' });
    expect(m.get('o2')?.id).toBe('d');
    expect(m.size).toBe(2);
  });
});

describe('exposureMatrix', () => {
  const row = (
    market: string,
    currency: string,
    line: 'Motor' | 'Property',
    res: number,
    app: number,
    unreserved?: number,
  ): ExposureRowX => ({
    market: market as ExposureRowX['market'],
    currency,
    line,
    openCount: 1,
    outstandingReserveMinor: res,
    approvedNotSettledMinor: app,
    approvedNotSettledCount: 0,
    unreservedOpenCount: unreserved ?? 0,
  });

  it('keeps outstanding and approved apart per market and line, in the market currency', () => {
    const m = exposureMatrix([
      row('SG', 'SGD', 'Motor', 100, 50),
      row('SG', 'SGD', 'Property', 300, 0),
      row('ID', 'IDR', 'Motor', 9_000_000, 0),
    ]);
    const sg = m.find((r) => r.market === 'SG')!;
    expect(sg.motor).toMatchObject({ outstandingMinor: 100, approvedMinor: 50 });
    expect(sg.outstandingMinor).toBe(400);
    expect(sg.approvedMinor).toBe(50);
    expect(sg.currency).toBe('SGD');
  });

  it('scales each bar within its own currency: a huge IDR amount does not shrink the SGD bars', () => {
    const m = exposureMatrix([
      row('SG', 'SGD', 'Motor', 100, 0),
      row('SG', 'SGD', 'Property', 300, 0),
      row('ID', 'IDR', 'Motor', 9_000_000, 0),
    ]);
    const sg = m.find((r) => r.market === 'SG')!;
    expect(sg.property.pct).toBe(100);
    expect(sg.motor.pct).toBeCloseTo(33.3, 0);
    expect(m.find((r) => r.market === 'ID')!.motor.pct).toBe(100);
  });

  it('an empty cell has no bar; unreserved counts are optional and summed per market', () => {
    const one = exposureMatrix([row('SG', 'SGD', 'Motor', 100, 0)])[0];
    expect(one.property).toMatchObject({ pct: 0, unreserved: 0 });
    const m = exposureMatrix([row('SG', 'SGD', 'Motor', 100, 0, 2), row('SG', 'SGD', 'Property', 5, 0, 1)])[0];
    expect(m.unreserved).toBe(3);
  });
});

describe('bandWindow and byLoad', () => {
  it('turns an ageing band into an age window', () => {
    expect(bandWindow('0-2')).toEqual({ minAgeDays: 0, maxAgeDays: 2 });
    expect(bandWindow('15+')).toEqual({ minAgeDays: 15 });
    expect(bandWindow('weird')).toEqual({});
  });

  it('sorts officers heaviest first without mutating', () => {
    const input = [
      { officerId: 'a', underReview: 1, infoRequested: 0 },
      { officerId: 'b', underReview: 2, infoRequested: 2 },
    ];
    expect(byLoad(input).map((o) => o.officerId)).toEqual(['b', 'a']);
    expect(input[0].officerId).toBe('a');
  });
});

describe('exposureQueueParams', () => {
  it('opens the market and line, defaulting to the queue open view', () => {
    expect(exposureQueueParams('MY', 'Motor')).toEqual({ market: 'MY', line: 'Motor' });
  });
  it('opens approved-not-settled claims for the same cell', () => {
    expect(exposureQueueParams('SG', 'Property', true)).toEqual({ market: 'SG', line: 'Property', status: 'Approved' });
  });
});
