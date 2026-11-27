import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { injectMutation, injectQuery, QueryClient } from '@tanstack/angular-query';
import { ToastStore } from '../../core/ui/toast.store';
import { ClaimsApi, EMPTY_CLAIM_FILTER, claimKeys } from '../../api/claims.api';
import { AccessService } from '../../core/auth/access.service';
import { RelativeTimePipe } from '../../ui/format';
import { ClaimStatusPill, MoneyPipe } from '../claims/claim-bits';
import { SLA_DECISION_DAYS, SLA_PICKUP_DAYS, ageDays, marketName } from '../claims/claims-logic';
import { bySlaRisk, claimantHero, landingKind, needsMyAction, officerPrimary } from './home-logic';

/** The page after sign-in: one landing per persona, each with one obvious primary action. */
@Component({
  selector: 'fg-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ClaimStatusPill, MoneyPipe, RelativeTimePipe],
  styleUrl: './home.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">{{ title() }}</h1>
          <p class="page-lede">{{ lede() }}</p>
        </div>
      </header>

      @switch (kind()) {
        @case ('claimant') {
          @if (claimantQuery.isPending()) {
            <div class="panel panel-pad"><span class="skeleton" style="width: 50%; height: 2rem"></span></div>
          } @else if (hero().claim; as c) {
            <section class="hero" [class.hero-urgent]="hero().needsYou" data-testid="hero" data-tour="landing-hero">
              <div>
                <p class="hero-kicker">{{ hero().needsYou ? 'This one needs you' : 'Where your claim stands' }}</p>
                <h2 class="hero-title">
                  {{ hero().needsYou ? 'We need one answer from you on ' + c.number : c.number }}
                </h2>
                <p class="hero-sub">
                  <a [routerLink]="['/claims', c.id]" class="num">{{ c.number }}</a> · {{ c.line }} ·
                  {{ market(c.market) }} · <fg-claim-status [status]="c.status" />
                </p>
                @if (heroDetail.data()?.openInfoRequest; as q) {
                  <p class="hero-q">{{ q.question }}</p>
                }
              </div>
              <a [routerLink]="['/claims', c.id]" class="btn btn-primary" data-testid="primary-action">{{
                hero().needsYou ? 'Answer now' : 'Open the claim'
              }}</a>
            </section>
          } @else {
            <section class="hero" data-testid="hero">
              <div>
                <p class="hero-kicker">Nothing open</p>
                <h2 class="hero-title">Something happened? Tell us.</h2>
                <p class="hero-sub">It takes a few minutes. An officer takes it from there.</p>
              </div>
              <a routerLink="/claims/new" class="btn btn-primary" data-testid="primary-action">Report an incident</a>
            </section>
          }
          @if (claimantRest().length) {
            <section class="panel panel-pad" aria-labelledby="yc-h">
              <h2 id="yc-h" class="section-title">Your claims</h2>
              <ul class="rows" data-testid="your-claims">
                @for (c of claimantRest(); track c.id) {
                  <li>
                    <a class="num" [routerLink]="['/claims', c.id]">{{ c.number }}</a>
                    <span class="text-muted">{{ c.line }} · {{ market(c.market) }}</span>
                    <fg-claim-status [status]="c.status" />
                    <span class="text-muted">updated {{ c.updatedAt | relative }}</span>
                  </li>
                }
              </ul>
              <p class="mt-3 mb-0">
                <a routerLink="/claims/mine">All your claims</a> ·
                <a routerLink="/claims/new">Report an incident</a>
              </p>
            </section>
          }
        }

        @case ('officer') {
          @if (officerQueries()) {
            <div class="panel panel-pad"><span class="skeleton" style="width: 50%; height: 2rem"></span></div>
          } @else {
            @if (primary(); as p) {
              <section class="hero" [class.hero-urgent]="p.kind === 'work'" data-testid="hero">
                <div>
                  <p class="hero-kicker">{{ p.kind === 'work' ? 'Closest to its SLA' : 'Waiting for an officer' }}</p>
                  <h2 class="hero-title">
                    {{ p.kind === 'work' ? 'Continue ' + p.claim.number : 'Pick up ' + p.claim.number }}
                  </h2>
                  <p class="hero-sub">
                    {{ p.claim.line }} · {{ market(p.claim.market) }} · submitted {{ days(p.claim.submittedAt) }} days
                    ago
                  </p>
                </div>
                <a [routerLink]="['/claims', p.claim.id]" class="btn btn-primary" data-testid="primary-action">{{
                  p.kind === 'work' ? 'Open claim' : 'Open and pick up'
                }}</a>
              </section>
            } @else {
              <section class="hero" data-testid="hero">
                <div>
                  <p class="hero-kicker">All clear</p>
                  <h2 class="hero-title">Nothing needs you right now</h2>
                  <p class="hero-sub">No claim of yours needs action and none is waiting for pickup.</p>
                </div>
                <a routerLink="/claims/queue" class="btn btn-primary" data-testid="primary-action">Open the queue</a>
              </section>
            }
            <section class="panel panel-pad" aria-labelledby="mw-h">
              <h2 id="mw-h" class="section-title">Needs my action</h2>
              <p class="mt-1 mb-3 text-muted">Assigned to you and under review. No reserve first, then oldest.</p>
              <ul class="rows" data-testid="my-work">
                @for (c of needsAction(); track c.id) {
                  <li>
                    <a class="num" [routerLink]="['/claims', c.id]">{{ c.number }}</a>
                    <span class="text-muted">{{ c.line }} · {{ market(c.market) }}</span>
                    @if (c.reserveMinor === null || c.reserveMinor === undefined) {
                      <span class="pill pill-err">No reserve</span>
                    }
                    <span [class.late]="days(c.submittedAt) >= sla">{{ days(c.submittedAt) }} d</span>
                  </li>
                } @empty {
                  <li class="text-muted">Nothing assigned to you needs action.</li>
                }
              </ul>
            </section>
            <div class="two">
              <section class="panel panel-pad" aria-labelledby="up-h">
                <h2 id="up-h" class="section-title">Pick up next</h2>
                <p class="mt-1 mb-3 text-muted">Submitted, nobody assigned. The oldest five.</p>
                <ul class="rows" data-testid="to-pick-up">
                  @for (c of pickUpNext(); track c.id) {
                    <li>
                      <a class="num" [routerLink]="['/claims', c.id]">{{ c.number }}</a>
                      <span class="text-muted">{{ c.line }} · {{ market(c.market) }}</span>
                      <span [class.late]="days(c.submittedAt) >= pickupSla">{{ days(c.submittedAt) }} d</span>
                      <button
                        type="button"
                        class="btn btn-sm"
                        [disabled]="pickup.isPending()"
                        (click)="pickup.mutate(c.id)"
                        data-action="pickup"
                      >
                        Pick up
                      </button>
                    </li>
                  } @empty {
                    <li class="text-muted">The pickup queue is empty.</li>
                  }
                </ul>
              </section>
              <section class="panel panel-pad" aria-labelledby="wc-h">
                <h2 id="wc-h" class="section-title">Waiting on claimant</h2>
                <p class="mt-1 mb-3 text-muted">Yours, paused on a question.</p>
                <ul class="rows" data-testid="waiting">
                  @for (c of waitingSorted(); track c.id) {
                    <li>
                      <a class="num" [routerLink]="['/claims', c.id]">{{ c.number }}</a>
                      <span class="text-muted">{{ c.line }} · {{ market(c.market) }}</span>
                      <span>{{ days(c.updatedAt) }} d waiting</span>
                    </li>
                  } @empty {
                    <li class="text-muted">Nothing is waiting on a claimant.</li>
                  }
                </ul>
              </section>
            </div>
          }
        }

        @case ('manager') {
          <section class="hero" data-testid="hero">
            <div>
              <p class="hero-kicker">Team and exposure</p>
              <h2 class="hero-title">{{ unassigned() }} waiting for pickup, {{ openLoad() }} open with officers</h2>
              <p class="hero-sub">{{ decided7() }} decided this week. Money below is per currency and never summed.</p>
            </div>
            <a routerLink="/claims/team" class="btn btn-primary" data-testid="primary-action">Open Team and exposure</a>
          </section>
          <section class="panel panel-pad" aria-labelledby="ts-h">
            <h2 id="ts-h" class="section-title">To settle</h2>
            <p class="mt-1 mb-3 text-muted">Approved and waiting for payment, oldest first.</p>
            <ul class="rows" data-testid="to-settle">
              @for (c of toSettle(); track c.id) {
                <li>
                  <a class="num" [routerLink]="['/claims', c.id]">{{ c.number }}</a>
                  <span class="text-muted">{{ c.line }} · {{ market(c.market) }}</span>
                  <span class="mono">{{ c.approvedAmountMinor | money: c.currency }}</span>
                </li>
              } @empty {
                <li class="text-muted">Nothing is waiting to be settled.</li>
              }
            </ul>
          </section>
          @if (exposureQuery.data(); as e) {
            <section class="panel panel-pad" aria-labelledby="ex-h">
              <h2 id="ex-h" class="section-title">Where the money is</h2>
              <div class="chips" data-testid="money-chips">
                @for (t of e.totals; track t.currency) {
                  <div class="total-chip">
                    <span class="mono">{{ t.currency }}</span>
                    <span class="mono">{{
                      t.outstandingReserveMinor + t.approvedNotSettledMinor | money: t.currency
                    }}</span>
                    <span class="sub">{{ t.openCount }} open</span>
                  </div>
                }
              </div>
            </section>
          }
        }

        @case ('admin') {
          <section class="hero" data-testid="hero">
            <div>
              <p class="hero-kicker">System overview</p>
              <h2 class="hero-title">{{ moduleCount() }} modules on, {{ perms() }} permissions in effect</h2>
              <p class="hero-sub">Review what happened, who can do what, and what is switched on.</p>
            </div>
            <a routerLink="/audit" class="btn btn-primary" data-testid="primary-action">Open the audit log</a>
          </section>
          <section class="cards" data-testid="admin-links">
            <a class="panel panel-pad card-link" routerLink="/access"
              ><h2 class="section-title">Users and access</h2>
              <span class="text-muted">Groups, overrides and who holds what.</span></a
            >
            <a class="panel panel-pad card-link" routerLink="/modules"
              ><h2 class="section-title">Modules</h2>
              <span class="text-muted">Switch features on and off.</span></a
            >
            <a class="panel panel-pad card-link" routerLink="/claims/team"
              ><h2 class="section-title">Team and exposure</h2>
              <span class="text-muted">Workload, SLA and money per currency.</span></a
            >
          </section>
        }
      }
    </div>
  `,
})
export class HomePage {
  private readonly api = inject(ClaimsApi);
  protected readonly access = inject(AccessService);
  private readonly toasts = inject(ToastStore);
  private readonly queryClient = inject(QueryClient);

  protected readonly sla = SLA_DECISION_DAYS;
  protected readonly pickupSla = SLA_PICKUP_DAYS;
  protected readonly market = marketName;
  protected readonly kind = computed(() => landingKind((c) => this.access.has(c)));

  protected readonly title = computed(
    () =>
      ({
        claimant: 'Your claims',
        officer: 'My work',
        manager: 'Team and exposure',
        admin: 'System overview',
        other: 'Home',
      })[this.kind()],
  );
  protected readonly lede = computed(
    () =>
      ({
        claimant: 'The claim that needs you comes first.',
        officer: 'What needs your action, closest to its SLA first, and what is waiting for pickup.',
        manager: 'Who is overloaded, what is breaching, and where the money is.',
        admin: 'A short look at the platform.',
        other: '',
      })[this.kind()],
  );

  // claimant
  protected readonly claimantQuery = injectQuery(() => ({
    queryKey: claimKeys.list(EMPTY_CLAIM_FILTER),
    queryFn: () => this.api.list(EMPTY_CLAIM_FILTER, null, 50),
    enabled: this.kind() === 'claimant',
  }));
  protected readonly hero = computed(() => claimantHero(this.claimantQuery.data()?.items ?? []));
  /** The list rows carry no open question (contract gap), so the hero reads the one claim that needs the claimant. */
  protected readonly heroDetail = injectQuery(() => ({
    queryKey: claimKeys.one(this.hero().claim?.id ?? ''),
    queryFn: () => this.api.get(this.hero().claim!.id),
    enabled: this.kind() === 'claimant' && this.hero().needsYou,
  }));
  protected readonly claimantRest = computed(() => {
    const h = this.hero().claim;
    return (this.claimantQuery.data()?.items ?? []).filter((c) => c.id !== h?.id).slice(0, 6);
  });

  // officer
  private readonly mineQuery = injectQuery(() => ({
    queryKey: claimKeys.list({ ...EMPTY_CLAIM_FILTER, assigned: 'me', status: 'UnderReview' }),
    queryFn: () => this.api.list({ ...EMPTY_CLAIM_FILTER, assigned: 'me', status: 'UnderReview' }, null, 50),
    enabled: this.kind() === 'officer',
  }));
  private readonly unassignedQuery = injectQuery(() => ({
    queryKey: claimKeys.list({ ...EMPTY_CLAIM_FILTER, assigned: 'none', status: 'Submitted' }),
    queryFn: () => this.api.list({ ...EMPTY_CLAIM_FILTER, assigned: 'none', status: 'Submitted' }, null, 50),
    enabled: this.kind() === 'officer',
  }));
  protected readonly officerQueries = computed(() => this.mineQuery.isPending() || this.unassignedQuery.isPending());
  private readonly waitingQuery = injectQuery(() => ({
    queryKey: claimKeys.list({ ...EMPTY_CLAIM_FILTER, assigned: 'me', status: 'InfoRequested' }),
    queryFn: () => this.api.list({ ...EMPTY_CLAIM_FILTER, assigned: 'me', status: 'InfoRequested' }, null, 50),
    enabled: this.kind() === 'officer',
  }));
  protected readonly needsAction = computed(() => needsMyAction(this.mineQuery.data()?.items ?? []));
  protected readonly pickUpNext = computed(() => bySlaRisk(this.unassignedQuery.data()?.items ?? []).slice(0, 5));
  protected readonly waitingSorted = computed(() => bySlaRisk(this.waitingQuery.data()?.items ?? []));
  protected readonly primary = computed(() =>
    officerPrimary(this.mineQuery.data()?.items ?? [], this.unassignedQuery.data()?.items ?? []),
  );

  protected readonly pickup = injectMutation(() => ({
    mutationFn: (id: string) => this.api.pickup(id),
    onSuccess: (c) => {
      this.toasts.ok(`Picked up ${c.number}`);
      void this.queryClient.invalidateQueries({ queryKey: claimKeys.all });
    },
  }));

  // manager
  private readonly workloadQuery = injectQuery(() => ({
    queryKey: claimKeys.workload,
    queryFn: () => this.api.workload(),
    enabled: this.kind() === 'manager',
  }));
  protected readonly exposureQuery = injectQuery(() => ({
    queryKey: claimKeys.exposure,
    queryFn: () => this.api.exposure(),
    enabled: this.kind() === 'manager',
  }));
  private readonly approvedQuery = injectQuery(() => ({
    queryKey: [...claimKeys.all, 'approved'] as const,
    queryFn: () => this.api.list({ ...EMPTY_CLAIM_FILTER, status: 'Approved' }, null, 100),
    enabled: this.kind() === 'manager',
  }));
  protected readonly toSettle = computed(() => bySlaRisk(this.approvedQuery.data()?.items ?? []).slice(0, 5));
  protected readonly unassigned = computed(() => this.workloadQuery.data()?.unassignedCount ?? 0);
  protected readonly openLoad = computed(
    () => this.workloadQuery.data()?.officers.reduce((n, o) => n + o.underReview + o.infoRequested, 0) ?? 0,
  );
  protected readonly decided7 = computed(
    () => this.workloadQuery.data()?.officers.reduce((n, o) => n + o.decidedLast7Days, 0) ?? 0,
  );

  // admin
  protected readonly moduleCount = computed(() => this.access.me()?.modules.length ?? 0);
  protected readonly perms = computed(() => this.access.permissions().size);

  protected days(iso: string): number {
    return ageDays(iso);
  }
}
