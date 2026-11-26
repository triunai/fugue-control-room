import { effect } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';

const KEY = 'fugue.console.prefs';

interface Prefs {
  sidebarCollapsed: boolean;
  /** Mask emails in tables (on by default for screen sharing). */
  maskEmails: boolean;
}

function load(): Prefs {
  const defaults: Prefs = { sidebarCollapsed: false, maskEmails: true };
  try {
    return { ...defaults, ...JSON.parse(localStorage.getItem(KEY) ?? '{}') };
  } catch {
    return defaults;
  }
}

/** Per-viewer UI preferences, remembered in localStorage (best effort). */
export const PrefsStore = signalStore(
  { providedIn: 'root' },
  withState<Prefs>(load()),
  withMethods((store) => ({
    toggleSidebar: () => patchState(store, { sidebarCollapsed: !store.sidebarCollapsed() }),
    setMaskEmails: (maskEmails: boolean) => patchState(store, { maskEmails }),
  })),
  withHooks({
    onInit(store) {
      effect(() => {
        const snapshot: Prefs = { sidebarCollapsed: store.sidebarCollapsed(), maskEmails: store.maskEmails() };
        try {
          localStorage.setItem(KEY, JSON.stringify(snapshot));
        } catch {
          /* ignore */
        }
      });
    },
  }),
);
