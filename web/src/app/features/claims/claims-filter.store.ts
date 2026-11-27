import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { ClaimFilter, EMPTY_CLAIM_FILTER } from '../../api/claims.api';

/** Client state: the queue's filters survive navigating to a claim and back. The rows and counts live in TanStack Query. */
export const ClaimsFilterStore = signalStore(
  { providedIn: 'root' },
  withState<ClaimFilter>({ ...EMPTY_CLAIM_FILTER }),
  withMethods((store) => ({
    setStatus: (status: ClaimFilter['status']) => patchState(store, { status }),
    setAssigned: (assigned: ClaimFilter['assigned']) => patchState(store, { assigned }),
    setMarket: (market: string) => patchState(store, { market }),
    setLine: (line: string) => patchState(store, { line }),
    setSearch: (q: string) => patchState(store, { q }),
    reset: () => patchState(store, { ...EMPTY_CLAIM_FILTER }),
  })),
);
