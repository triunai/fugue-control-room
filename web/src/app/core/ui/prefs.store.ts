import { effect } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';

const KEY = 'fugue.console.prefs';

interface Prefs {
  sidebarCollapsed: boolean;
  /** Labels of the sidebar groups the user folded. */
  collapsedGroups: string[];
  /** Mask emails in tables (on by default for screen sharing). */
  maskEmails: boolean;
}

function load(): Prefs {
  const defaults: Prefs = { sidebarCollapsed: false, collapsedGroups: [], maskEmails: true };
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
    toggleGroup: (label: string) => {
      const open = store.collapsedGroups();
      patchState(store, { collapsedGroups: open.includes(label) ? open.filter((g) => g !== label) : [...open, label] });
    },
    /** Open a group (the one holding the route on screen), never closing anything. */
    openGroup: (label: string) => {
      if (store.collapsedGroups().includes(label)) {
        patchState(store, { collapsedGroups: store.collapsedGroups().filter((g) => g !== label) });
      }
    },
    setMaskEmails: (maskEmails: boolean) => patchState(store, { maskEmails }),
  })),
  withHooks({
    onInit(store) {
      effect(() => {
        const snapshot: Prefs = {
          sidebarCollapsed: store.sidebarCollapsed(),
          collapsedGroups: store.collapsedGroups(),
          maskEmails: store.maskEmails(),
        };
        try {
          localStorage.setItem(KEY, JSON.stringify(snapshot));
        } catch {
          /* ignore */
        }
      });
    },
  }),
);
