import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FX_AS_OF, FX_SOURCE, FxInputRow, USD_PER_UNIT, usdView } from './fx-indicative';
import { marketFlag, marketName } from './claims-logic';

const usd = new Intl.NumberFormat('en', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
const SERIES_VAR = ['var(--chart-1)', 'var(--chart-2)'];

/** Indicative USD view of outstanding reserve: total, bar by market, donut by line. Beside the exact per-currency table. */
@Component({
  selector: 'fg-usd-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .usd-total {
      font-family: var(--font-display);
      font-size: 2rem;
      font-weight: 600;
      line-height: 1.1;
    }
    .charts {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
      gap: 2rem;
      margin-top: 1rem;
      align-items: start;
    }
    @media (max-width: 40rem) {
      .charts {
        grid-template-columns: 1fr;
      }
    }
    .chart-title {
      margin: 0 0 0.5rem;
      font-size: 0.8667rem;
      font-weight: 550;
      color: var(--muted);
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .bar-row {
      display: grid;
      grid-template-columns: 7.5rem 1fr 5.5rem;
      align-items: center;
      gap: 0.75rem;
      padding: 0.3rem 0;
    }
    .bar-row svg {
      width: 100%;
      height: 1rem;
      display: block;
    }
    .bar-track {
      fill: var(--line-soft);
    }
    .bar-fill {
      fill: var(--chart-1);
      transition: width 0.4s ease;
    }
    .val {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .donut-wrap {
      display: flex;
      align-items: center;
      gap: 1.25rem;
      flex-wrap: wrap;
    }
    .donut {
      width: 8.5rem;
      height: 8.5rem;
      flex: none;
    }
    .donut circle {
      fill: none;
      stroke-width: 6;
      transition: stroke-dasharray 0.4s ease;
    }
    .donut .ring {
      stroke: var(--line-soft);
    }
    .legend li {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.2rem 0;
    }
    .swatch {
      width: 0.75rem;
      height: 0.75rem;
      border-radius: 2px;
      flex: none;
    }
    .warn {
      color: var(--warn-ink);
    }
    @media (prefers-reduced-motion: reduce) {
      .bar-fill,
      .donut circle {
        transition: none;
      }
    }
  `,
  template: `
    <h2 id="usd-h" class="section-title">In US dollars (indicative)</h2>
    <p class="mt-1 mb-3 text-muted" data-testid="usd-caption">
      Outstanding reserve converted at fixed rates as of {{ asOf }}: {{ rateText }}. {{ source }} The per-currency figures
      above are the exact ones.
    </p>
    @if (view().unconverted.length) {
      <p class="warn" role="status" data-testid="usd-unconverted">
        No rate for {{ view().unconverted.join(', ') }}: those markets are left out of the dollar figures.
      </p>
    }
    <div>
      <div class="label">Total outstanding reserve, indicative</div>
      <div class="usd-total mono" data-testid="usd-total">{{ money(view().totalUsdCents) }}</div>
    </div>
    <div class="charts">
      <div>
        <h3 class="chart-title">By market</h3>
        <ul data-testid="usd-bars">
          @for (m of bars(); track m.market) {
            <li class="bar-row">
              <span><span aria-hidden="true">{{ flag(m.market) }}</span> {{ name(m.market) }}</span>
              <svg aria-hidden="true" focusable="false" viewBox="0 0 100 10" preserveAspectRatio="none">
                <rect class="bar-track" x="0" y="0" width="100" height="10" rx="2" />
                <rect class="bar-fill" x="0" y="0" [attr.width]="m.pct" height="10" rx="2" />
              </svg>
              <span class="val mono">{{ money(m.usdCents) }}</span>
            </li>
          }
        </ul>
      </div>
      <div>
        <h3 class="chart-title">By line</h3>
        <div class="donut-wrap">
          <svg class="donut" viewBox="0 0 42 42" role="img" [attr.aria-label]="donutLabel()">
            <circle class="ring" cx="21" cy="21" r="15.9155" />
            @for (s of slices(); track s.line) {
              <circle
                cx="21"
                cy="21"
                r="15.9155"
                transform="rotate(-90 21 21)"
                [attr.stroke]="s.color"
                [attr.stroke-dasharray]="s.pct + ' ' + (100 - s.pct)"
                [attr.stroke-dashoffset]="-s.start"
              />
            }
          </svg>
          <ul class="legend" data-testid="usd-legend">
            @for (s of slices(); track s.line) {
              <li>
                <span class="swatch" [style.background]="s.color" aria-hidden="true"></span>
                <span>{{ s.line }}</span>
                <span class="mono">{{ money(s.usdCents) }} ({{ s.label }})</span>
              </li>
            }
          </ul>
        </div>
      </div>
    </div>
  `,
})
export class UsdView {
  readonly rows = input.required<readonly FxInputRow[]>();

  protected readonly asOf = FX_AS_OF;
  protected readonly source = FX_SOURCE;
  protected readonly rateText = Object.entries(USD_PER_UNIT)
    .map(([c, r]) => `1 ${c} = ${r} USD`)
    .join(', ');
  protected readonly flag = marketFlag;
  protected readonly name = marketName;
  protected readonly view = computed(() => usdView(this.rows()));

  protected readonly bars = computed(() => {
    const max = Math.max(1, ...this.view().byMarket.map((m) => m.usdCents));
    return this.view().byMarket.map((m) => ({ ...m, pct: m.usdCents > 0 ? Math.max(2, (m.usdCents / max) * 100) : 0 }));
  });

  protected readonly slices = computed(() => {
    const v = this.view();
    let start = 0;
    return v.byLine.map((l, i) => {
      const share = v.totalUsdCents > 0 ? (l.usdCents / v.totalUsdCents) * 100 : 0;
      const slice = {
        ...l,
        pct: share,
        start,
        color: SERIES_VAR[i % SERIES_VAR.length],
        label: `${Math.round(share)}%`,
      };
      start += share;
      return slice;
    });
  });

  protected readonly donutLabel = computed(
    () => 'Share of USD reserve by line: ' + this.slices().map((s) => `${s.line} ${s.label}`).join(', '),
  );

  protected money(cents: number): string {
    return usd.format(cents / 100);
  }
}
