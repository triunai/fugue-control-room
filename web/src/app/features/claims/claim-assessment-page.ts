import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { QueryClient, injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { ClaimDto } from '../../api/models';
import { ErrorState } from '../../ui/states';
import { ClaimAssessmentSheet } from './claim-assessment-sheet';

/** A small host for the assessment worksheet (`/claims/:id/assessment`) until the claim record embeds it. */
@Component({
  selector: 'fg-claim-assessment-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ClaimAssessmentSheet, ErrorState, RouterLink],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Assessment worksheet</h1>
          @if (query.data(); as c) {
            <p class="page-lede">
              <a [routerLink]="['/claims', c.id]" class="mono">{{ c.number }}</a> · {{ c.market }} · {{ c.currency }}
            </p>
          }
        </div>
      </header>
      @if (query.isError()) {
        <div class="panel"><fg-error-state [error]="query.error()" (retry)="query.refetch()" /></div>
      } @else if (query.data(); as c) {
        <fg-claim-assessment-sheet [claim]="c" (reserved)="saved($event)" />
      }
    </div>
  `,
})
export class ClaimAssessmentPage {
  private readonly api = inject(ClaimsApi);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  private readonly queries = inject(QueryClient);

  protected readonly query = injectQuery(() => ({
    queryKey: claimKeys.one(this.id),
    queryFn: () => this.api.get(this.id),
  }));

  protected saved(claim: ClaimDto): void {
    this.queries.setQueryData(claimKeys.one(this.id), claim);
  }
}
