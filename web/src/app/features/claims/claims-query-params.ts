import { Params } from '@angular/router';
import { ClaimFilter } from '../../api/claims.api';
import { ClaimStatus } from '../../api/models';

/** The queue's default view: the open claims, not everything ever filed. */
export const QUEUE_DEFAULT: ClaimFilter = { status: 'open', assigned: 'any', market: '', line: '', q: '' };

const STATUSES = new Set<string>(Object.values(ClaimStatus));
const ASSIGNED = new Set(['any', 'me', 'none']);
const KEYS = ['status', 'assigned', 'market', 'line', 'q', 'officerId', 'minAgeDays', 'maxAgeDays'] as const;

const num = (v: unknown): number | undefined => {
  const t = typeof v === 'number' ? String(v) : v;
  return typeof t === 'string' && /^\d{1,4}$/.test(t) ? Number(t) : undefined;
};

/** True when the URL carries any filter, so a bare /claims/queue keeps the remembered filters. */
export function hasFilterParams(p: Params): boolean {
  return KEYS.some((k) => p[k] !== undefined);
}

/** URL query params -> a filter. Unknown or malformed values fall back to the default for that field. */
export function paramsToFilter(p: Params): ClaimFilter {
  const status = typeof p['status'] === 'string' ? p['status'] : undefined;
  const assigned = typeof p['assigned'] === 'string' && ASSIGNED.has(p['assigned']) ? p['assigned'] : 'any';
  const out: ClaimFilter = {
    ...QUEUE_DEFAULT,
    status:
      status === 'all'
        ? ''
        : status === 'open' || (status && STATUSES.has(status))
          ? (status as ClaimFilter['status'])
          : 'open',
    assigned: assigned as ClaimFilter['assigned'],
    market: typeof p['market'] === 'string' ? p['market'] : '',
    line: typeof p['line'] === 'string' ? p['line'] : '',
    q: typeof p['q'] === 'string' ? p['q'] : '',
  };
  if (typeof p['officerId'] === 'string' && p['officerId']) out.officerId = p['officerId'];
  const min = num(p['minAgeDays']);
  const max = num(p['maxAgeDays']);
  if (min !== undefined) out.minAgeDays = min;
  if (max !== undefined) out.maxAgeDays = max;
  return out;
}

/** A filter -> URL query params; defaults are left out so the URL stays short. */
export function filterToParams(f: ClaimFilter): Params {
  const p: Params = {};
  if (f.status !== 'open') p['status'] = f.status === '' ? 'all' : f.status;
  if (f.assigned !== 'any') p['assigned'] = f.assigned;
  if (f.market) p['market'] = f.market;
  if (f.line) p['line'] = f.line;
  if (f.q) p['q'] = f.q;
  if (f.officerId) p['officerId'] = f.officerId;
  if (f.minAgeDays !== undefined) p['minAgeDays'] = f.minAgeDays;
  if (f.maxAgeDays !== undefined) p['maxAgeDays'] = f.maxAgeDays;
  return p;
}
