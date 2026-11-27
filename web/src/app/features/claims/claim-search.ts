import { ChangeDetectionStrategy, Component, computed, effect, input, signal, untracked, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { ClaimSearchHitDto } from '../../api/generated/model';
import { isApiProblem } from '../../core/http/problem';
import { ClaimStatusPill } from './claim-bits';

/** How long typing pauses before the search runs (the same pause the queue filter uses). */
export const CLAIM_SEARCH_DEBOUNCE_MS = 280;

/**
 * Full-text claim search for a claims screen (D-063). `text` is the box's live value; the query runs on its debounced, trimmed value. The server scopes
 * hits to the caller, so there is nothing to filter here. A 503 (`search.unavailable`) means "no engine": `active` goes false and the page shows
 * its normal list with a note. Call in an injection context.
 */
export function injectClaimSearch(text: () => string) {
  const api = inject(ClaimsApi);
  const term = signal('');
  effect((onCleanup) => {
    const value = text().trim();
    const t = setTimeout(() => untracked(() => term.set(value)), value === '' ? 0 : CLAIM_SEARCH_DEBOUNCE_MS);
    onCleanup(() => clearTimeout(t));
  });
  // No retries, so a 503 (engine off) falls back at once.
  const query = injectQuery(() => ({
    queryKey: claimKeys.search(term()),
    queryFn: () => api.search(term()),
    enabled: term() !== '',
    retry: false,
  }));
  const unavailable = computed(() => {
    const e = query.error();
    return isApiProblem(e) && e.status === 503;
  });
  return {
    query,
    unavailable,
    /** Search mode: a term is set and the engine did not answer 503. */
    active: computed(() => term() !== '' && !unavailable()),
    hits: computed(() => query.data()?.hits ?? []),
    total: computed(() => query.data()?.estimatedTotalHits ?? 0),
  };
}

/** The search results as queue-style rows, each linking to its claim. `showClaimant` is for staff (the API only sends the name to them). */
@Component({
  selector: 'fg-claim-search-hits',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ClaimStatusPill],
  styleUrl: './claims.css',
  template: `
    @if (hits().length) {
      <table class="table" data-testid="search-hits">
        <thead>
          <tr>
            <th scope="col">Claim</th>
            @if (showClaimant()) {
              <th scope="col">Claimant</th>
            }
            <th scope="col">Where and what</th>
            <th scope="col">Line</th>
            <th scope="col">Market</th>
            <th scope="col">Status</th>
          </tr>
        </thead>
        <tbody>
          @for (h of hits(); track h.id) {
            <tr class="row-link" (click)="open(h)">
              <td>
                <a class="num" [routerLink]="['/claims', h.id]" (click)="$event.stopPropagation()">{{ h.number }}</a>
                <span class="sub mono">{{ h.policyNumber }}</span>
              </td>
              @if (showClaimant()) {
                <td>{{ h.claimantName || '—' }}</td>
              }
              <td>
                {{ h.incidentLocation || '—' }}
                <span class="sub">{{ h.incidentDescription }}</span>
              </td>
              <td>{{ h.line }}</td>
              <td>{{ h.market }}</td>
              <td><fg-claim-status [status]="h.status" /></td>
            </tr>
          }
        </tbody>
      </table>
      <div class="table-foot">
        <span class="text-muted" data-testid="search-total">{{ hits().length }} of about {{ total() }} matches</span>
      </div>
    } @else {
      <p class="note" data-testid="search-empty">No claim matches that search.</p>
    }
  `,
})
export class ClaimSearchHits {
  private readonly router = inject(Router);
  readonly hits = input.required<ClaimSearchHitDto[]>();
  readonly total = input(0);
  readonly showClaimant = input(false);

  protected open(h: ClaimSearchHitDto): void {
    void this.router.navigate(['/claims', h.id]);
  }
}

export const SEARCH_PLACEHOLDER = 'Search by claim number, policy, place or description';
export const SEARCH_OFF_NOTE = 'Search is not available on this server, so this is the plain list.';
