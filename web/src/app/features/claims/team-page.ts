import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, EMPTY_CLAIM_FILTER, claimKeys } from '../../api/claims.api';
import { ExposureRowDto, OfficerWorkloadDto } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { UsdView } from './usd-view';
import { livePoll } from './live-poll';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { MoneyPipe } from './claim-bits';
import { SLA_DECISION_DAYS, SLA_PICKUP_DAYS, marketFlag } from './claims-logic';
import {
  ExposureRowX,
  bandWindow,
  byLoad,
  exposureMatrix,
  exposureQueueParams,
  oldestPerOfficer,
  summariseOpen,
} from './team-logic';

/** Paid (settled) money of one market: per line and for the market, in the market's currency. */
export interface MarketPaid {
  Motor: { count: number; minor: number };
  Property: { count: number; minor: number };
  total: { count: number; minor: number };
}

/** Sums the settled count and amount per market and line. A market has one currency, so its total never mixes currencies. */
export function paidByMarket(rows: readonly ExposureRowDto[]): Map<string, MarketPaid> {
  const out = new Map<string, MarketPaid>();
  for (const r of rows) {
    const m = out.get(r.market) ?? {
      Motor: { count: 0, minor: 0 },
      Property: { count: 0, minor: 0 },
      total: { count: 0, minor: 0 },
    };
    for (const acc of [m[r.line], m.total]) {
      acc.count += r.settledCount;
      acc.minor += r.settledMinor;
    }
    out.set(r.market, m);
  }
  return out;
}

/** Fields the backend is adding to the workload officers; absent until it lands. */
type OfficerX = OfficerWorkloadDto & { oldestOpenClaimId?: string | null };

