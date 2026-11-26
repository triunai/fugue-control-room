import { DOCUMENT, Injectable, inject, signal } from '@angular/core';

/** 12 lowercase hex characters: short enough to read aloud from a toast, inside the API's [A-Za-z0-9-_]{1,64} rule. */
export function newCorrelationId(): string {
  const bytes = new Uint8Array(6);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

/**
 * One X-Correlation-Id per USER ACTION. A click, Enter/Space or form submit starts a new action; every request it
 * causes (the PUT and the refetch after it) shares the id, so one ref chip finds the whole chain in the audit log.
 * Background work (polls) pins its own id through the CORRELATION_ID context token instead.
 */
@Injectable({ providedIn: 'root' })
export class CorrelationService {
  private readonly current = signal(newCorrelationId());
  readonly id = this.current.asReadonly();

  constructor() {
    const doc = inject(DOCUMENT);
    const rotate = () => this.current.set(newCorrelationId());
    doc.addEventListener('pointerdown', rotate, { capture: true, passive: true });
    doc.addEventListener('submit', rotate, { capture: true });
    doc.addEventListener(
      'keydown',
      (e: KeyboardEvent) => {
        if (e.key === 'Enter' || e.key === ' ') rotate();
      },
      { capture: true, passive: true },
    );
  }

  /** Starts a new action explicitly (for programmatic flows such as "Overwrite" doing GET then PUT). */
  begin(): string {
    const id = newCorrelationId();
    this.current.set(id);
    return id;
  }
}
