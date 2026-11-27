import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuditApi, EMPTY_AUDIT_FILTER } from '../../api/audit.api';
import { ClaimsApi } from '../../api/claims.api';
import { AccessService } from '../../core/auth/access.service';
import { AuditFilterStore } from '../audit/audit-filter.store';
import { ClaimStatusPill } from '../claims/claim-bits';
import type { HighlightSegment } from '../audit/highlight';
import {
  ClaimRow,
  DROPDOWN_HITS,
  SearchRow,
  facetSummary,
  isSearchOff,
  toClaimRows,
  toRows,
} from './global-search-logic';

type State = 'idle' | 'loading' | 'ok' | 'off' | 'error';

/**
 * Highlight segments as text, matched words in <em>. Text bindings only, so no markup from a claim ever reaches the DOM.
 * Every segment is an element: Angular drops whitespace-only text between elements but pads a bare interpolation with the
 * template's line breaks, which would put spaces inside "CLM-<em>SG</em>-2026".
 */
@Component({
  selector: 'fg-gs-hl',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    em {
      font-style: normal;
      font-weight: 600;
      color: var(--text);
      background: var(--accent-soft);
      border-radius: 3px;
    }
  `,
  template: `@for (s of segments(); track $index) {
    @if (s.em) {
      <em>{{ s.text }}</em>
    } @else {
      <span>{{ s.text }}</span>
    }
  }`,
})
class Highlighted {
  readonly segments = input.required<HighlightSegment[]>();
}

/** The header search: a Claims group for anyone with CLAIMS_READ (scoped server-side) and an Audit group for AUDIT_READ. Typing (debounced) or Enter opens the dropdown. */
@Component({
  selector: 'fg-global-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ClaimStatusPill, Highlighted],
  host: { '(document:click)': 'outside($event)', '(keydown.escape)': 'close()' },
  styleUrl: './global-search.css',
  template: `
    <div class="gs" role="search">
      <input
        type="search"
        class="gs-input"
        [placeholder]="placeholder()"
        [attr.aria-label]="placeholder()"
        autocomplete="off"
        data-testid="global-search"
        [value]="q()"
        (input)="typed($any($event.target).value)"
        (keydown.enter)="run(true)"
      />
      <span class="gs-badge" title="Meilisearch-powered search">Meilisearch</span>
      @if (open()) {
        <div class="gs-menu" role="group" aria-label="Search results" data-testid="global-search-menu">
          @if (canClaims) {
            <p class="gs-head">Claims</p>
            @switch (claimState()) {
              @case ('loading') {
                <p class="gs-note">Searching</p>
              }
              @case ('off') {
                <p class="gs-note" data-testid="claims-search-off">Search is unavailable</p>
              }
              @case ('error') {
                <p class="gs-note">Search failed. Try again.</p>
              }
              @case ('ok') {
                @if (claimRows().length === 0) {
                  <p class="gs-note">No matching claims</p>
                } @else if (claimFacets()) {
                  <p class="gs-facets" data-testid="claim-facets" title="Matches by market, line and status">
                    {{ claimTotal() }} {{ claimTotal() === 1 ? 'match' : 'matches' }} · {{ claimFacets() }}
                  </p>
                }
                @for (c of claimRows(); track c.id) {
                  <button type="button" class="gs-row" data-testid="claim-hit" (click)="openClaim(c)">
                    <span class="gs-action"
                      ><fg-gs-hl [segments]="c.number" /> <fg-claim-status [status]="c.status"
                    /></span>
                    <span class="gs-meta"><fg-gs-hl [segments]="c.meta" /></span>
                    <span class="gs-meta gs-line"><fg-gs-hl [segments]="c.description" /></span>
                    @if (c.matched.length) {
                      <span class="gs-meta gs-why" data-testid="claim-why">
                        Matched on
                        @for (m of c.matched; track m.label) {
                          <span class="gs-why-item">{{ m.label }}: <fg-gs-hl [segments]="m.text" /></span>
                        }
                      </span>
                    }
                  </button>
                }
              }
            }
          }
          @if (canAudit) {
            <p class="gs-head">Audit</p>
            @switch (state()) {
              @case ('loading') {
                <p class="gs-note">Searching</p>
              }
              @case ('off') {
                <p class="gs-note" data-testid="search-off">Search is unavailable</p>
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
                <button type="button" class="gs-all" (click)="seeAll()">See all audit results</button>
              }
            }
          }
        </div>
      }
    </div>
  `,
})
export class GlobalSearch implements OnDestroy {
  private readonly api = inject(AuditApi);
  private readonly claimsApi = inject(ClaimsApi);
  private readonly access = inject(AccessService);
  private readonly store = inject(AuditFilterStore);
  private readonly router = inject(Router);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private seq = 0;

  protected readonly q = signal('');
  protected readonly open = signal(false);
  protected readonly state = signal<State>('idle');
  protected readonly rows = signal<SearchRow[]>([]);
  protected readonly claimState = signal<State>('idle');
  protected readonly claimRows = signal<ClaimRow[]>([]);
  protected readonly claimFacets = signal('');
  protected readonly claimTotal = signal(0);
  protected get canClaims(): boolean {
    return this.access.has('CLAIMS_READ');
  }
  protected get canAudit(): boolean {
    return this.access.has('AUDIT_READ');
  }
  protected readonly placeholder = () =>
    this.canClaims ? 'Search claims: number, policy, place' : 'Search the audit trail';

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
    this.state.set(this.canAudit ? 'loading' : 'idle');
    this.claimState.set(this.canClaims ? 'loading' : 'idle');
    const claims = this.canClaims
      ? this.claimsApi.search(q, DROPDOWN_HITS).then(
          (result) => {
            if (mine !== this.seq) return;
            this.claimRows.set(toClaimRows(result));
            this.claimFacets.set(facetSummary(result));
            this.claimTotal.set(result.estimatedTotalHits);
            this.claimState.set('ok');
          },
          (e) => {
            if (mine === this.seq) this.claimState.set(isSearchOff(e) ? 'off' : 'error');
          },
        )
      : Promise.resolve();
    const audit = this.canAudit
      ? this.api.search({ ...EMPTY_AUDIT_FILTER, q }, DROPDOWN_HITS).then(
          (result) => {
            if (mine !== this.seq) return;
            this.rows.set(toRows(result));
            this.state.set('ok');
          },
          (e) => {
            if (mine === this.seq) this.state.set(isSearchOff(e) ? 'off' : 'error');
          },
        )
      : Promise.resolve();
    await Promise.all([claims, audit]);
  }

  protected openClaim(c: ClaimRow): void {
    this.close();
    void this.router.navigate(['/claims', c.id]);
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
