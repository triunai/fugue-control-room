import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { AuditFilter, EMPTY_AUDIT_FILTER } from '../../api/audit.api';

/** Client state: the applied audit filters and the selected entry id. The entries live in TanStack Query. */
export const AuditFilterStore = signalStore(
  { providedIn: 'root' },
  withState<{ filter: AuditFilter; selectedId: string | null }>({ filter: EMPTY_AUDIT_FILTER, selectedId: null }),
  withComputed(({ filter }) => ({
    activeCount: computed(() => Object.values(filter()).filter((v) => v.trim() !== '').length),
  })),
  withMethods((store) => ({
    apply: (filter: AuditFilter) => patchState(store, { filter: { ...filter } }),
    byCorrelation: (correlationId: string) =>
      patchState(store, { filter: { ...EMPTY_AUDIT_FILTER, correlationId }, selectedId: null }),
    byEntity: (entityType: string, entityId: string) =>
      patchState(store, { filter: { ...EMPTY_AUDIT_FILTER, entityType, entityId }, selectedId: null }),
    clear: () => patchState(store, { filter: EMPTY_AUDIT_FILTER }),
    select: (selectedId: string | null) => patchState(store, { selectedId }),
  })),
);
