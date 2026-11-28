import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { injectInfiniteQuery } from '@tanstack/angular-query';
import { ClaimsApi, EMPTY_CLAIM_FILTER, claimKeys } from '../../api/claims.api';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { livePoll } from './live-poll';
import { ClaimStatusPill } from './claim-bits';
import { ClaimSearchHits, SEARCH_OFF_NOTE, SEARCH_PLACEHOLDER, injectClaimSearch } from './claim-search';
import { marketName } from './claims-logic';

/** The claimant's own claims, newest first (the API scopes the list; there is no owner filter to send). */
@Component({
  selector: 'fg-my-claims-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ClaimStatusPill, ClaimSearchHits, RelativeTimePipe, StampPipe, SkeletonRows, EmptyState, ErrorState],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">My claims</h1>
          <p class="page-lede">
            Every claim you have reported, newest first. Open one to see where it stands and to answer any question we
            have for you.
          </p>
        </div>
        <a routerLink="/claims/new" class="btn btn-primary">Report an incident</a>
      </header>

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
      </div>

      @if (search.unavailable()) {
        <p class="note" role="status" data-testid="search-unavailable">{{ offNote }}</p>
      }

      <div class="table-wrap">
        @if (search.active()) {
          @if (search.query.isPending()) {
            <fg-skeleton-rows [rows]="3" />
          } @else {
            <fg-claim-search-hits [hits]="search.hits()" [total]="search.total()" />
          }
        } @else if (query.isPending()) {
          <fg-skeleton-rows [rows]="5" />
        } @else if (query.isError() && !rows().length) {
          <fg-error-state [error]="query.error()" (retry)="query.refetch()" />
        } @else if (!rows().length) {
          <fg-empty-state
            title="You have not reported a claim yet"
            body="If something has happened to your car or your property, report it here. It takes a few minutes: you tell us what happened and when, and an officer takes it from there."
          >
            <a routerLink="/claims/new" class="btn btn-primary">Report an incident</a>
          </fg-empty-state>
        } @else {
          <table class="table">
            <thead>
              <tr>
                <th scope="col">Claim</th>
                <th scope="col">Line</th>
                <th scope="col">Market</th>
                <th scope="col">Status</th>
                <th scope="col">Submitted</th>
                <th scope="col">Last update</th>
              </tr>
            </thead>
            <tbody>
              @for (c of rows(); track c.id) {
                <tr class="row-link" (click)="open(c.id)">
                  <td>
                    <a class="num" [routerLink]="['/claims', c.id]" (click)="$event.stopPropagation()">{{
                      c.number
                    }}</a>
                    <span class="sub">Policy {{ c.policyNumber }}</span>
                  </td>
                  <td>{{ c.line }}</td>
                  <td>{{ market(c.market) }}</td>
                  <td><fg-claim-status [status]="c.status" /></td>
                  <td class="text-muted" [title]="c.submittedAt | stamp">{{ c.submittedAt | relative }}</td>
                  <td class="text-muted" [title]="c.updatedAt | stamp">{{ c.updatedAt | relative }}</td>
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
export class MyClaimsPage {
  private readonly api = inject(ClaimsApi);
  private readonly router = inject(Router);

  protected readonly searchText = signal('');
  protected readonly placeholder = SEARCH_PLACEHOLDER;
  protected readonly offNote = SEARCH_OFF_NOTE;
  protected readonly search = injectClaimSearch(() => this.searchText());

  private readonly live = livePoll();
  protected readonly query = injectInfiniteQuery(() => ({
    queryKey: claimKeys.listPaged(EMPTY_CLAIM_FILTER),
    queryFn: ({ pageParam }) => this.api.list(EMPTY_CLAIM_FILTER, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    ...this.live,
  }));

  protected readonly rows = computed(() => this.query.data()?.pages.flatMap((p) => p.items) ?? []);
  protected readonly market = marketName;

  protected open(id: string): void {
    void this.router.navigate(['/claims', id]);
  }
}
