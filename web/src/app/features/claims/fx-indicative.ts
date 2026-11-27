/**
 * INDICATIVE USD conversion for the exposure page (D-064). Fixed demo rates, never used for any decision:
 * the per-currency figures stay the only exact numbers. An unknown currency is reported, never dropped or guessed.
 */
export const FX_AS_OF = '2026-11-28';
export const FX_SOURCE = 'Indicative mid-market, fixed for the demo, not used for any decision.';

/** USD per one unit of the currency. */
export const USD_PER_UNIT: Readonly<Record<string, number>> = {
  HKD: 0.128,
  IDR: 0.000061,
  MYR: 0.22,
  PHP: 0.017,
  SGD: 0.77,
  THB: 0.029,
};

/** Minor units (cents) of `currency` to whole USD cents, rounded half up; null when the currency has no rate. */
export function toUsdCents(minor: number, currency: string): number | null {
  const rate = USD_PER_UNIT[currency];
  return rate === undefined ? null : Math.round(minor * rate);
}

export interface FxInputRow {
  market: string;
  currency: string;
  motor: { outstandingMinor: number };
  property: { outstandingMinor: number };
}

export interface UsdView {
  /** USD cents of outstanding reserve per market, largest first. */
  byMarket: { market: string; usdCents: number }[];
  /** USD cents per line. */
  byLine: { line: 'Motor' | 'Property'; usdCents: number }[];
  totalUsdCents: number;
  /** Currencies with no rate: their rows are excluded from every figure above and must be shown to the reader. */
  unconverted: string[];
}

export function usdView(rows: readonly FxInputRow[]): UsdView {
  const byMarket: UsdView['byMarket'] = [];
  const line = { Motor: 0, Property: 0 };
  const unconverted = new Set<string>();
  for (const r of rows) {
    const motor = toUsdCents(r.motor.outstandingMinor, r.currency);
    const property = toUsdCents(r.property.outstandingMinor, r.currency);
    if (motor === null || property === null) {
      unconverted.add(r.currency);
      continue;
    }
    line.Motor += motor;
    line.Property += property;
    byMarket.push({ market: r.market, usdCents: motor + property });
  }
  byMarket.sort((a, b) => b.usdCents - a.usdCents);
  return {
    byMarket,
    byLine: [
      { line: 'Motor', usdCents: line.Motor },
      { line: 'Property', usdCents: line.Property },
    ],
    totalUsdCents: line.Motor + line.Property,
    unconverted: [...unconverted].sort(),
  };
}
