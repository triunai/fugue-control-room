import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { ClaimFilter } from '../../api/claims.api';
import { QUEUE_DEFAULT } from './claims-query-params';

/** Client state: the queue's filters survive navigating to a claim and back, and mirror the URL query params. */
type QueueState = ClaimFilter & {
  officerId: string | undefined;
  minAgeDays: number | undefined;
  maxAgeDays: number | undefined;
};

export const ClaimsFilterStore = signalStore(
  { providedIn: 'root' },
  withState<QueueState>({ ...QUEUE_DEFAULT, officerId: undefined, minAgeDays: undefined, maxAgeDays: undefined }),
  withMethods((store) => ({
    setStatus: (status: ClaimFilter['status']) => patchState(store, { status }),
    setAssigned: (assigned: ClaimFilter['assigned']) => patchState(store, { assigned }),
    setMarket: (market: string) => patchState(store, { market }),
    setLine: (line: string) => patchState(store, { line }),
    setSearch: (q: string) => patchState(store, { q }),
    /** Replace every filter at once (a link into the queue sets them all). */
    setAll: (f: ClaimFilter) =>
      patchState(store, { officerId: undefined, minAgeDays: undefined, maxAgeDays: undefined, ...f }),
    reset: () =>
      patchState(store, { ...QUEUE_DEFAULT, officerId: undefined, minAgeDays: undefined, maxAgeDays: undefined }),
  })),
);
