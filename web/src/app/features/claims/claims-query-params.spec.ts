import { QUEUE_DEFAULT, filterToParams, hasFilterParams, paramsToFilter } from './claims-query-params';

describe('queue URL params', () => {
  it('a bare URL is the Open view and carries no filter params', () => {
    expect(paramsToFilter({})).toEqual(QUEUE_DEFAULT);
    expect(hasFilterParams({})).toBe(false);
    expect(filterToParams(QUEUE_DEFAULT)).toEqual({});
  });

  it('reads status, assigned, market, line, officer and an age window', () => {
    const f = paramsToFilter({
      status: 'UnderReview',
      assigned: 'none',
      market: 'SG',
      officerId: 'o1',
      minAgeDays: '14',
      maxAgeDays: '30',
    });
    expect(f).toMatchObject({
      status: 'UnderReview',
      assigned: 'none',
      market: 'SG',
      officerId: 'o1',
      minAgeDays: 14,
      maxAgeDays: 30,
    });
  });

  it('"all" means every status; junk falls back to the defaults', () => {
    expect(paramsToFilter({ status: 'all' }).status).toBe('');
    expect(paramsToFilter({ status: 'Bogus', assigned: 'x', minAgeDays: 'abc' })).toEqual(QUEUE_DEFAULT);
  });

  it('round-trips a filter through the URL', () => {
    const f = { ...QUEUE_DEFAULT, status: '' as const, assigned: 'me' as const, q: 'POL-1', minAgeDays: 2 };
    expect(paramsToFilter(filterToParams(f))).toEqual(f);
  });
});
