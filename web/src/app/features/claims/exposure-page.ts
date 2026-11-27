import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { MoneyPipe } from './claim-bits';
import { marketName } from './claims-logic';

/** Exposure: reserve still outstanding on open claims and approved money not yet settled, per market, line and currency. Totals are PER CURRENCY. */
@Component({
  selector: 'fg-exposure-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, RelativeTimePipe, StampPipe, SkeletonRows, EmptyState, ErrorState],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Exposure</h1>
          <p class="page-lede" data-testid="exposure-summary">
            Outstanding reserve on open claims, and approved money not yet settled. Each market settles in its own
            currency, so totals are shown per currency; amounts in different currencies are never added together.
          </p>
        </div>
        @if (query.data(); as e) {
          <span class="text-muted text-sm" [title]="e.asOf | stamp">as of {{ e.asOf | relative }}</span>
        }
      </header>

      @if (query.isPending()) {
        <div class="table-wrap"><fg-skeleton-rows [rows]="6" /></div>
      } @else if (query.isError()) {
        <div class="panel"><fg-error-state [error]="query.error()" (retry)="query.refetch()" /></div>
      } @else if (query.data(); as e) {
        @if (!e.rows.length) {
          <div class="panel">
            <fg-empty-state
              title="No open exposure"
              body="Open claims with a reserve, and approved claims not yet settled, appear here."
            />
          </div>
        } @else {
          <section class="totals" aria-label="Totals per currency" data-testid="exposure-totals">
            @for (t of e.totals; track t.currency) {
              <div class="panel panel-pad total">
                <h2 class="section-title mono">{{ t.currency }}</h2>
                <dl class="facts">
                  <div>
                    <dt>Open claims</dt>
                    <dd>{{ t.openCount }}</dd>
                  </div>
                  <div>
                    <dt>Outstanding reserve</dt>
                    <dd class="mono">{{ t.outstandingReserveMinor | money: t.currency }}</dd>
                  </div>
                  <div>
                    <dt>Approved, not settled ({{ t.approvedNotSettledCount }})</dt>
                    <dd class="mono">{{ t.approvedNotSettledMinor | money: t.currency }}</dd>
                  </div>
                </dl>
              </div>
            }
          </section>

          <div class="table-wrap">
            <table class="table" data-testid="exposure-table">
              <thead>
                <tr>
                  <th scope="col">Market</th>
                  <th scope="col">Line</th>
                  <th scope="col">Currency</th>
                  <th scope="col" class="right">Open claims</th>
                  <th scope="col" class="right">Outstanding reserve</th>
                  <th scope="col" class="right">Approved, not settled</th>
                </tr>
              </thead>
              <tbody>
                @for (r of e.rows; track r.market + r.line + r.currency) {
                  <tr>
                    <td>{{ market(r.market) }}</td>
                    <td>{{ r.line }}</td>
                    <td class="mono">{{ r.currency }}</td>
                    <td class="right">{{ r.openCount }}</td>
                    <td class="right mono">{{ r.outstandingReserveMinor | money: r.currency }}</td>
                    <td class="right mono">
                      {{ r.approvedNotSettledMinor | money: r.currency }}
                      <span class="sub"
                        >{{ r.approvedNotSettledCount }} claim{{ r.approvedNotSettledCount === 1 ? '' : 's' }}</span
                      >
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </div>
  `,
})
export class ExposurePage {
  private readonly api = inject(ClaimsApi);
  protected readonly market = marketName;

  protected readonly query = injectQuery(() => ({
    queryKey: claimKeys.exposure,
    queryFn: () => this.api.exposure(),
  }));
}
