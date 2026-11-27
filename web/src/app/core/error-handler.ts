import { ErrorHandler, Provider } from '@angular/core';

/**
 * The browser's own text for a ResizeObserver callback whose layout change is only picked up on the next frame. It is a
 * harmless notice, not a failure: FullCalendar re-lays out its day cells inside its ResizeObserver callback when the
 * window is resized, and every browser then fires a window `error` event with exactly this message. Angular's global
 * listener forwards it here, where it would be logged to the console as an error.
 */
export const RESIZE_OBSERVER_LOOP = 'ResizeObserver loop completed with undelivered notifications.';

/** True only for that one message (exact match). In dev builds Angular wraps it: the text is then on `cause`. */
export function isResizeObserverLoop(error: unknown): boolean {
  if (!(error instanceof Error)) return false;
  const cause = error.cause as { message?: unknown } | undefined;
  return error.message === RESIZE_OBSERVER_LOOP || cause?.message === RESIZE_OBSERVER_LOOP;
}

/** Angular's default handler (logs to the console), minus the one benign ResizeObserver notice above. */
export class FugueErrorHandler extends ErrorHandler {
  override handleError(error: unknown): void {
    if (isResizeObserverLoop(error)) return;
    super.handleError(error);
  }
}

export const provideFugueErrorHandler = (): Provider => ({ provide: ErrorHandler, useClass: FugueErrorHandler });
