import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { injectInfiniteQuery, injectQuery, keepPreviousData } from '@tanstack/angular-query';
import { ClaimsApi, ClaimFilter, claimKeys } from '../../api/claims.api';
import { ClaimDto, ClaimStatus } from '../../api/models';
import { StampPipe } from '../../ui/format';
import { Select, SelectOption } from '../../ui/select';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { livePoll } from './live-poll';
import { ClaimStatusPill, MoneyPipe } from './claim-bits';
import { ClaimSearchHits, SEARCH_OFF_NOTE, SEARCH_PLACEHOLDER, injectClaimSearch } from './claim-search';
import { ClaimsFilterStore } from './claims-filter.store';
import { filterToParams, hasFilterParams, paramsToFilter } from './claims-query-params';
import { MARKETS, ageDays } from './claims-logic';

const TABS: { v: ClaimFilter['status']; l: string }[] = [
  { v: 'open', l: 'Open' },
  { v: '', l: 'All' },
  { v: 'Submitted', l: 'Submitted' },
  { v: 'UnderReview', l: 'Under review' },
  { v: 'InfoRequested', l: 'Info requested' },
  { v: 'Approved', l: 'Approved' },
  { v: 'Rejected', l: 'Rejected' },
  { v: 'Settled', l: 'Settled' },
];

