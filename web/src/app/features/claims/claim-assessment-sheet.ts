import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ClaimsApi } from '../../api/claims.api';
import { ClaimDto } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import {
  AMOUNT_COL,
  ASSESSMENT_INPUTS,
  Assessment,
  EXCESS_ROW,
  FIRST_INPUT_ROW,
  GROSS_FORMULA,
  GROSS_ROW,
  NET_FORMULA,
  NET_ROW,
  assess,
  assessmentGrid,
  cellToNumber,
  isAssessmentInput,
} from './claim-assessment-logic';
import { availableActions, formatMoney } from './claims-logic';
import { loadStylesheet } from './lazy-css';
import { sheetHostHeight, sheetRowCount } from './sheet-layout';
import { errorResult } from './reserve-sheet-logic';
import type { UniverHandle } from './univer-loader';
import { downloadXlsx } from './xlsx-export';

const LABELS = [...ASSESSMENT_INPUTS, 'Gross estimate', 'Less: excess', 'Net estimate'];

/**
 * An officer's estimate worksheet for one claim, in the claim's currency: repair or rebuild, parts, labour, rental, other,
 * less the excess, giving the net estimate (Gross and Net are live Univer formulas). "Use as reserve" sends the existing
 * guarded reserve command; "Open in Excel" downloads the sheet as a real .xlsx. The worksheet is ephemeral: it is not saved.
 * Univer and ExcelJS load on demand, so this component adds nothing to the initial bundle.
 */
@Component({
  selector: 'fg-claim-assessment-sheet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './claim-assessment-sheet.css',
  template: `
    <section class="assess" aria-label="Claim assessment worksheet" data-tour="assessment-sheet">
      <header class="assess-head">
        <div>
          <h2 class="section-title">Assessment worksheet</h2>
          <p class="text-muted text-sm">
            Estimate this claim in {{ claim().currency }}. Edit the tinted cells; Gross and Net are formulas. This
            worksheet is not saved anywhere: it is gone when you leave the page.
          </p>
        </div>
      </header>

      <div class="assess-frame">
        <div class="assess-host" [style.height.px]="hostHeight" aria-label="Assessment spreadsheet"></div>
      </div>
      <footer class="assess-foot">
        <p class="text-muted text-sm assess-caption">Tinted cells are editable inputs; everything else is formula.</p>
        <div class="assess-actions">
          <button type="button" class="btn" data-tour="assessment-excel" [disabled]="!ready()" (click)="openInExcel()">
            Open in Excel
          </button>
          <button
            type="button"
            class="btn btn-primary"
            data-tour="assessment-use-reserve"
            [disabled]="!canUse()"
            [title]="useHint()"
            (click)="useAsReserve()"
          >
            Use as reserve
          </button>
        </div>
      </footer>

      <p class="assess-net" data-testid="assess-net" aria-live="polite">
        @if (assessment(); as a) {
          @if (a.ok) {
            Net estimate <strong class="mono">{{ money(a.netMinor) }}</strong>
          } @else {
            <span class="assess-err">{{ a.message }}</span>
          }
        }
      </p>
      @if (message()) {
        <p class="text-sm" [class.assess-err]="failed()" role="status" data-testid="assess-message">{{ message() }}</p>
      }
    </section>
  `,
})
export class ClaimAssessmentSheet implements AfterViewInit, OnDestroy {
  readonly claim = input.required<ClaimDto>();
  /** The claim as it stands after "Use as reserve" saved. */
  readonly reserved = output<ClaimDto>();

  private readonly api = inject(ClaimsApi);
  private readonly access = inject(AccessService);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private univer: UniverHandle | null = null;

  protected readonly hostHeight = sheetHostHeight(sheetRowCount(LABELS.length));
  protected readonly ready = signal(false);
  protected readonly assessment = signal<Assessment | null>(null);
  protected readonly message = signal('');
  protected readonly failed = signal(false);
  protected readonly saving = signal(false);

  private readonly mayReserve = computed(() =>
    availableActions(this.claim(), { userId: this.access.me()?.userId, permissions: this.access.permissions() }).some(
      (a) => a.action === 'reserve' && a.enabled,
    ),
  );
  protected readonly canUse = computed(() => {
    const a = this.assessment();
    return this.ready() && !this.saving() && this.mayReserve() && !!a && a.ok && a.netMinor > 0;
  });
  protected readonly useHint = computed(() => {
    if (!this.mayReserve()) return 'You cannot set the reserve on this claim in its current status.';
    const a = this.assessment();
    if (a && a.ok && a.netMinor <= 0) return 'The net estimate must be above zero.';
    return 'Set the claim reserve to the net estimate';
  });

  protected money(minor: number): string {
    return formatMoney(minor, this.claim().currency);
  }

  async ngAfterViewInit(): Promise<void> {
    const [, lib] = await Promise.all([loadStylesheet('vendor/univer/index.css'), import('./univer-loader')]);
    const host = this.element.nativeElement.querySelector<HTMLElement>('.assess-host');
    if (!host || !this.element.nativeElement.isConnected) return;
    this.univer = lib.createAssessmentSheet(
      host,
      this.claim().currency,
      LABELS,
      { [GROSS_ROW]: GROSS_FORMULA, [NET_ROW]: NET_FORMULA },
      isAssessmentInput,
      () => this.refresh(),
    );
    this.ready.set(true);
    this.refresh();
  }

  ngOnDestroy(): void {
    this.univer?.dispose();
    this.univer = null;
  }

  private sheet() {
    return this.univer?.univerAPI.getActiveWorkbook()?.getActiveSheet();
  }

  private inputs(): { items: unknown[]; excess: unknown } {
    const ws = this.sheet();
    const items = ws
      ? ws
          .getRange(FIRST_INPUT_ROW, AMOUNT_COL, ASSESSMENT_INPUTS.length, 1)
          .getValues()
          .map((r: unknown[]) => r[0])
      : [];
    return { items, excess: ws?.getRange(EXCESS_ROW, AMOUNT_COL).getValue() };
  }

  private refresh(): void {
    const { items, excess } = this.inputs();
    this.assessment.set(this.univer ? assess(items, excess) : null);
  }

  protected async useAsReserve(): Promise<void> {
    const a = this.assessment();
    if (!a || !a.ok || !this.canUse()) return;
    const c = this.claim();
    this.saving.set(true);
    this.failed.set(false);
    try {
      const claim = await this.api.setReserve(c.id, a.netMinor);
      this.message.set(`Reserve set to ${this.money(a.netMinor)}.`);
      this.reserved.emit(claim);
    } catch (e) {
      const r = errorResult({ id: c.id, number: c.number }, e);
      this.failed.set(true);
      this.message.set(r.message);
    } finally {
      this.saving.set(false);
    }
  }

  protected async openInExcel(): Promise<void> {
    const a = this.assessment();
    if (!a || !a.ok) {
      this.failed.set(true);
      this.message.set('Fix the amounts first: every amount must be a number with at most two decimals.');
      return;
    }
    const { items, excess } = this.inputs();
    const grid = assessmentGrid(this.claim().currency, items.map(cellToNumber), cellToNumber(excess));
    await downloadXlsx(`assessment-${this.claim().number}.xlsx`, 'Assessment', grid);
  }
}
