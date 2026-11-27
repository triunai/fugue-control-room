import {
  afterEveryRender,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
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
        <div class="panel" #panel (scroll)="measure()">
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
                      <span class="path">{{ c.path }}</span>
                      <!-- The full path on hover, a child of the row: it moves and goes with the row, unlike a native title tooltip. -->
                      <span class="tip" aria-hidden="true" data-testid="hood-call-tip">{{ c.path }}</span>
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
            @if (n.flow?.length) {
              <section aria-labelledby="hood-flow">
                <h3 id="hood-flow" class="sec">What happens</h3>
                <ol class="flow" data-testid="hood-flow">
                  @for (s of n.flow; track $index) {
                    <li class="step">
                      <span class="fl-layer" [attr.data-layer]="s.layer">{{ s.layer }}</span>
                      <span class="fl-text">{{ s.text }}</span>
                      @if (s.path) {
                        <a class="fl-path" [href]="url(s.path)" target="_blank" rel="noopener noreferrer">{{
                          fileName(s.path)
                        }}</a>
                      }
                    </li>
                  }
                </ol>
              </section>
            }
            <section aria-labelledby="hood-how">
              <h3 id="hood-how" class="sec">How the API does it</h3>
              <ul class="how" data-testid="hood-how">
                @for (line of n.how; track $index) {
                  <li>{{ line }}</li>
                }
              </ul>
            </section>
            <section aria-labelledby="hood-guards">
              <h3 id="hood-guards" class="sec">Guards</h3>
              <ul class="guards" data-testid="hood-guards">
                @for (g of n.guards; track g.status + g.name) {
                  <li>
                    <details class="guard" (toggle)="measure()">
                      <summary class="chip">
                        <span class="gs">{{ g.status }}</span> {{ g.name }}
                      </summary>
                      <pre class="src"><code>{{ g.code }}</code></pre>
                      @if (g.note) {
                        <p class="gnote">{{ g.note }}</p>
                      }
                      <a class="gpath" [href]="url(g.path)" target="_blank" rel="noopener noreferrer">{{ g.path }}</a>
                    </details>
                  </li>
                }
              </ul>
            </section>
            <section aria-labelledby="hood-tests">
              <h3 id="hood-tests" class="sec">Tested by</h3>
              <p class="muted">
                dotnet test (xUnit, Testcontainers Postgres) · npm test (Vitest) · npx bru run --env compose · the
                pre-push hook builds and runs the dotnet tests.
              </p>
              <ul class="tests" data-testid="hood-tests">
                @for (x of n.tests; track x.path + x.name) {
                  <li>
                    <span class="layer">{{ x.layer }}</span>
                    <a class="tname" [href]="url(x.path)" target="_blank" rel="noopener noreferrer">{{ x.name }}</a>
                    <p class="gnote">{{ x.asserts }}</p>
                  </li>
                }
              </ul>
            </section>
            <section aria-labelledby="hood-dec">
              <h3 id="hood-dec" class="sec">Decisions</h3>
              <ul class="links">
                @for (a of n.adrs; track a.path) {
                  <li class="adr">
                    <a [href]="url(a.path)" target="_blank" rel="noopener noreferrer">{{ a.label }}</a>
                  </li>
                }
                @for (d of n.decisions; track d.path + d.label) {
                  <li>
                    <a [href]="url(d.path)" target="_blank" rel="noopener noreferrer">{{ d.label }}</a>
                  </li>
                }
              </ul>
            </section>
          }
        </div>
        <div class="fade" [class.on]="more()" aria-hidden="true" data-testid="hood-fade"></div>
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

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  /** True while the panel has more content below the visible edge (drives the bottom fade). */
  protected readonly more = signal(false);

  private page = pagePath(this.router.url);

  constructor() {
    afterEveryRender(() => this.measure());
    // "Requests made by THIS page since it opened": a new path starts a new log (a filter change in the URL does not).
    this.router.events.pipe(filter((e): e is NavigationStart => e instanceof NavigationStart)).subscribe((e) => {
      const next = pagePath(e.url);
      if (next !== this.page) {
        this.page = next;
        this.log.clear();
      }
    });
  }

  protected measure(): void {
    const el = this.panel()?.nativeElement;
    this.more.set(!!el && el.scrollHeight - el.scrollTop - el.clientHeight > 4);
  }

  protected cls(c: ApiCall): string {
    return statusClass(c.status);
  }

  protected url(path: string): string {
    return hoodUrl(path);
  }

  /** The last path segment, e.g. `AuditController.cs`; the link carries the full path. */
  protected fileName(path: string): string {
    return path.split('#', 1)[0].split('/').pop() ?? path;
  }

  protected onKey(e: KeyboardEvent): void {
    if (e.key !== '.' || e.ctrlKey || e.metaKey || e.altKey || e.defaultPrevented || e.repeat) return;
    if (isTypingTarget(e.target)) return;
    this.state.toggle();
  }
}
