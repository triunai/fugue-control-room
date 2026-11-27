import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, EMPTY_CLAIM_FILTER, claimKeys } from '../../api/claims.api';
import { AccessService } from '../../core/auth/access.service';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { MoneyPipe } from './claim-bits';
import { ClaimsFilterStore } from './claims-filter.store';
import { SLA_DECISION_DAYS } from './claims-logic';
import { exposureMatrix, oldestPerOfficer, summariseOpen } from './team-logic';

/** Team and exposure on one page: who is overloaded, what is breaching, where the money is. Money is per currency, never summed. */
@Component({
  selector: 'fg-team-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, RelativeTimePipe, StampPipe, SkeletonRows, EmptyState, ErrorState],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Team and exposure</h1>
          <p class="page-lede">
            Who is overloaded, what is breaching its SLA, and where the money is. Read live from the claims.
          </p>
        </div>
        @if (workload.data(); as w) {
          <span class="text-muted text-sm" [title]="w.asOf | stamp">as of {{ w.asOf | relative }}</span>
        }
      </header>

      @if (canTeam()) {
        @if (workload.isPending()) {
          <div class="table-wrap"><fg-skeleton-rows [rows]="4" /></div>
        } @else if (workload.isError()) {
          <div class="panel"><fg-error-state [error]="workload.error()" (retry)="workload.refetch()" /></div>
        } @else if (workload.data(); as w) {
          <section class="kpis" aria-label="Team at a glance" data-testid="team-kpis">
            <button type="button" class="kpi" (click)="toQueue('none')" data-testid="kpi-unassigned">
              <span class="kpi-n">{{ w.unassignedCount }}</span>
              <span class="kpi-l">Unassigned, waiting for pickup</span>
            </button>
            <button type="button" class="kpi" (click)="toQueue('any')" data-testid="kpi-breaching">
              <span class="kpi-n" [class.kpi-bad]="summary().pastSla > 0">{{
                openQuery.isSuccess() ? summary().pastSla : '…'
              }}</span>
              <span class="kpi-l">Open past the {{ sla }} day SLA</span>
            </button>
            <div class="kpi static" data-testid="kpi-oldest">
              @if (summary().oldest; as o) {
                <span class="kpi-n">{{ o.ageDays }} d</span>
                <span class="kpi-l"
                  >Oldest open claim: <a [routerLink]="['/claims', o.id]">{{ o.number }}</a></span
                >
              } @else {
                <span class="kpi-n">{{ openQuery.isSuccess() ? '—' : '…' }}</span>
                <span class="kpi-l">Oldest open claim</span>
              }
            </div>
            <div class="kpi static" data-testid="kpi-decided">
              <span class="kpi-n">{{ decided7() }}</span>
              <span class="kpi-l">Decided this week</span>
            </div>
          </section>

          <section class="panel panel-pad" aria-labelledby="off-h">
            <h2 id="off-h" class="section-title">Officers</h2>
            <p class="mt-1 mb-3 text-muted">
              Load is open claims: under review plus waiting on the claimant. The bar is relative to the busiest
              officer.
            </p>
            @if (!w.officers.length) {
              <fg-empty-state
                title="No officer holds a claim"
                body="Claims appear here once an officer picks one up."
              />
            } @else {
              <div class="table-wrap">
                <table class="table" data-testid="team-table">
                  <thead>
                    <tr>
                      <th scope="col">Officer</th>
                      <th scope="col">Open load</th>
                      <th scope="col" class="right">Oldest open</th>
                      <th scope="col" class="right">Decided 7 / 30 days</th>
                      <th scope="col" class="right">Median days to decide</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (o of w.officers; track o.officerId) {
                      <tr>
                        <td>{{ o.officerName }}</td>
                        <td>
                          <div class="load">
                            <span class="bar-track" aria-hidden="true"
                              ><span class="bar-fill" [style.width.%]="loadPct(o.underReview + o.infoRequested)"></span
                            ></span>
                            <span class="load-n"
                              >{{ o.underReview + o.infoRequested }}
                              <span class="sub"
                                >{{ o.underReview }} reviewing{{
                                  o.infoRequested ? ', ' + o.infoRequested + ' waiting on claimant' : ''
                                }}</span
                              ></span
                            >
                          </div>
                        </td>
                        <td class="right">
                          @if (o.oldestOpenAgeDays === null || o.oldestOpenAgeDays === undefined) {
                            —
                          } @else if (oldestOf().get(o.officerId); as c) {
                            <a [routerLink]="['/claims', c.id]">{{ days(o.oldestOpenAgeDays) }}</a>
                            <span class="sub">{{ c.number }}</span>
                          } @else {
                            {{ days(o.oldestOpenAgeDays) }}
                          }
                        </td>
                        <td class="right">{{ o.decidedLast7Days }} / {{ o.decidedLast30Days }}</td>
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
              </div>
            }
          </section>

          <section class="panel panel-pad" aria-labelledby="age-h">
            <h2 id="age-h" class="section-title">Ageing of open claims</h2>
            <p class="mt-1 mb-3 text-muted">Days since submission. Select a band to open the queue.</p>
            <ul class="bars" role="list" data-testid="ageing">
              @for (b of w.ageing; track b.label) {
                <li>
                  <button
                    type="button"
                    class="bar-btn"
                    (click)="toQueue('any')"
                    [attr.aria-label]="b.label + ' days: ' + b.count + ' claims, open the queue'"
                  >
                    <span class="bar-label">{{ b.label }} days</span>
                    <span class="bar-track" aria-hidden="true"
                      ><span class="bar-fill" [style.width.%]="agePct(b.count)"></span
                    ></span>
                    <span class="bar-n">{{ b.count }}</span>
                  </button>
                </li>
              }
            </ul>
          </section>
        }
      }

      @if (canMoney()) {
        <section class="panel panel-pad" aria-labelledby="exp-h">
          <h2 id="exp-h" class="section-title">Where the money is</h2>
          <p class="mt-1 mb-3 text-muted" data-testid="exposure-summary">
            Outstanding reserve on open claims plus approved money not yet settled. Each market settles in its own
            currency, so bars are scaled within a currency and nothing is ever added across currencies: there is no
            grand total.
          </p>
          @if (exposure.isPending()) {
            <fg-skeleton-rows [rows]="4" />
          } @else if (exposure.isError()) {
            <fg-error-state [error]="exposure.error()" (retry)="exposure.refetch()" />
          } @else if (!matrix().length) {
            <fg-empty-state
              title="No open exposure"
              body="Open claims with a reserve, and approved claims not yet settled, appear here."
            />
          } @else {
            <div class="table-wrap">
              <table class="table matrix" data-testid="exposure-table">
                <thead>
                  <tr>
                    <th scope="col">Market</th>
                    <th scope="col">Motor</th>
                    <th scope="col">Property</th>
                    <th scope="col" class="right">Total in its currency</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of matrix(); track r.market) {
                    <tr>
                      <th scope="row">
                        {{ r.marketName }} <span class="sub mono">{{ r.currency }}</span>
                      </th>
                      <td>
                        <div class="cell">
                          <span class="mono">{{ r.motor.minor | money: r.currency }}</span>
                          <span class="cell-bar" aria-hidden="true"><span [style.width.%]="r.motor.pct"></span></span>
                        </div>
                      </td>
                      <td>
                        <div class="cell">
                          <span class="mono">{{ r.property.minor | money: r.currency }}</span>
                          <span class="cell-bar" aria-hidden="true"
                            ><span [style.width.%]="r.property.pct"></span
                          ></span>
                        </div>
                      </td>
                      <td class="right mono">
                        <strong>{{ r.totalMinor | money: r.currency }}</strong>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <section class="totals" aria-label="Totals per currency" data-testid="exposure-totals">
              @for (t of exposure.data()?.totals ?? []; track t.currency) {
                <div class="total-chip">
                  <span class="mono">{{ t.currency }}</span>
                  <span class="mono">{{
                    t.outstandingReserveMinor + t.approvedNotSettledMinor | money: t.currency
                  }}</span>
                  <span class="sub">{{ t.openCount }} open, {{ t.approvedNotSettledCount }} approved not settled</span>
                </div>
              }
            </section>
          }
        </section>
      }
    </div>
  `,
})
export class TeamPage {
  private readonly api = inject(ClaimsApi);
  private readonly access = inject(AccessService);
  private readonly router = inject(Router);
  private readonly filters = inject(ClaimsFilterStore);

  protected readonly sla = SLA_DECISION_DAYS;
  protected readonly canTeam = computed(() => this.access.has('CLAIMS_WORKLOAD'));
  protected readonly canMoney = computed(() => this.access.has('CLAIMS_EXPOSURE'));

  protected readonly workload = injectQuery(() => ({
    queryKey: claimKeys.workload,
    queryFn: () => this.api.workload(),
    enabled: this.canTeam(),
  }));

  /** Every open claim (one page of 100 per open status), for "past SLA" and each officer's oldest claim. */
  protected readonly openQuery = injectQuery(() => ({
    queryKey: [...claimKeys.all, 'open'] as const,
    queryFn: async () =>
      (
        await Promise.all(
          (['Submitted', 'UnderReview', 'InfoRequested'] as const).map((status) =>
            this.api.list({ ...EMPTY_CLAIM_FILTER, status }, null, 100),
          ),
        )
      ).flatMap((p) => p.items),
    enabled: this.canTeam(),
  }));

  protected readonly exposure = injectQuery(() => ({
    queryKey: claimKeys.exposure,
    queryFn: () => this.api.exposure(),
    enabled: this.canMoney(),
  }));

  protected readonly summary = computed(() => summariseOpen(this.openQuery.data() ?? []));
  protected readonly oldestOf = computed(() => oldestPerOfficer(this.openQuery.data() ?? []));
  protected readonly matrix = computed(() => exposureMatrix(this.exposure.data()?.rows ?? []));
  protected readonly decided7 = computed(
    () => this.workload.data()?.officers.reduce((n, o) => n + o.decidedLast7Days, 0) ?? 0,
  );
  private readonly maxLoad = computed(() =>
    Math.max(1, ...(this.workload.data()?.officers.map((o) => o.underReview + o.infoRequested) ?? [0])),
  );
  private readonly maxAge = computed(() => Math.max(1, ...(this.workload.data()?.ageing.map((b) => b.count) ?? [0])));

  protected loadPct(n: number): number {
    return (n / this.maxLoad()) * 100;
  }

  protected agePct(n: number): number {
    return (n / this.maxAge()) * 100;
  }

  protected days(n: number): string {
    return `${Math.round(n * 10) / 10} d`;
  }

  protected toQueue(assigned: 'none' | 'any'): void {
    this.filters.reset();
    this.filters.setAssigned(assigned);
    void this.router.navigateByUrl('/claims/queue');
  }
}
