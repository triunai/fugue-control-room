import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';

const REFRESH_KEY = 'fugue.console.refresh';

export type SessionStatus = 'anonymous' | 'restoring' | 'authenticated' | 'expired';

interface SessionState {
  /** In memory only: never written to storage, gone on reload (the refresh token restores it). */
  accessToken: string | null;
  /** Tab-scoped (sessionStorage) so a reload survives; see web/README.md "Token strategy" for the trade-off. */
  refreshToken: string | null;
  expiresAt: number | null;
  status: SessionStatus;
}

function readRefresh(): string | null {
  try {
    return sessionStorage.getItem(REFRESH_KEY);
  } catch {
    return null;
  }
}

function writeRefresh(token: string | null): void {
  try {
    if (token) sessionStorage.setItem(REFRESH_KEY, token);
    else sessionStorage.removeItem(REFRESH_KEY);
  } catch {
    /* storage blocked: the session simply will not survive a reload */
  }
}

/** Client state for the signed-in session. Server data (who am I, permissions) lives in TanStack Query, not here. */
export const SessionStore = signalStore(
  { providedIn: 'root' },
  withState<SessionState>({ accessToken: null, refreshToken: readRefresh(), expiresAt: null, status: 'anonymous' }),
  withComputed(({ accessToken }) => ({
    isAuthenticated: computed(() => accessToken() !== null),
  })),
  withMethods((store) => ({
    signedIn(accessToken: string, expiresInSeconds: number, refreshToken: string | null): void {
      writeRefresh(refreshToken);
      patchState(store, {
        accessToken,
        refreshToken,
        expiresAt: Date.now() + expiresInSeconds * 1000,
        status: 'authenticated',
      });
    },
    restoring(): void {
      patchState(store, { status: 'restoring' });
    },
    cleared(status: SessionStatus = 'anonymous'): void {
      writeRefresh(null);
      patchState(store, { accessToken: null, refreshToken: null, expiresAt: null, status });
    },
  })),
);
