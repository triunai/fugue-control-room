import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { ApiCall, ApiCallLog } from '../../core/http/api-call-log';
import { HoodNote, hoodUrl, noteFor, pagePath } from './hood-note';
import { HoodState, isTypingTarget } from './hood-state';

/** Colour class by status family. */
const statusClass = (s: number) => (s >= 500 || s === 0 ? 's5' : s >= 400 ? 's4' : s >= 300 ? 's3' : 's2');

/**
 * "Under the hood": a docked right column. Open it shows the live API calls of the page, why the page works as it
 * does, the decisions behind it and its guards. Minimised it is a faint edge strip with one opaque tab.
 */
@Component({
  selector: 'fg-hood',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './hood-sidebar.css',
  host: {
    '[class.min]': '!state.open()',
    '(document:keydown)': 'onKey($event)',
  },
  template: `
    <aside class="hood" aria-label="Under the hood" data-testid="hood">
      <div class="bg"></div>
      @if (state.open()) {
        <div class="panel">
          <header class="head">
            <h2 class="title"><span class="glyph" aria-hidden="true">&lt;/&gt;</span> Under the hood</h2>
            <button
              type="button"
              class="min-btn"
              data-testid="hood-minimise"
              aria-label="Minimise under the hood"
              title="Minimise (.)"
              (click)="state.toggle()"
            >
              &rsaquo;
            </button>
          </header>

          <section aria-labelledby="hood-calls">
            <h3 id="hood-calls" class="sec">API calls <span class="live">live</span></h3>
            @if (log.calls().length === 0) {
              <p class="muted">No calls yet from this page.</p>
            } @else {
              <ol class="calls" data-testid="hood-calls">
                @for (c of log.calls(); track c.seq) {
                  <li class="call">
                    <div class="line">
                      <span class="method">{{ c.method }}</span>
                      <span class="path" [title]="c.path">{{ c.path }}</span>
                    </div>
                    <div class="line meta">
                      <span class="status" [class]="cls(c)">{{ c.status || 'ERR' }}</span>
                      <span class="ms">{{ c.ms }} ms</span>
                      @if (c.ifMatch) {
                        <span class="mark">If-Match</span>
                      }
                      @if (c.etag) {
                        <span class="mark">ETag</span>
                      }
                      @if (c.idempotencyKey) {
                        <span class="mark">Idempotency-Key</span>
                      }
                    </div>
                    @if (c.code) {
                      <div class="code">{{ c.code }}</div>
                    }
                  </li>
                }
              </ol>
            }
          </section>

          @if (note(); as n) {
            <section aria-labelledby="hood-why">
              <h3 id="hood-why" class="sec">Why this page</h3>
              @for (line of n.why; track $index) {
                <p class="why">{{ line }}</p>
              }
            </section>
            <section aria-labelledby="hood-dec">
              <h3 id="hood-dec" class="sec">Decisions</h3>
              <ul class="links">
                @for (d of n.decisions; track d.path + d.label) {
                  <li>
                    <a [href]="url(d.path)" target="_blank" rel="noopener noreferrer">{{ d.label }}</a>
                  </li>
                }
              </ul>
            </section>
            <section aria-labelledby="hood-guards">
              <h3 id="hood-guards" class="sec">Guards</h3>
              <ul class="chips">
                @for (g of n.guards; track g) {
                  <li class="chip">{{ g }}</li>
                }
              </ul>
            </section>
          }
        </div>
      } @else {
        <button
          type="button"
          class="tab"
          data-testid="hood-tab"
          aria-label="Open under the hood"
          title="Under the hood (.)"
          (click)="state.toggle()"
        >
          <span aria-hidden="true">&lt;/&gt; &lsaquo;</span>
        </button>
      }
    </aside>
  `,
})
export class HoodSidebar {
  protected readonly state = inject(HoodState);
  protected readonly log = inject(ApiCallLog);
  private readonly router = inject(Router);

  /** The note of the page on screen (null: the page only shows its live calls). */
  protected readonly note = toSignal<HoodNote | null>(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => noteFor(this.router.routerState.snapshot.root)),
    ),
    { initialValue: null },
  );

  private page = pagePath(this.router.url);

  constructor() {
    // "Requests made by THIS page since it opened": a new path starts a new log (a filter change in the URL does not).
    this.router.events.pipe(filter((e): e is NavigationStart => e instanceof NavigationStart)).subscribe((e) => {
      const next = pagePath(e.url);
      if (next !== this.page) {
        this.page = next;
        this.log.clear();
      }
    });
  }

  protected cls(c: ApiCall): string {
    return statusClass(c.status);
  }

  protected url(path: string): string {
    return hoodUrl(path);
  }

  protected onKey(e: KeyboardEvent): void {
    if (e.key !== '.' || e.ctrlKey || e.metaKey || e.altKey || e.defaultPrevented || e.repeat) return;
    if (isTypingTarget(e.target)) return;
    this.state.toggle();
  }
}
