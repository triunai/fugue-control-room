import { ClaimsApi } from '../../api/claims.api';
import { ClaimDto } from '../../api/models';
import { OPEN_STATUSES } from './reserve-sheet-logic';

const MAX_PAGES_PER_STATUS = 5;

/** Every open claim the caller may read, oldest first. The list takes one status at a time, so this pages each open status in turn. */
export async function fetchOpenClaims(api: ClaimsApi, assigned: 'any' | 'me' = 'any'): Promise<ClaimDto[]> {
  const out: ClaimDto[] = [];
  for (const status of OPEN_STATUSES) {
    let cursor: string | null = null;
    for (let page = 0; page < MAX_PAGES_PER_STATUS; page++) {
      const res = await api.list({ status, assigned, market: '', line: '', q: '' }, cursor, 100);
      out.push(...res.items);
      cursor = res.nextCursor;
      if (!cursor) break;
    }
  }
  return out.sort((a, b) => a.submittedAt.localeCompare(b.submittedAt));
}
