import { ClaimDto, ExposureRowDto } from '../../api/models';
import { SLA_DECISION_DAYS, SLA_PICKUP_DAYS, ageDays, marketName } from './claims-logic';

type PickClaim = Pick<ClaimDto, 'id' | 'number' | 'submittedAt' | 'assignedOfficerId'>;
type OpenClaim = Pick<ClaimDto, 'id' | 'number' | 'status' | 'submittedAt' | 'assignedOfficerId' | 'reserveMinor'>;

export interface OpenSummary {
  /** Open claims older than the decision SLA. */
  pastSla: number;
  /** Submitted claims nobody picked up within the pickup SLA. */
  unassignedLate: number;
  /** Worked claims (under review or waiting on the claimant) with no reserve: exposure cannot count them. */
  noReserve: number;
  /** The longest-waiting open claim (the queue reads oldest first, but this does not rely on it). */
  oldest: { id: string; number: string; ageDays: number } | null;
}

export function summariseOpen(claims: readonly OpenClaim[], now = Date.now()): OpenSummary {
  let oldest: OpenClaim | null = null;
  let pastSla = 0;
  let unassignedLate = 0;
  let noReserve = 0;
  for (const c of claims) {
    const age = ageDays(c.submittedAt, now);
    if (age >= SLA_DECISION_DAYS) pastSla++;
    if (c.status === 'Submitted' && age >= SLA_PICKUP_DAYS) unassignedLate++;
    if (c.status !== 'Submitted' && (c.reserveMinor === null || c.reserveMinor === undefined)) noReserve++;
    if (!oldest || c.submittedAt < oldest.submittedAt) oldest = c;
  }
  return {
    pastSla,
    unassignedLate,
    noReserve,
    oldest: oldest ? { id: oldest.id, number: oldest.number, ageDays: ageDays(oldest.submittedAt, now) } : null,
  };
}

/** The longest-waiting open claim per assigned officer, so an officer's "oldest open" can link to the claim itself. */
export function oldestPerOfficer(claims: readonly PickClaim[]): Map<string, { id: string; number: string }> {
  const best = new Map<string, PickClaim>();
  for (const c of claims) {
    if (!c.assignedOfficerId) continue;
    const cur = best.get(c.assignedOfficerId);
    if (!cur || c.submittedAt < cur.submittedAt) best.set(c.assignedOfficerId, c);
  }
  return new Map([...best].map(([k, c]) => [k, { id: c.id, number: c.number }]));
}

/** An exposure row as the API sends it (`unreservedOpenCount` included since the nth-api2 merge). */
export type ExposureRowX = ExposureRowDto;

export interface ExposureCell {
  outstandingMinor: number;
  approvedMinor: number;
  /** Open claims with no reserve (absent until the backend sends it). */
  unreserved: number;
  /** 0-100, outstanding + approved scaled against the largest cell OF THE SAME CURRENCY only. */
  pct: number;
}

export interface ExposureMatrixRow {
  market: string;
  marketName: string;
  currency: string;
  motor: ExposureCell;
  property: ExposureCell;
  outstandingMinor: number;
  approvedMinor: number;
  unreserved: number;
}

const total = (c: { outstandingMinor: number; approvedMinor: number }) => c.outstandingMinor + c.approvedMinor;

interface Acc {
  outstandingMinor: number;
  approvedMinor: number;
  unreserved: number;
}

/** Market x line matrix. Every amount stays in its row's currency; bars never compare across currencies. */
export function exposureMatrix(rows: readonly ExposureRowX[]): ExposureMatrixRow[] {
  const zero = (): Acc => ({ outstandingMinor: 0, approvedMinor: 0, unreserved: 0 });
  const byMarket = new Map<string, { currency: string; Motor: Acc; Property: Acc }>();
  for (const r of rows) {
    const m = byMarket.get(r.market) ?? { currency: r.currency, Motor: zero(), Property: zero() };
    const c = m[r.line];
    c.outstandingMinor += r.outstandingReserveMinor;
    c.approvedMinor += r.approvedNotSettledMinor;
    c.unreserved += r.unreservedOpenCount ?? 0;
    byMarket.set(r.market, m);
  }
  const maxPerCurrency = new Map<string, number>();
  for (const m of byMarket.values()) {
    maxPerCurrency.set(m.currency, Math.max(maxPerCurrency.get(m.currency) ?? 0, total(m.Motor), total(m.Property)));
  }
  const cell = (a: Acc, currency: string): ExposureCell => ({
    ...a,
    pct: total(a) > 0 ? Math.max(4, (total(a) / (maxPerCurrency.get(currency) ?? 1)) * 100) : 0,
  });
  return [...byMarket]
    .map(([market, m]) => ({
      market,
      marketName: marketName(market),
      currency: m.currency,
      motor: cell(m.Motor, m.currency),
      property: cell(m.Property, m.currency),
      outstandingMinor: m.Motor.outstandingMinor + m.Property.outstandingMinor,
      approvedMinor: m.Motor.approvedMinor + m.Property.approvedMinor,
      unreserved: m.Motor.unreserved + m.Property.unreserved,
    }))
    .sort((a, b) => a.market.localeCompare(b.market));
}

/** An ageing band label ("0-2", "15+") as the queue's age window. */
export function bandWindow(label: string): { minAgeDays?: number; maxAgeDays?: number } {
  const range = /^(\d+)-(\d+)$/.exec(label);
  if (range) return { minAgeDays: Number(range[1]), maxAgeDays: Number(range[2]) };
  const open = /^(\d+)\+$/.exec(label);
  return open ? { minAgeDays: Number(open[1]) } : {};
}

export interface OfficerRow {
  officerId: string;
  underReview: number;
  infoRequested: number;
}

/** Officers by open load, heaviest first. */
export function byLoad<T extends OfficerRow>(officers: readonly T[]): T[] {
  return [...officers].sort((a, b) => b.underReview + b.infoRequested - (a.underReview + a.infoRequested));
}
