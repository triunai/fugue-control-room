import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router } from '@angular/router';

export type ProgressPhase = 'idle' | 'running' | 'finishing' | 'fading';

/** A route change that commits inside this window never shows the bar. */
export const SHOW_DELAY_MS = 120;
const FINISH_MS = 200;

/** Next phase when a navigation ends while the bar is in `phase`: only a visible bar finishes and fades. */
export function onNavigationEnd(phase: ProgressPhase): ProgressPhase {
  return phase === 'running' ? 'finishing' : phase === 'finishing' ? 'finishing' : 'idle';
}

/**
 * A thin bar along the top edge while a route change is pending: it appears after a short delay (so a fast
 * change shows nothing), trickles toward 85%, completes when the route commits, then fades. Pointer-transparent.
 */
@Component({
  selector: 'fg-nav-progress',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="bar" [attr.data-phase]="phase()" aria-hidden="true"></div>`,
  styles: `
    :host {
      position: fixed;
      inset: 0 0 auto 0;
      z-index: 100;
      height: 2px;
      pointer-events: none;
    }
    .bar {
      height: 100%;
      background: var(--accent);
      transform-origin: left;
      transform: scaleX(0);
      opacity: 0;
    }
    .bar[data-phase='running'] {
      opacity: 1;
      transform: scaleX(0.85);
      transition: transform 8s cubic-bezier(0.16, 1, 0.3, 1);
    }
    .bar[data-phase='finishing'] {
      opacity: 1;
      transform: scaleX(1);
      transition: transform 0.2s ease-out;
    }
    .bar[data-phase='fading'] {
      opacity: 0;
      transform: scaleX(1);
      transition: opacity 0.2s ease-out;
    }
    @media (prefers-reduced-motion: reduce) {
      .bar[data-phase] {
        transition: none;
        transform: none;
      }
      .bar[data-phase='running'],
      .bar[data-phase='finishing'] {
        transform: scaleX(1);
      }
    }
  `,
})
export class NavProgress {
  protected readonly phase = signal<ProgressPhase>('idle');
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    const router = inject(Router);
    const sub = router.events.subscribe((e) => {
      if (e instanceof NavigationStart) {
        clearTimeout(this.timer);
        this.timer = setTimeout(() => this.phase.set('running'), SHOW_DELAY_MS);
      } else if (e instanceof NavigationEnd || e instanceof NavigationCancel || e instanceof NavigationError) {
        clearTimeout(this.timer);
        const next = onNavigationEnd(this.phase());
        this.phase.set(next);
        if (next === 'finishing') {
          this.timer = setTimeout(() => {
            this.phase.set('fading');
            this.timer = setTimeout(() => this.phase.set('idle'), FINISH_MS);
          }, FINISH_MS);
        }
      }
    });
    inject(DestroyRef).onDestroy(() => {
      sub.unsubscribe();
      clearTimeout(this.timer);
    });
  }
}
