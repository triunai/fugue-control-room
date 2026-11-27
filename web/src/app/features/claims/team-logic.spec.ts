import { ExposureRowDto } from '../../api/models';
import { exposureMatrix, oldestPerOfficer, summariseOpen } from './team-logic';

const NOW = Date.parse('2026-11-27T00:00:00Z');
const day = (n: number) => new Date(NOW - n * 86_400_000).toISOString();
const claim = (id: string, ageD: number, officer: string | null) => ({
  id,
  number: `N-${id}`,
  submittedAt: day(ageD),
  assignedOfficerId: officer,
});

describe('summariseOpen', () => {
  it('counts claims past the 14 day SLA and finds the oldest', () => {
    const s = summariseOpen([claim('a', 3, 'o1'), claim('b', 14, 'o1'), claim('c', 20, null)], NOW);
    expect(s.pastSla).toBe(2);
    expect(s.oldest).toEqual({ id: 'c', number: 'N-c', ageDays: 20 });
  });

  it('is empty-safe', () => {
    expect(summariseOpen([], NOW)).toEqual({ pastSla: 0, oldest: null });
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
  ): ExposureRowDto => ({
    market: market as ExposureRowDto['market'],
    currency,
    line,
    openCount: 1,
    outstandingReserveMinor: res,
    approvedNotSettledMinor: app,
    approvedNotSettledCount: 0,
  });

  it('sums reserve and approved per market and line, in the market currency', () => {
    const m = exposureMatrix([
      row('SG', 'SGD', 'Motor', 100, 50),
      row('SG', 'SGD', 'Property', 300, 0),
      row('ID', 'IDR', 'Motor', 9_000_000, 0),
    ]);
    const sg = m.find((r) => r.market === 'SG')!;
    expect(sg.motor.minor).toBe(150);
    expect(sg.property.minor).toBe(300);
    expect(sg.totalMinor).toBe(450);
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

  it('an empty cell has no bar', () => {
    expect(exposureMatrix([row('SG', 'SGD', 'Motor', 100, 0)])[0].property).toEqual({ minor: 0, pct: 0 });
  });
});
