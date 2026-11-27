import { Dialog } from '@angular/cdk/dialog';
import { inject } from '@angular/core';
import { QueryClient } from '@tanstack/angular-query';

export const POLL_MS = 15_000;

/**
 * Query options that keep a claims screen fresh: refetch on window focus and every 15 s, PAUSED while a dialog is open,
 * a mutation is running, or the caller says the user is mid-edit. A claim's ETag must not move under a half-filled form.
 * Call it in an injection context (a field initialiser).
 */
export function livePoll(editing: () => boolean = () => false) {
  const dialog = inject(Dialog);
  const client = inject(QueryClient);
  const paused = () => editing() || dialog.openDialogs.length > 0 || client.isMutating() > 0;
  return {
    refetchOnWindowFocus: () => !paused(),
    refetchInterval: () => (paused() ? false : POLL_MS),
  } as const;
}
