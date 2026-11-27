import { describe, expect, it } from 'vitest';
import { FxInputRow, toUsdCents, usdView } from './fx-indicative';

const row = (market: string, currency: string, motor: number, property: number): FxInputRow => ({
  market,
  currency,
  motor: { outstandingMinor: motor },
  property: { outstandingMinor: property },
});

describe('toUsdCents', () => {
  it('converts minor units at the fixed rate', () => {
    expect(toUsdCents(100_000, 'SGD')).toBe(77_000);
  });
  it('rounds to whole cents', () => {
    expect(toUsdCents(1, 'SGD')).toBe(1);
    expect(toUsdCents(1, 'IDR')).toBe(0);
    expect(toUsdCents(1_000_000, 'IDR')).toBe(61);
  });
  it('returns null for an unknown currency', () => {
    expect(toUsdCents(100, 'EUR')).toBeNull();
  });
});

describe('usdView', () => {
  it('totals by market (largest first), line and overall', () => {
    const v = usdView([row('MY', 'MYR', 10_000, 0), row('SG', 'SGD', 10_000, 10_000)]);
    expect(v.byMarket).toEqual([
      { market: 'SG', usdCents: 15_400 },
      { market: 'MY', usdCents: 2_200 },
    ]);
    expect(v.byLine).toEqual([
      { line: 'Motor', usdCents: 9_900 },
      { line: 'Property', usdCents: 7_700 },
    ]);
    expect(v.totalUsdCents).toBe(17_600);
    expect(v.unconverted).toEqual([]);
  });
  it('reports an unknown currency instead of dropping it silently', () => {
    const v = usdView([row('SG', 'SGD', 10_000, 0), row('XX', 'EUR', 5_000, 0)]);
    expect(v.unconverted).toEqual(['EUR']);
    expect(v.byMarket.map((m) => m.market)).toEqual(['SG']);
    expect(v.totalUsdCents).toBe(7_700);
  });
});
