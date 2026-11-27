import { Injectable, signal } from '@angular/core';

const KEY = 'fugue.hood.open';
const OFF_KEY = 'fugue.hood.off';

/** `?hood=off` on an entry URL starts the sidebar minimised for this browser session (presenting). */
export function hoodOffFromUrl(): void {
  try {
    if (new URLSearchParams(location.search).get('hood') === 'off') sessionStorage.setItem(OFF_KEY, '1');
  } catch {
    /* best effort */
  }
}

function startsOpen(): boolean {
  try {
    if (sessionStorage.getItem(OFF_KEY) === '1') return false;
  } catch {
    /* ignore */
  }
  try {
    return localStorage.getItem(KEY) !== '0';
  } catch {
    return true;
  }
}

/** Whether the sidebar is open. Open on a first visit; a choice is remembered; `?hood=off` is not remembered. */
@Injectable({ providedIn: 'root' })
export class HoodState {
  private readonly _open = signal(startsOpen());
  readonly open = this._open.asReadonly();

  toggle(): void {
    const next = !this._open();
    this._open.set(next);
    try {
      localStorage.setItem(KEY, next ? '1' : '0');
    } catch {
      /* best effort */
    }
  }
}

/** The "." shortcut applies unless the person is typing. */
export function isTypingTarget(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el || typeof el.tagName !== 'string') return false;
  return /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName) || el.isContentEditable === true;
}
