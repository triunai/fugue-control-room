import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';

/** Team workload: per officer what is open, how old, what was decided; the unassigned count; ageing bands as CSS bars. */
@Component({
  selector: 'fg-workload-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RelativeTimePipe, StampPipe, SkeletonRows, EmptyState, ErrorState],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Workload</h1>
          <p class="page-lede">
            Who is carrying what. Open means under review or waiting on the claimant. Read live from the claims, so it
            is as fresh as the last transaction.
          </p>
        </div>
        @if (query.data(); as w) {
          <span class="text-muted text-sm" [title]="w.asOf | stamp">as of {{ w.asOf | relative }}</span>
        }
      </header>

      @if (query.isPending()) {
        <div class="table-wrap"><fg-skeleton-rows [rows]="5" /></div>
      } @else if (query.isError()) {
        <div class="panel"><fg-error-state [error]="query.error()" (retry)="query.refetch()" /></div>
      } @else if (query.data(); as w) {
        <section class="kpis kpis-2" aria-label="Unassigned and open" data-testid="workload-kpis">
          <div class="kpi static">
            <span class="kpi-n">{{ w.unassignedCount }}</span>
            <span class="kpi-l">Unassigned (waiting to be picked up)</span>
          </div>
          <div class="kpi static">
            <span class="kpi-n">{{ openTotal() }}</span>
            <span class="kpi-l">Open with an officer</span>
          </div>
        </section>

        <section class="panel panel-pad" aria-labelledby="age-h">
          <h2 id="age-h" class="section-title">Ageing of open claims</h2>
          <p class="mt-1 mb-3 text-muted">Days since the claim was submitted.</p>
          <ul class="bars" role="list" data-testid="ageing">
            @for (b of w.ageing; track b.label) {
              <li>
                <span class="bar-label">{{ b.label }} days</span>
                <span class="bar-track" aria-hidden="true"
                  ><span class="bar-fill" [style.width.%]="pct(b.count)"></span
                ></span>
                <span class="bar-n">{{ b.count }}</span>
              </li>
            }
          </ul>
        </section>

        <div class="table-wrap">
          @if (!w.officers.length) {
            <fg-empty-state title="No officer holds a claim" body="Claims appear here once an officer picks one up." />
          } @else {
            <table class="table" data-testid="workload-table">
              <thead>
                <tr>
                  <th scope="col">Officer</th>
                  <th scope="col" class="right">Under review</th>
                  <th scope="col" class="right">Info requested</th>
                  <th scope="col" class="right">Oldest open</th>
                  <th scope="col" class="right">Decided, 7 days</th>
                  <th scope="col" class="right">Decided, 30 days</th>
                  <th scope="col" class="right">Median days to decide</th>
                </tr>
              </thead>
              <tbody>
                @for (o of w.officers; track o.officerId) {
                  <tr>
                    <td>{{ o.officerName }}</td>
                    <td class="right">{{ o.underReview }}</td>
                    <td class="right">{{ o.infoRequested }}</td>
                    <td class="right">
                      {{
                        o.oldestOpenAgeDays === null || o.oldestOpenAgeDays === undefined
                          ? '—'
                          : days(o.oldestOpenAgeDays)
                      }}
                    </td>
                    <td class="right">{{ o.decidedLast7Days }}</td>
                    <td class="right">{{ o.decidedLast30Days }}</td>
                    <td class="right">
                      {{
                        o.medianDaysToDecision === null || o.medianDaysToDecision === undefined
                          ? '—'
                          : days(o.medianDaysToDecision)
                      }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      }
    </div>
  `,
})
export class WorkloadPage {
  private readonly api = inject(ClaimsApi);

  protected readonly query = injectQuery(() => ({
    queryKey: claimKeys.workload,
    queryFn: () => this.api.workload(),
  }));

  protected readonly openTotal = computed(
    () => this.query.data()?.officers.reduce((sum, o) => sum + o.underReview + o.infoRequested, 0) ?? 0,
  );
  private readonly maxBucket = computed(() => Math.max(1, ...(this.query.data()?.ageing.map((b) => b.count) ?? [0])));

  protected pct(count: number): number {
    return (count / this.maxBucket()) * 100;
  }

  protected days(n: number): string {
    const rounded = Math.round(n * 10) / 10;
    return `${rounded} d`;
  }
}
