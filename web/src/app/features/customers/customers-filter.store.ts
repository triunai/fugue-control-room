import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { CustomerFilter } from '../../api/customers.api';

/** Client state: the Customers filters survive navigating away and back. The rows themselves live in TanStack Query. */
export const CustomersFilterStore = signalStore(
  { providedIn: 'root' },
  withState<CustomerFilter>({ search: '', status: '' }),
  withMethods((store) => ({
    setSearch: (search: string) => patchState(store, { search }),
    setStatus: (status: CustomerFilter['status']) => patchState(store, { status }),
    reset: () => patchState(store, { search: '', status: '' }),
  })),
);
