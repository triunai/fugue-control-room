import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { injectQuery, QueryClient } from '@tanstack/angular-query';
import { claimKeys } from '../../api/claims.api';
import type { ImportFileDto, ImportSource } from '../../api/generated/model';
import { isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { StampPipe } from '../../ui/format';
import { ClaimStatusPill } from '../claims/claim-bits';
import { MARKETS } from '../claims/claims-logic';
import { ImportApi, importKeys } from './import.api';
import {
  ColumnMapping,
  DraftField,
  DraftRow,
  FIELDS,
  SOURCES,
  createBody,
  draftsFromFile,
  editValue,
  fieldErrors,
  guessMapping,
  otherErrors,
  rowsToDrafts,
  stateOf,
  summarise,
  toInput,
  withVerdict,
} from './import-logic';

/**
 * Import (item 11a): a spreadsheet, an inbox export or a WhatsApp chat export becomes DRAFT claims a person reviews; nothing is created until they
 * press Create, and then each claim goes through the normal submit path on the server. File imports standing in for live connectors.
 */
@Component({
  selector: 'fg-import-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ClaimStatusPill, StampPipe],
  styleUrl: './import.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Import</h1>
          <p class="page-lede">
            Turn a spreadsheet, exported emails or a WhatsApp chat into draft claims. Every draft is checked with the
            same rules as Report an incident, and nothing is created until you review it and press Create.
          </p>
        </div>
      </header>

      <div class="banner banner-info" role="note" data-testid="import-honest">
        <span>
          <span class="banner-title">File imports, not live connectors.</span>
          You upload files; the console calls no outside service. The inbox upload stands in for a Microsoft Graph or
          Gmail connector, the chat upload for WhatsApp Business. The same row, email or chat is never imported twice.
        </span>
      </div>

      <div class="tabs" role="tablist" aria-label="Import source">
        @for (s of sources; track s.id) {
          <button
            type="button"
            class="tab"
            role="tab"
            [id]="'imp-tab-' + s.id"
            [attr.aria-selected]="source() === s.id"
            [attr.aria-controls]="'imp-panel'"
            (click)="choose(s.id)"
          >
            {{ s.label }}
          </button>
        }
      </div>

      <section
        class="panel panel-pad imp-upload"
        id="imp-panel"
        role="tabpanel"
        [attr.aria-labelledby]="'imp-tab-' + source()"
      >
        <div>
          <label class="label" for="imp-file">{{ current().label }} file ({{ current().file }})</label>
          <input
            id="imp-file"
            class="input"
            type="file"
            [accept]="current().accept"
            (change)="pick($event)"
            [disabled]="busy()"
            data-testid="import-file"
          />
          <span class="field-hint">{{ current().howTo }} Stands in for {{ current().standsInFor }}.</span>
        </div>
        @if (busy() === 'read') {
          <p class="text-muted" role="status">Reading the file…</p>
        }
        @if (uploadError(); as e) {
          <div class="banner banner-err" role="alert">
            <span><span class="banner-title">That file could not be read.</span> {{ e }}</span>
          </div>
        }
      </section>

      @if (file(); as f) {
        @if (f.sheet; as sheet) {
          <section class="panel panel-pad" aria-labelledby="imp-map-title" data-testid="import-mapping">
            <h2 class="section-title" id="imp-map-title">Columns of {{ f.fileName }}</h2>
            <p class="text-muted imp-small">
              {{ sheet.rows.length }} data row{{ sheet.rows.length === 1 ? '' : 's' }}. Say which column holds each
              field; the guesses come from the header row.
            </p>
            <div class="imp-map">
              @for (field of fields; track field.id) {
                <div class="field">
                  <label class="label" [for]="'imp-col-' + field.id">{{ field.label }}</label>
                  <select
                    class="select"
                    [id]="'imp-col-' + field.id"
                    [value]="mapping()[field.id]"
                    (change)="mapColumn(field.id, $any($event.target).value)"
                  >
                    <option value="-1">{{ field.required ? '(not in the file)' : '(none)' }}</option>
                    @for (h of sheet.headers; track $index) {
                      <option [value]="$index" [selected]="mapping()[field.id] === $index">
                        {{ h || 'Column ' + ($index + 1) }}
                      </option>
                    }
                  </select>
                </div>
              }
            </div>
            <div class="imp-actions">
              <button
                type="button"
                class="btn btn-primary"
                (click)="useMapping()"
                [disabled]="!!busy()"
                data-testid="import-map"
              >
                Make {{ sheet.rows.length }} draft{{ sheet.rows.length === 1 ? '' : 's' }} and check them
              </button>
            </div>
          </section>
        }

        @if (drafts().length) {
          <section aria-labelledby="imp-review-title" data-testid="import-review">
            <div class="imp-bar panel panel-pad">
              <div>
                <h2 class="section-title" id="imp-review-title">Review {{ f.fileName }}</h2>
                <p class="imp-small text-muted" data-testid="import-summary">
                  {{ summary().ready }} ready · {{ summary().problems }} with problems
                  @if (summary().unchecked) {
                    · {{ summary().unchecked }} not checked yet
                  }
                  @if (summary().imported) {
                    · {{ summary().imported }} imported before
                  }
                  @if (summary().created) {
                    · {{ summary().created }} created now
                  }
                </p>
              </div>
              <div class="imp-actions">
                <button type="button" class="btn" (click)="checkAll()" [disabled]="!!busy()" data-testid="import-check">
                  {{ busy() === 'check' ? 'Checking…' : 'Check again' }}
                </button>
                <button
                  type="button"
                  class="btn btn-primary"
                  (click)="createAll()"
                  [disabled]="!!busy() || summary().toCreate === 0"
                  data-testid="import-create"
                >
                  {{
                    busy() === 'create'
                      ? 'Creating…'
                      : 'Create ' + summary().toCreate + ' claim' + (summary().toCreate === 1 ? '' : 's')
                  }}
                </button>
              </div>
            </div>

            @for (row of drafts(); track $index; let i = $index) {
              <article class="panel imp-draft" [attr.data-state]="state(row)" [attr.data-testid]="'draft-' + i">
                <header class="imp-draft-head">
                  <label class="imp-include">
                    <input
                      type="checkbox"
                      [checked]="row.include"
                      [disabled]="!!row.claim"
                      (change)="toggle(i, $any($event.target).checked)"
                    />
                    <span class="sr-only">Include {{ row.item }}</span>
                  </label>
                  <span class="imp-item">{{ row.item }}</span>
                  @switch (state(row)) {
                    @case ('created') {
                      <span class="pill pill-ok"
                        >Created
                        <a [routerLink]="['/claims', row.claim!.id]" class="mono">{{ row.claim!.number }}</a></span
                      >
                    }
                    @case ('imported') {
                      <span class="pill pill-muted"
                        >Imported before as
                        <a [routerLink]="['/claims', row.claim!.id]" class="mono">{{ row.claim!.number }}</a></span
                      >
                    }
                    @case ('ready') {
                      <span class="pill pill-acc">Ready</span>
                    }
                    @case ('problems') {
                      <span class="pill pill-warn">{{ problemCount(row) }} to fix</span>
                    }
                    @default {
                      <span class="pill pill-plain">Not checked</span>
                    }
                  }
                  @if (row.verdict?.claimant; as c) {
                    <span class="imp-small text-muted">Claimant account: {{ c.displayName }}</span>
                  } @else if (row.claimantName) {
                    <span class="imp-small text-muted">From: {{ row.claimantName }}</span>
                  }
                </header>

                <div class="imp-fields">
                  @for (field of fields; track field.id) {
                    <div class="field" [class.wide]="field.id === 'incidentDescription'">
                      <label class="label" [for]="'d' + i + '-' + field.id">{{ field.label }}</label>
                      @switch (field.id) {
                        @case ('market') {
                          <select
                            class="select"
                            [id]="'d' + i + '-' + field.id"
                            [disabled]="!!row.claim"
                            (change)="edit(i, field.id, $any($event.target).value)"
                            [attr.aria-invalid]="errs(row, field.id).length > 0"
                          >
                            <option value="" [selected]="!knownMarket(row.values.market)">
                              {{
                                row.values.market && !knownMarket(row.values.market)
                                  ? row.values.market + ' (unknown)'
                                  : 'Choose a market'
                              }}
                            </option>
                            @for (m of markets; track m.code) {
                              <option [value]="m.code" [selected]="row.values.market === m.code">{{ m.name }}</option>
                            }
                          </select>
                        }
                        @case ('line') {
                          <select
                            class="select"
                            [id]="'d' + i + '-' + field.id"
                            [disabled]="!!row.claim"
                            (change)="edit(i, field.id, $any($event.target).value)"
                            [attr.aria-invalid]="errs(row, field.id).length > 0"
                          >
                            <option value="" [selected]="row.values.line !== 'Motor' && row.values.line !== 'Property'">
                              {{
                                row.values.line && row.values.line !== 'Motor' && row.values.line !== 'Property'
                                  ? row.values.line + ' (unknown)'
                                  : 'Choose a line'
                              }}
                            </option>
                            <option value="Motor" [selected]="row.values.line === 'Motor'">Motor</option>
                            <option value="Property" [selected]="row.values.line === 'Property'">Property</option>
                          </select>
                        }
                        @case ('incidentDescription') {
                          <textarea
                            class="input"
                            rows="3"
                            [id]="'d' + i + '-' + field.id"
                            [value]="row.values.incidentDescription"
                            [disabled]="!!row.claim"
                            (change)="edit(i, field.id, $any($event.target).value)"
                            [attr.aria-invalid]="errs(row, field.id).length > 0"
                          ></textarea>
                        }
                        @default {
                          <input
                            class="input"
                            [class.input-mono]="field.id === 'policyNumber' || field.id === 'incidentDate'"
                            [id]="'d' + i + '-' + field.id"
                            [value]="row.values[field.id]"
                            [disabled]="!!row.claim"
                            [attr.placeholder]="field.id === 'incidentDate' ? 'yyyy-mm-dd' : null"
                            (change)="edit(i, field.id, $any($event.target).value)"
                            [attr.aria-invalid]="errs(row, field.id).length > 0"
                          />
                        }
                      }
                      @for (m of errs(row, field.id); track m) {
                        <span class="field-error">{{ m }}</span>
                      }
                    </div>
                  }
                </div>

                @if (extra(row).length || row.createError) {
                  <div class="banner banner-err imp-inner" role="alert">
                    <span>{{ extra(row).join(' ') }} {{ row.createError ?? '' }}</span>
                  </div>
                }
                @if (row.hints.length || row.note) {
                  <details class="imp-how">
                    <summary>How this draft was read{{ row.note ? ', and the note it carries' : '' }}</summary>
                    @if (row.hints.length) {
                      <ul>
                        @for (h of row.hints; track $index) {
                          <li>{{ h }}</li>
                        }
                      </ul>
                    }
                    @if (row.note) {
                      <p class="label">Timeline note (attached when the claim is created)</p>
                      <pre class="imp-note">{{ row.note }}</pre>
                    }
                  </details>
                }
              </article>
            }
          </section>
        }
      }

      <section class="panel" aria-labelledby="imp-recent-title" data-testid="import-recent">
        <div class="panel-pad">
          <h2 class="section-title" id="imp-recent-title">Recently imported</h2>
          <p class="imp-small text-muted">
            The ledger that stops a second import of the same item. The audit log has the full record.
          </p>
        </div>
        @if (recent.isPending()) {
          <p class="panel-pad text-muted">Loading…</p>
        } @else if (recent.isError()) {
          <p class="panel-pad text-muted" role="alert">The list could not be loaded.</p>
        } @else if (!recent.data()?.length) {
          <p class="panel-pad text-muted">Nothing imported yet.</p>
        } @else {
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  <th scope="col">Claim</th>
                  <th scope="col">Status</th>
                  <th scope="col">Source</th>
                  <th scope="col">File and item</th>
                  <th scope="col">Imported by</th>
                  <th scope="col">When</th>
                </tr>
              </thead>
              <tbody>
                @for (r of recent.data(); track r.claimId) {
                  <tr>
                    <td>
                      <a [routerLink]="['/claims', r.claimId]" class="mono">{{ r.claimNumber }}</a>
                    </td>
                    <td><fg-claim-status [status]="r.status" /></td>
                    <td>{{ sourceLabel(r.source) }}</td>
                    <td>
                      {{ r.fileName }} <span class="sub">{{ r.item }}</span>
                    </td>
                    <td>{{ r.importedBy }}</td>
                    <td>{{ r.importedAt | stamp }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
    </div>
  `,
})
export class ImportPage {
  private readonly api = inject(ImportApi);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);

  protected readonly sources = SOURCES;
  protected readonly fields = FIELDS;
  protected readonly markets = MARKETS;

  protected readonly source = signal<ImportSource>('Spreadsheet');
  protected readonly current = computed(() => SOURCES.find((s) => s.id === this.source())!);
  protected readonly file = signal<ImportFileDto | null>(null);
  protected readonly mapping = signal<ColumnMapping>(guessMapping([]));
  protected readonly drafts = signal<DraftRow[]>([]);
  protected readonly busy = signal<'read' | 'check' | 'create' | null>(null);
  protected readonly uploadError = signal<string | null>(null);
  protected readonly summary = computed(() => summarise(this.drafts()));

  protected readonly recent = injectQuery(() => ({
    queryKey: importKeys.recent,
    queryFn: () => this.api.recent(),
  }));

  protected readonly state = stateOf;
  protected readonly errs = fieldErrors;
  protected readonly extra = otherErrors;

  protected choose(source: ImportSource): void {
    if (this.busy()) return;
    this.source.set(source);
    this.reset();
  }

  protected async pick(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const picked = input.files?.[0];
    if (!picked) return;
    this.reset();
    this.busy.set('read');
    try {
      const file = await this.api.readFile(this.source(), picked);
      this.file.set(file);
      if (file.sheet) {
        this.mapping.set(guessMapping(file.sheet.headers));
      } else {
        this.drafts.set(draftsFromFile(file.drafts));
        await this.check();
      }
    } catch (e) {
      this.uploadError.set(problemText(e));
    } finally {
      this.busy.set(null);
      input.value = '';
    }
  }

  protected mapColumn(field: DraftField, value: string): void {
    this.mapping.update((m) => ({ ...m, [field]: Number(value) }));
  }

  protected async useMapping(): Promise<void> {
    const sheet = this.file()?.sheet;
    if (!sheet) return;
    this.drafts.set(rowsToDrafts(sheet, this.mapping()));
    this.busy.set('check');
    try {
      await this.check();
    } finally {
      this.busy.set(null);
    }
  }

  protected async checkAll(): Promise<void> {
    this.busy.set('check');
    try {
      await this.check();
    } finally {
      this.busy.set(null);
    }
  }

  protected edit(index: number, field: DraftField, value: string): void {
    this.drafts.update((rows) => rows.map((r, i) => (i === index ? editValue(r, field, value) : r)));
  }

  protected toggle(index: number, include: boolean): void {
    this.drafts.update((rows) => rows.map((r, i) => (i === index ? { ...r, include } : r)));
  }

  /** Creates the included ready drafts one at a time, so each gets its own answer (created, already imported, or a field error). */
  protected async createAll(): Promise<void> {
    const file = this.file();
    if (!file) return;
    this.busy.set('create');
    let made = 0;
    try {
      const rows = this.drafts();
      for (let i = 0; i < rows.length; i++) {
        const row = rows[i];
        if (!row.include || stateOf(row) !== 'ready') continue;
        const body = createBody(file, row);
        try {
          const claim = await this.api.create(body);
          made++;
          this.patch(i, {
            claim: { id: claim.id, number: claim.number, now: true },
            include: false,
            createError: null,
          });
        } catch (e) {
          this.patch(i, { createError: problemText(e) });
          if (isApiProblem(e) && e.code === 'import.already_imported') await this.check();
        }
      }
    } finally {
      this.busy.set(null);
      if (made > 0) {
        this.toasts.ok(`${made} claim${made === 1 ? '' : 's'} created from ${file.fileName}`);
        void this.queryClient.invalidateQueries({ queryKey: importKeys.recent });
        void this.queryClient.invalidateQueries({ queryKey: claimKeys.all });
      }
    }
  }

  protected problemCount(row: DraftRow): number {
    return Object.values(row.verdict?.errors ?? {}).reduce((n, list) => n + list.length, 0);
  }

  protected knownMarket(code: string): boolean {
    return MARKETS.some((m) => m.code === code);
  }

  protected sourceLabel(source: ImportSource): string {
    return SOURCES.find((s) => s.id === source)?.label ?? source;
  }

  /** Checks every draft not created yet; a verdict is applied only if the draft was not edited while the check ran. */
  private async check(): Promise<void> {
    const sent = this.drafts()
      .map((row, index) => ({ index, input: toInput(row), row }))
      .filter((x) => !x.row.claim?.now);
    if (!sent.length) return;
    try {
      const verdicts = await this.api.check(sent.map((x) => x.input));
      this.drafts.update((all) =>
        all.map((row, index) => {
          const at = sent.findIndex((x) => x.index === index);
          const unchanged = at >= 0 && JSON.stringify(toInput(row)) === JSON.stringify(sent[at].input);
          return unchanged && verdicts[at] ? withVerdict(row, verdicts[at]) : row;
        }),
      );
    } catch (e) {
      this.uploadError.set(problemText(e));
    }
  }

  private patch(index: number, change: Partial<DraftRow>): void {
    this.drafts.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  }

  private reset(): void {
    this.file.set(null);
    this.drafts.set([]);
    this.uploadError.set(null);
  }
}

function problemText(e: unknown): string {
  if (isApiProblem(e)) {
    const fields = Object.values(e.errors).flat();
    return fields.length ? fields.join(' ') : (e.detail ?? e.title);
  }
  return 'Something went wrong; try again.';
}
