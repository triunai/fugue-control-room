import { ExposureRowDto } from '../../api/models';
import { paidByMarket } from './team-page';

const row = (
  market: ExposureRowDto['market'],
  line: ExposureRowDto['line'],
  currency: string,
  settledCount: number,
  settledMinor: number,
): ExposureRowDto => ({
  market,
  line,
  currency,
  openCount: 0,
  outstandingReserveMinor: 0,
  approvedNotSettledCount: 0,
  approvedNotSettledMinor: 0,
  unreservedOpenCount: 0,
  settledCount,
  settledMinor,
});

describe('paidByMarket', () => {
  it('sums the settled count and amount per line and per market, in that market only', () => {
    const paid = paidByMarket([
      row('SG', 'Motor', 'SGD', 2, 85_000),
      row('SG', 'Property', 'SGD', 1, 10_000),
      row('TH', 'Motor', 'THB', 1, 1_800_000),
    ]);
    expect(paid.get('SG')).toEqual({
      Motor: { count: 2, minor: 85_000 },
      Property: { count: 1, minor: 10_000 },
      total: { count: 3, minor: 95_000 },
    });
    expect(paid.get('TH')?.total).toEqual({ count: 1, minor: 1_800_000 });
    expect(paid.get('TH')?.Property).toEqual({ count: 0, minor: 0 });
  });

  it('keeps a market with nothing settled at zero, never borrowing another currency', () => {
    const paid = paidByMarket([row('MY', 'Motor', 'MYR', 0, 0), row('SG', 'Motor', 'SGD', 1, 5)]);
    expect(paid.get('MY')?.total).toEqual({ count: 0, minor: 0 });
  });
});