/** The staff queue: a KPI strip and status tabs (both with counts), filters, a rich table, keyset "Load more". Oldest first, so the longest waiting claim is on top. */
@Component({
  selector: 'fg-claims-queue-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, Select, RouterLink, ClaimStatusPill, ClaimSearchHits, MoneyPipe, StampPipe, SkeletonRows, EmptyState, ErrorState],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Claims queue</h1>
          <p class="page-lede">
            Open claims by default, the longest-waiting first. Pick up a submitted claim to work it; reserves are shown
            in each claim's own currency and are never added across currencies.
          </p>
        </div>
      </header>

      <section class="kpis" aria-label="Queue at a glance" data-testid="kpis" data-tour="queue-kpis">
        @for (k of kpis; track k.status) {
          <button
            type="button"
            class="kpi"
            [class.on]="filters.status() === k.status"
            (click)="filters.setStatus(k.status)"
          >
            <span class="kpi-n">{{ countLabel(k.status) }}</span>
            <span class="kpi-l">{{ k.label }}</span>
          </button>
        }
      </section>

      <div class="toolbar" role="search">
        <label class="search">
          <span class="sr-only">{{ placeholder }}</span>
          <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
            <circle cx="7" cy="7" r="4.5" fill="none" stroke="currentColor" stroke-width="1.6" />
            <path d="M10.5 10.5L14 14" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" />
          </svg>
          <input
            class="input"
            type="search"
            [placeholder]="placeholder"
            [value]="searchText()"
            (input)="searchText.set($any($event.target).value)"
          />
        </label>
        <div class="segmented" role="radiogroup" aria-label="Assigned to">
          @for (a of assignedOptions; track a.v) {
            <button
              type="button"
              role="radio"
              [attr.aria-checked]="filters.assigned() === a.v"
              (click)="filters.setAssigned(a.v)"
            >
              {{ a.l }}
            </button>
          }
        </div>
        <div class="inline-filter">
          <label class="sr-only" for="q-market">Market</label>
          <fg-select
            inputId="q-market"
            [options]="marketOptions"
            [ngModel]="filters.market()"
            (ngModelChange)="filters.setMarket($event)"
          />
        </div>
        <div class="inline-filter">
          <label class="sr-only" for="q-line">Line</label>
          <fg-select
            inputId="q-line"
            [options]="lineOptions"
            [ngModel]="filters.line()"
            (ngModelChange)="filters.setLine($event)"
          />
        </div>
        @if (query.isFetching() && !query.isPending()) {
          <span class="text-sm text-muted" aria-live="polite">Refreshing…</span>
        }
      </div>

      <div class="tabs" role="tablist" data-tour="queue-tabs" aria-label="Status">
        @for (t of tabs; track t.v) {
          <button
            type="button"
            role="tab"
            class="tab"
            [attr.aria-selected]="filters.status() === t.v"
            (click)="filters.setStatus(t.v)"
          >
            {{ t.l }} <span class="tab-n">{{ tabCount(t.v) }}</span>
          </button>
        }
      </div>

      @if (search.unavailable()) {
        <p class="note" role="status" data-testid="search-unavailable">{{ offNote }}</p>
      }

      <div class="table-wrap">
        @if (search.active()) {
          @if (search.query.isPending()) {
            <fg-skeleton-rows [rows]="4" [widths]="['16%', '16%', '30%', '10%', '10%']" />
          } @else {
            <fg-claim-search-hits [hits]="search.hits()" [total]="search.total()" [showClaimant]="true" />
          }
        } @else if (query.isPending()) {
          <fg-skeleton-rows [rows]="8" [widths]="['16%', '16%', '10%', '10%', '10%', '12%']" />
        } @else if (query.isError() && !rows().length) {
          <fg-error-state [error]="query.error()" (retry)="query.refetch()" />
        } @else if (!rows().length) {
          <fg-empty-state
            title="No claims match"
            body="Nothing in the queue matches this status and these filters. Widen them to see more."
          >
            <button type="button" class="btn" (click)="clear()">Clear filters</button>
          </fg-empty-state>
        } @else {
          <table class="table" [class.dim]="query.isPlaceholderData()" data-testid="queue-table">
            <thead>
              <tr>
                <th scope="col">Claim</th>
                <th scope="col">Claimant</th>
                <th scope="col">Market</th>
                <th scope="col">Line</th>
                <th scope="col">Status</th>
                <th scope="col" class="right">Reserve</th>
                <th scope="col" class="right">Age</th>
                <th scope="col">Officer</th>
              </tr>
            </thead>
            <tbody>
              @for (c of rows(); track c.id) {
                <tr class="row-link" (click)="open(c)">
                  <td>
                    <a class="num" [routerLink]="['/claims', c.id]" (click)="$event.stopPropagation()">{{
                      c.number
                    }}</a>
                    <span class="sub mono">{{ c.policyNumber }}</span>
                  </td>
                  <td>{{ c.claimantName || '—' }}</td>
                  <td>{{ c.market }}</td>
                  <td>{{ c.line }}</td>
                  <td><fg-claim-status [status]="c.status" /></td>
                  <td class="right mono">{{ c.reserveMinor | money: c.currency }}</td>
                  <td class="right" [title]="c.submittedAt | stamp">{{ age(c) }} d</td>
                  <td [class.text-muted]="!c.assignedOfficerName">{{ c.assignedOfficerName || 'Unassigned' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="table-foot">
            <span class="text-muted">{{ rows().length }} shown{{ query.hasNextPage() ? ', more available' : '' }}</span>
            @if (query.hasNextPage()) {
              <button type="button" class="btn" (click)="query.fetchNextPage()" [disabled]="query.isFetchingNextPage()">
                {{ query.isFetchingNextPage() ? 'Loading…' : 'Load more' }}
              </button>
            } @else {
              <span class="text-sm text-muted">End of list</span>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class ClaimsQueuePage {
  private readonly api = inject(ClaimsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly filters = inject(ClaimsFilterStore);

  protected readonly tabs = TABS;
  protected readonly marketOptions: SelectOption[] = [
    { value: '', label: 'All markets' },
    ...MARKETS.map((m) => ({ value: m.code, label: m.name })),
  ];
  protected readonly lineOptions: SelectOption[] = [
    { value: '', label: 'Motor and property' },
    { value: 'Motor', label: 'Motor' },
    { value: 'Property', label: 'Property' },
  ];
  protected readonly assignedOptions: { v: ClaimFilter['assigned']; l: string }[] = [
    { v: 'any', l: 'Everyone' },
    { v: 'me', l: 'Assigned to me' },
    { v: 'none', l: 'Unassigned' },
  ];
  /** The four numbers the queue is run by (Submitted is by definition unassigned). */
  protected readonly kpis: { status: ClaimStatus; label: string }[] = [
    { status: 'Submitted', label: 'Submitted, unassigned' },
    { status: 'UnderReview', label: 'Under review' },
    { status: 'InfoRequested', label: 'Info requested' },
    { status: 'Approved', label: 'Approved, awaiting settlement' },
  ];

  protected readonly searchText = signal(this.filters.q());
  protected readonly placeholder = SEARCH_PLACEHOLDER;
  protected readonly offNote = SEARCH_OFF_NOTE;
  protected readonly search = injectClaimSearch(() => this.searchText());

  private readonly filter = computed<ClaimFilter>(() => ({
    status: this.filters.status(),
    assigned: this.filters.assigned(),
    market: this.filters.market(),
    line: this.filters.line(),
    q: this.filters.q(),
    officerId: this.filters.officerId(),
    minAgeDays: this.filters.minAgeDays(),
    maxAgeDays: this.filters.maxAgeDays(),
  }));

  private readonly live = livePoll();
  protected readonly query = injectInfiniteQuery(() => ({
    queryKey: claimKeys.list(this.filter()),
    queryFn: ({ pageParam }) => this.api.list(this.filter(), pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    placeholderData: keepPreviousData,
    ...this.live,
  }));
  protected readonly rows = computed(() => this.query.data()?.pages.flatMap((p) => p.items) ?? []);

  /** Counts follow the assigned / market / line / search filters but not the tab, so every tab shows what it would hold. */
  private readonly counts = injectQuery(() => {
    const { assigned, market, line, q, officerId, minAgeDays, maxAgeDays } = this.filter();
    const rest = { assigned, market, line, q, officerId, minAgeDays, maxAgeDays };
    return {
      queryKey: claimKeys.counts(rest),
      queryFn: () => this.api.counts(rest),
      placeholderData: keepPreviousData,
      ...this.live,
    };
  });

  constructor() {
    // The URL is the source when it carries filters (a link from the team page); the filters then write themselves back.
    const params = this.route.snapshot.queryParams;
    if (hasFilterParams(params)) {
      this.filters.setAll(paramsToFilter(params));
      this.searchText.set(this.filters.q());
    }
    effect(() => {
      const target = filterToParams(this.filter());
      untracked(() => this.router.navigate([], { relativeTo: this.route, queryParams: target, replaceUrl: true }));
    });
    effect((onCleanup) => {
      const text = this.searchText();
      const t = setTimeout(() => untracked(() => this.filters.setSearch(text)), 280);
      onCleanup(() => clearTimeout(t));
    });
  }

  protected countLabel(status: ClaimStatus): string {
    const c = this.counts.data()?.[status];
    return c ? `${c.n}${c.more ? '+' : ''}` : '…';
  }

  protected tabCount(status: ClaimFilter['status']): string {
    const data = this.counts.data();
    if (!data) return '';
    if (status === 'open') {
      const open = [data.Submitted, data.UnderReview, data.InfoRequested];
      return `${open.reduce((sum, c) => sum + c.n, 0)}${open.some((c) => c.more) ? '+' : ''}`;
    }
    if (status) return this.countLabel(status);
    const all = Object.values(data);
    return `${all.reduce((sum, c) => sum + c.n, 0)}${all.some((c) => c.more) ? '+' : ''}`;
  }

  protected age(c: ClaimDto): number {
    return ageDays(c.submittedAt);
  }

  protected open(c: ClaimDto): void {
    void this.router.navigate(['/claims', c.id]);
  }

  protected clear(): void {
    this.searchText.set('');
    this.filters.reset();
  }
}