/** Team and exposure on one page, in the order a manager asks: what is breaching, who is overloaded, where the money is. */
@Component({
  selector: 'fg-team-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, RelativeTimePipe, StampPipe, SkeletonRows, EmptyState, ErrorState, UsdView],
  styleUrls: ['./claims.css', './team.css'],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Team and exposure</h1>
          <p class="page-lede">
            What is breaching, who is overloaded, and where the money is. Select a number to open the claims behind it.
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
          <section class="kpis kpis-5" aria-label="What is breaching" data-testid="team-kpis" data-tour="team-kpis">
            <a
              class="kpi"
              routerLink="/claims/queue"
              [queryParams]="{ status: 'Submitted', assigned: 'none' }"
              data-testid="kpi-unassigned"
            >
              <span class="kpi-n">{{ w.unassignedCount }}</span>
              <span class="kpi-l">Unassigned</span>
              <span class="kpi-sub" [class.kpi-bad]="summary().unassignedLate > 0">{{
                openQuery.isSuccess() ? summary().unassignedLate + ' past ' + pickupDays + ' d' : '…'
              }}</span>
            </a>
            <a
              class="kpi"
              routerLink="/claims/queue"
              [queryParams]="{ minAgeDays: slaDays }"
              data-testid="kpi-breaching"
            >
              <span class="kpi-n" [class.kpi-bad]="summary().pastSla > 0">{{
                openQuery.isSuccess() ? summary().pastSla : '…'
              }}</span>
              <span class="kpi-l">Open past {{ slaDays }} d</span>
            </a>
            <a
              class="kpi"
              routerLink="/claims/queue"
              [queryParams]="{ status: 'InfoRequested' }"
              data-testid="kpi-waiting"
            >
              <span class="kpi-n">{{ waitingOnClaimant() }}</span>
              <span class="kpi-l">Waiting on claimant</span>
            </a>
            @if (canSettle()) {
              <a class="kpi" routerLink="/claims/queue" [queryParams]="{ status: 'Approved' }" data-testid="kpi-settle">
                <span class="kpi-n">{{ approvedQuery.isSuccess() ? approvedQuery.data().items.length : '…' }}</span>
                <span class="kpi-l">To settle</span>
              </a>
            }
            <div class="kpi" data-testid="kpi-noreserve">
              <span class="kpi-n" [class.kpi-bad]="summary().noReserve > 0">{{
                openQuery.isSuccess() ? summary().noReserve : '…'
              }}</span>
              <span class="kpi-l">Open with no reserve</span>
            </div>
          </section>
          @if (summary().oldest; as o) {
            <p class="m-0 text-muted" data-testid="kpi-oldest">
              Longest-waiting open claim: <a [routerLink]="['/claims', o.id]">{{ o.number }}</a> ({{ o.ageDays }} days).
            </p>
          }

          <section class="panel panel-pad" aria-labelledby="off-h" data-tour="team-workload">
            <h2 id="off-h" class="section-title">Who is carrying what</h2>
            <p class="mt-1 mb-3 text-muted">
              Open load is under review plus waiting on the claimant, heaviest first; the bar is relative to the busiest
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
                    @for (o of officers(); track o.officerId) {
                      <tr>
                        <td>
                          @if (hasOfficerFilter()) {
                            <a routerLink="/claims/queue" [queryParams]="{ officerId: o.officerId }">{{
                              o.officerName
                            }}</a>
                          } @else {
                            {{ o.officerName }}
                          }
                        </td>
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
                          } @else if (oldestId(o); as c) {
                            <a [routerLink]="['/claims', c.id]">{{ days(o.oldestOpenAgeDays) }}</a>
                            @if (c.number) {
                              <span class="sub">{{ c.number }}</span>
                            }
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
            <p class="mt-1 mb-3 text-muted">Days since submission. Select a band to open its claims.</p>
            <ul class="bars" role="list" data-testid="ageing">
              @for (b of w.ageing; track b.label) {
                <li>
                  <a
                    class="bar-btn"
                    routerLink="/claims/queue"
                    [queryParams]="band(b.label)"
                    [attr.aria-label]="b.label + ' days: ' + b.count + ' claims, open them'"
                  >
                    <span class="bar-label">{{ b.label }} days</span>
                    <span class="bar-track" aria-hidden="true"
                      ><span class="bar-fill" [style.width.%]="agePct(b.count)"></span
                    ></span>
                    <span class="bar-n">{{ b.count }}</span>
                  </a>
                </li>
              }
            </ul>
          </section>
        }
      }

      @if (canMoney()) {
        <section class="panel panel-pad" aria-labelledby="exp-h">
          <h2 id="exp-h" class="section-title">Where the money is</h2>
          <p class="mt-1 mb-3 text-muted" data-testid="exposure-summary" data-tour="exposure-note">
            Per cell: outstanding reserve, with approved-not-settled under it, then Paid (settled): what settled claims
            have paid out, all time. Paid money is no longer exposure, so the bars leave it out. Each market settles in
            its own currency, so bars are scaled within a currency and nothing is ever added across currencies: there is
            no grand total.
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
              <table class="table matrix" data-testid="exposure-table" data-tour="exposure-totals">
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
                        <span aria-hidden="true">{{ flag(r.market) }}</span> {{ r.marketName }}
                        <span class="sub mono">{{ r.currency }}</span>
                      </th>
                      @for (c of [r.motor, r.property]; track $index) {
                        <td>
                          <div class="cell">
                            <a
                              class="mono"
                              routerLink="/claims/queue"
                              [queryParams]="cellParams(r.market, $index, false)"
                              >{{ c.outstandingMinor | money: r.currency }}</a
                            >
                            @if (c.approvedMinor) {
                              <a
                                class="sub mono"
                                routerLink="/claims/queue"
                                [queryParams]="cellParams(r.market, $index, true)"
                                >{{ c.approvedMinor | money: r.currency }} approved</a
                              >
                            }
                            @if (paidCell(r.market, $index); as p) {
                              <a
                                class="sub mono"
                                routerLink="/claims/queue"
                                [queryParams]="paidParams(r.market, $index)"
                                [attr.aria-label]="'Paid (settled): ' + p.count + ' claims, open them'"
                                data-testid="paid-cell"
                                >{{ p.minor | money: r.currency }} paid</a
                              >
                            }
                            <span class="cell-bar" aria-hidden="true"><span [style.width.%]="c.pct"></span></span>
                            @if (c.unreserved) {
                              <span class="unreserved">{{ c.unreserved }} unreserved</span>
                            }
                          </div>
                        </td>
                      }
                      <td class="right">
                        <div class="cell cell-right">
                          <strong class="mono">{{ r.outstandingMinor | money: r.currency }}</strong>
                          <span class="sub mono">{{ r.approvedMinor | money: r.currency }} approved</span>
                          @if (paidTotal(r.market); as p) {
                            <span class="sub mono" data-testid="paid-total"
                              >{{ p.minor | money: r.currency }} paid ({{ p.count }}&nbsp;settled)</span
                            >
                          }
                          @if (r.unreserved) {
                            <span class="unreserved">{{ r.unreserved }} unreserved</span>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
        @if (matrix().length) {
          <section class="usd-section" aria-labelledby="usd-h" data-testid="usd-section">
            <fg-usd-view [rows]="matrix()" />
          </section>
        }
      }
    </div>
  `,
})
export class TeamPage {
  private readonly api = inject(ClaimsApi);
  private readonly access = inject(AccessService);

  protected readonly flag = marketFlag;
  protected readonly slaDays = SLA_DECISION_DAYS;
  protected readonly pickupDays = SLA_PICKUP_DAYS;
  protected readonly canTeam = computed(() => this.access.has('CLAIMS_WORKLOAD'));
  protected readonly canMoney = computed(() => this.access.has('CLAIMS_EXPOSURE'));
  protected readonly canSettle = computed(() => this.access.has('CLAIMS_SETTLE'));

  /** Refetch on focus and every 15 s, paused mid-dialog or mid-mutation: managers read this as the live picture. */
  private readonly live = livePoll();

  protected readonly workload = injectQuery(() => ({
    queryKey: claimKeys.workload,
    ...this.live,
    queryFn: () => this.api.workload(),
    enabled: this.canTeam(),
  }));

  /** Every open claim (one page of 100 per open status), for the breach and no-reserve counts and each officer's oldest claim. */
  protected readonly openQuery = injectQuery(() => ({
    queryKey: [...claimKeys.all, 'open'] as const,
    ...this.live,
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

  protected readonly approvedQuery = injectQuery(() => ({
    queryKey: [...claimKeys.all, 'approved'] as const,
    ...this.live,
    queryFn: () => this.api.list({ ...EMPTY_CLAIM_FILTER, status: 'Approved' }, null, 100),
    enabled: this.canTeam() && this.canSettle(),
  }));

  protected readonly exposure = injectQuery(() => ({
    queryKey: claimKeys.exposure,
    ...this.live,
    queryFn: () => this.api.exposure(),
    enabled: this.canMoney(),
  }));

  protected readonly summary = computed(() => summariseOpen(this.openQuery.data() ?? []));
  private readonly oldestOf = computed(() => oldestPerOfficer(this.openQuery.data() ?? []));
  protected readonly officers = computed(() => byLoad<OfficerX>(this.workload.data()?.officers ?? []));
  /** The backend sends oldestOpenClaimId (and filters the list by officerId) together; until then the officer name is plain text. */
  protected readonly hasOfficerFilter = computed(() => this.officers().some((o) => o.oldestOpenClaimId !== undefined));
  protected readonly matrix = computed(() => exposureMatrix((this.exposure.data()?.rows ?? []) as ExposureRowX[]));
  private readonly paid = computed(() => paidByMarket(this.exposure.data()?.rows ?? []));
  protected readonly waitingOnClaimant = computed(
    () => this.workload.data()?.officers.reduce((n, o) => n + o.infoRequested, 0) ?? 0,
  );
  private readonly maxLoad = computed(() =>
    Math.max(1, ...this.officers().map((o) => o.underReview + o.infoRequested)),
  );
  private readonly maxAge = computed(() => Math.max(1, ...(this.workload.data()?.ageing.map((b) => b.count) ?? [0])));

  protected oldestId(o: OfficerX): { id: string; number?: string } | null {
    if (o.oldestOpenClaimId) return { id: o.oldestOpenClaimId };
    return this.oldestOf().get(o.officerId) ?? null;
  }

  protected cellParams(market: string, lineIndex: number, approved: boolean) {
    return exposureQueueParams(market, lineIndex === 0 ? 'Motor' : 'Property', approved);
  }

  /** A line's paid money, or null when nothing was settled there (the cell then shows no paid line). */
  protected paidCell(market: string, lineIndex: number): { count: number; minor: number } | null {
    const p = this.paid().get(market)?.[lineIndex === 0 ? 'Motor' : 'Property'];
    return p && p.count > 0 ? p : null;
  }

  protected paidTotal(market: string): { count: number; minor: number } {
    return this.paid().get(market)?.total ?? { count: 0, minor: 0 };
  }

  /** The settled claims behind a paid figure. */
  protected paidParams(market: string, lineIndex: number) {
    return { market, line: lineIndex === 0 ? 'Motor' : 'Property', status: 'Settled' };
  }

  protected band(label: string) {
    return bandWindow(label);
  }

  protected loadPct(n: number): number {
    return (n / this.maxLoad()) * 100;
  }

  protected agePct(n: number): number {
    return (n / this.maxAge()) * 100;
  }

  protected days(n: number): string {
    return `${Math.round(n * 10) / 10} d`;
  }
}
