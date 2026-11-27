import { ClaimDto, ExposureRowDto, ExposureTotalDto } from '../../api/models';
import { SLA_DECISION_DAYS, ageDays, marketName } from './claims-logic';

type OpenClaim = Pick<ClaimDto, 'id' | 'number' | 'submittedAt' | 'assignedOfficerId'>;

export interface OpenSummary {
  /** Open claims older than the decision SLA. */
  pastSla: number;
  /** The longest-waiting open claim (the queue reads oldest first, but this does not rely on it). */
  oldest: { id: string; number: string; ageDays: number } | null;
}

export function summariseOpen(claims: readonly OpenClaim[], now = Date.now()): OpenSummary {
  let oldest: OpenClaim | null = null;
  let pastSla = 0;
  for (const c of claims) {
    if (ageDays(c.submittedAt, now) >= SLA_DECISION_DAYS) pastSla++;
    if (!oldest || c.submittedAt < oldest.submittedAt) oldest = c;
  }
  return {
    pastSla,
    oldest: oldest ? { id: oldest.id, number: oldest.number, ageDays: ageDays(oldest.submittedAt, now) } : null,
  };
}

/** The longest-waiting open claim per assigned officer, so an officer's "oldest open" can link to the claim itself. */
export function oldestPerOfficer(claims: readonly OpenClaim[]): Map<string, { id: string; number: string }> {
  const best = new Map<string, OpenClaim>();
  for (const c of claims) {
    if (!c.assignedOfficerId) continue;
    const cur = best.get(c.assignedOfficerId);
    if (!cur || c.submittedAt < cur.submittedAt) best.set(c.assignedOfficerId, c);
  }
  return new Map([...best].map(([k, c]) => [k, { id: c.id, number: c.number }]));
}

export interface ExposureCell {
  /** Outstanding reserve + approved-not-settled, in the market's own currency. */
  minor: number;
  /** 0-100, scaled against the largest cell OF THE SAME CURRENCY only. */
  pct: number;
}

export interface ExposureMatrixRow {
  market: string;
  marketName: string;
  currency: string;
  motor: ExposureCell;
  property: ExposureCell;
  totalMinor: number;
}

/** Market x line matrix. Every amount stays in its row's currency; bars never compare across currencies. */
export function exposureMatrix(rows: readonly ExposureRowDto[]): ExposureMatrixRow[] {
  const byMarket = new Map<string, { currency: string; Motor: number; Property: number }>();
  for (const r of rows) {
    const m = byMarket.get(r.market) ?? { currency: r.currency, Motor: 0, Property: 0 };
    m[r.line] += r.outstandingReserveMinor + r.approvedNotSettledMinor;
    byMarket.set(r.market, m);
  }
  const maxPerCurrency = new Map<string, number>();
  for (const m of byMarket.values()) {
    maxPerCurrency.set(m.currency, Math.max(maxPerCurrency.get(m.currency) ?? 0, m.Motor, m.Property));
  }
  const cell = (minor: number, currency: string): ExposureCell => ({
    minor,
    pct: minor > 0 ? Math.max(4, (minor / (maxPerCurrency.get(currency) ?? 1)) * 100) : 0,
  });
  return [...byMarket]
    .map(([market, m]) => ({
      market,
      marketName: marketName(market),
      currency: m.currency,
      motor: cell(m.Motor, m.currency),
      property: cell(m.Property, m.currency),
      totalMinor: m.Motor + m.Property,
    }))
    .sort((a, b) => a.market.localeCompare(b.market));
}

/** Per-currency totals: outstanding + approved-not-settled, never added across currencies. */
export function currencyTotals(
  totals: readonly ExposureTotalDto[],
): { currency: string; minor: number; open: number }[] {
  return totals.map((t) => ({
    currency: t.currency,
    minor: t.outstandingReserveMinor + t.approvedNotSettledMinor,
    open: t.openCount,
  }));
}
