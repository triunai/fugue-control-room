import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  inject,
  signal,
} from '@angular/core';
import { ClaimsApi } from '../../api/claims.api';
import { ClaimDto } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import type { UniverHandle } from './univer-loader';
import { XlsxCell, downloadXlsx } from './xlsx-export';
import { loadStylesheet } from './lazy-css';
import { RESERVES_SHEET, sheetHostHeight, sheetRowCount } from './sheet-layout';
import { fetchOpenClaims } from './open-claims';
import { Actor, STATUS_LABEL, ageDays, currencyOf } from './claims-logic';
import {
  RESERVE_COL,
  RowResult,
  SHEET_COLUMNS,
  SheetRow,
  diffReserves,
  errorResult,
  minorToCell,
  savedResult,
  toSheetRow,
} from './reserve-sheet-logic';

/** The reserve review sheet (staff). Univer is imported inside `mount()`, so it only ever lives in this route's lazy chunks. */
@Component({
  selector: 'fg-reserve-sheet-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './reserve-sheet.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Reserve sheet</h1>
          <p class="page-lede">
            Open claims in a spreadsheet. Only the Reserve column is editable, in each claim's own currency (a decimal,
            two places at most). Save sends one guarded reserve command per changed row; amounts in different currencies
            are never added together.
          </p>
        </div>
      </header>

      @if (loadError()) {
        <div class="panel panel-pad" role="alert">
          {{ loadError() }} <button class="btn btn-sm" (click)="reload()">Retry</button>
        </div>
      }

      @if (notice()) {
        <p class="text-muted text-sm" role="status">{{ notice() }}</p>
      }
      @if (results().length) {
        <section class="panel panel-pad results" aria-label="Save results" data-testid="sheet-results">
          <ul>
            @for (r of results(); track r.id) {
              <li [class]="'res res-' + r.outcome">
                <span class="mono">{{ r.number }}</span>
                <span>{{ r.message }}</span>
              </li>
            }
          </ul>
        </section>
      }

      <div class="sheet-frame" data-tour="sheet-grid">
        <div class="sheet-host" [style.height.px]="hostHeight()" aria-label="Reserve spreadsheet"></div>
      </div>
      <footer class="sheet-foot">
        <p class="text-muted text-sm sheet-caption">
          Tinted cells are editable inputs; everything else is read-only. "By currency" totals each currency live as you
          type; a sheet you add with + is scratch space and is not saved.
        </p>
        <div class="sheet-actions">
          <span class="text-muted text-sm" data-testid="sheet-pending"
            >{{ pending() }} unsaved change{{ pending() === 1 ? '' : 's' }}</span
          >
          <button type="button" class="btn" [disabled]="saving()" (click)="reload()">Reload</button>
          <button type="button" class="btn" data-tour="sheet-excel" [disabled]="!ready()" (click)="openInExcel()">
            Open in Excel
          </button>
          <button
            type="button"
            class="btn btn-primary"
            data-testid="sheet-save"
            data-tour="sheet-save"
            [disabled]="saving() || !ready()"
            (click)="save()"
          >
            {{ saving() ? 'Saving' : 'Save changes' }}
          </button>
        </div>
      </footer>
    </div>
  `,
})
export class ReserveSheetPage implements AfterViewInit, OnDestroy {
  private readonly api = inject(ClaimsApi);
  private readonly access = inject(AccessService);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly ready = signal(false);
  protected readonly saving = signal(false);
  protected readonly hostHeight = signal(sheetHostHeight(1));
  protected readonly pending = signal(0);
  protected readonly loadError = signal('');
  protected readonly results = signal<RowResult[]>([]);
  protected readonly notice = signal('');

  private univer: UniverHandle | null = null;
  private rows: SheetRow[] = [];
  private disposed = false;

  ngAfterViewInit(): void {
    void this.mount();
  }

  ngOnDestroy(): void {
    this.dispose();
  }

  private hostEl(): HTMLElement {
    return this.element.nativeElement.querySelector('.sheet-host')!;
  }

  private actor(): Actor {
    return { userId: this.access.me()?.userId, permissions: this.access.permissions() };
  }

  private async mount(): Promise<void> {
    this.loadError.set('');
    try {
      const [claims, lib] = await Promise.all([
        fetchOpenClaims(this.api),
        loadStylesheet('vendor/univer/index.css'),
        import('./univer-loader'),
      ]).then(([c, , l]) => [c, l] as const);
      if (this.disposed) return;
      const actor = this.actor();
      this.rows = claims.map((c) => toSheetRow(c, actor));
      const header = SHEET_COLUMNS as readonly string[];
      const body = this.rows.map((r) => this.rowCells(r.claim));
      this.hostHeight.set(sheetHostHeight(sheetRowCount(body.length)));
      this.univer = lib.createSheet(this.hostEl(), header, body, RESERVE_COL, () => this.refreshPending());
      this.pending.set(0);
      this.ready.set(true);
    } catch (e) {
      if (!this.disposed) this.loadError.set(e instanceof Error ? e.message : 'Could not load the sheet.');
    }
  }

  private rowCells(c: ClaimDto): (string | number | null)[] {
    return [
      c.number,
      c.market,
      c.currency || currencyOf(c.market),
      c.line,
      STATUS_LABEL[c.status],
      c.assignedOfficerName ?? 'Unassigned',
      minorToCell(c.reserveMinor),
      ageDays(c.submittedAt),
    ];
  }

  async reload(): Promise<void> {
    this.dispose(false);
    this.results.set([]);
    this.notice.set('');
    this.ready.set(false);
    await this.mount();
  }

  private currentCells(): unknown[] {
    if (!this.univer) return [];
    const ws = this.univer.univerAPI.getActiveWorkbook()?.getSheetBySheetId(RESERVES_SHEET);
    if (!ws || !this.rows.length) return [];
    return ws
      .getRange(1, RESERVE_COL, this.rows.length, 1)
      .getValues()
      .map((r: unknown[]) => r[0]);
  }

  private refreshPending(): void {
    const { commands, rejected } = diffReserves(this.rows, this.currentCells());
    this.pending.set(commands.length + rejected.length);
  }

  /** Downloads the sheet as it is now (including unsaved reserve edits) as a real .xlsx. */
  async openInExcel(): Promise<void> {
    const ws = this.univer?.univerAPI.getActiveWorkbook()?.getSheetBySheetId(RESERVES_SHEET);
    if (!ws || !this.rows.length) return;
    const values: unknown[][] = ws.getRange(1, 0, this.rows.length, SHEET_COLUMNS.length).getValues();
    const grid = [
      [...SHEET_COLUMNS] as XlsxCell[],
      ...values.map((r) => r.map((v) => (v === '' || v === undefined ? null : (v as XlsxCell)))),
    ];
    await downloadXlsx('reserve-sheet.xlsx', 'Open claims', grid);
  }

  /** One If-Match command per changed row, in sheet order, one after another; a failure never stops the next row. */
  async save(): Promise<void> {
    if (!this.univer || this.saving()) return;
    const { commands, rejected } = diffReserves(this.rows, this.currentCells());
    const results: RowResult[] = rejected.map((r) => ({
      id: r.id,
      number: r.number,
      outcome: 'failed',
      message: r.message,
    }));
    this.saving.set(true);
    try {
      for (const cmd of commands) {
        const index = this.rows.findIndex((r) => r.claim.id === cmd.id);
        try {
          const claim = await this.api.setReserve(cmd.id, cmd.amountMinor);
          this.applyRow(index, claim);
          results.push(savedResult(cmd, claim));
        } catch (e) {
          const result = errorResult(cmd, e);
          results.push(result);
          if (result.outcome === 'stale') await this.reloadRow(index);
        }
      }
    } finally {
      this.saving.set(false);
    }
    this.notice.set(results.length ? '' : 'No changes to save.');
    this.results.set(results);
    this.refreshPending();
  }

  /** A stale row (412) is re-read: the sheet shows the claim as it now stands and the new ETag is stored for the next try. */
  private async reloadRow(index: number): Promise<void> {
    try {
      this.applyRow(index, await this.api.get(this.rows[index].claim.id));
    } catch {
      /* the row keeps what the user typed; the failed result is already listed */
    }
  }

  private applyRow(index: number, claim: ClaimDto): void {
    if (index < 0) return;
    this.rows[index] = toSheetRow(claim, this.actor());
    const ws = this.univer?.univerAPI.getActiveWorkbook()?.getSheetBySheetId(RESERVES_SHEET);
    ws?.getRange(index + 1, RESERVE_COL).setValue(minorToCell(claim.reserveMinor) ?? '');
    ws?.getRange(index + 1, 4).setValue(STATUS_LABEL[claim.status]);
  }

  private dispose(final = true): void {
    if (final) this.disposed = true;
    this.univer?.dispose();
    this.univer = null;
    this.hostEl().replaceChildren();
  }
}
