import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuditApi, EMPTY_AUDIT_FILTER } from '../../api/audit.api';
import { AuditFilterStore } from '../audit/audit-filter.store';
import { DROPDOWN_HITS, SearchRow, isSearchOff, toRows } from './global-search-logic';

type State = 'idle' | 'loading' | 'ok' | 'off' | 'error';

/** The header search over the audit trail (the audit search API). Typing (debounced) or Enter opens the dropdown. */
@Component({
  selector: 'fg-global-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:click)': 'outside($event)', '(keydown.escape)': 'close()' },
  styleUrl: './global-search.css',
  template: `
    <div class="gs" role="search">
      <input
        type="search"
        class="gs-input"
        placeholder="Search the audit trail"
        aria-label="Search the audit trail"
        autocomplete="off"
        data-testid="global-search"
        [value]="q()"
        (input)="typed($any($event.target).value)"
        (keydown.enter)="run(true)"
      />
      <span class="gs-badge" title="Meilisearch-powered search">Meilisearch</span>
      @if (open()) {
        <div class="gs-menu" role="group" aria-label="Audit search results" data-testid="global-search-menu">
          @switch (state()) {
            @case ('loading') {
              <p class="gs-note">Searching</p>
            }
            @case ('off') {
              <p class="gs-note" data-testid="search-off">Search is off</p>
            }
            @case ('error') {
              <p class="gs-note">Search failed. Try again.</p>
            }
            @case ('ok') {
              @if (rows().length === 0) {
                <p class="gs-note">No matches</p>
              } @else {
                @for (r of rows(); track r.id) {
                  <button type="button" class="gs-row" (click)="openRow(r)">
                    <span class="gs-action">
                      @for (s of r.action; track $index) {
                        @if (s.em) {
                          <em>{{ s.text }}</em>
                        } @else {
                          {{ s.text }}
                        }
                      }
                    </span>
                    <span class="gs-meta"
                      >{{ r.actor }} · {{ r.outcome }}{{ r.entityType ? ' · ' + r.entityType : '' }}</span
                    >
                  </button>
                }
              }
              <button type="button" class="gs-all" (click)="seeAll()">See all results</button>
            }
          }
        </div>
      }
    </div>
  `,
})
export class GlobalSearch implements OnDestroy {
  private readonly api = inject(AuditApi);
  private readonly store = inject(AuditFilterStore);
  private readonly router = inject(Router);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private seq = 0;

  protected readonly q = signal('');
  protected readonly open = signal(false);
  protected readonly state = signal<State>('idle');
  protected readonly rows = signal<SearchRow[]>([]);

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  protected typed(value: string): void {
    this.q.set(value);
    clearTimeout(this.timer);
    if (!value.trim()) return this.close();
    this.timer = setTimeout(() => void this.run(false), 300);
  }

  protected async run(now: boolean): Promise<void> {
    const q = this.q().trim();
    if (!q) return;
    if (now) clearTimeout(this.timer);
    const mine = ++this.seq;
    this.open.set(true);
    this.state.set('loading');
    try {
      const result = await this.api.search({ ...EMPTY_AUDIT_FILTER, q }, DROPDOWN_HITS);
      if (mine !== this.seq) return;
      this.rows.set(toRows(result));
      this.state.set('ok');
    } catch (e) {
      if (mine !== this.seq) return;
      this.state.set(isSearchOff(e) ? 'off' : 'error');
    }
  }

  protected openRow(r: SearchRow): void {
    if (r.entityId) this.store.byEntity(r.entityType, r.entityId);
    else this.store.apply({ ...EMPTY_AUDIT_FILTER, q: this.q().trim() });
    this.close();
    void this.router.navigateByUrl('/audit');
  }

  protected seeAll(): void {
    this.store.apply({ ...EMPTY_AUDIT_FILTER, q: this.q().trim() });
    this.close();
    void this.router.navigateByUrl('/audit');
  }

  protected close(): void {
    this.seq++;
    this.open.set(false);
  }

  protected outside(e: Event): void {
    if (!this.element.nativeElement.contains(e.target as Node)) this.close();
  }
}
