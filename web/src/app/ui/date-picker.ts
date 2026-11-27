import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  forwardRef,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

// ---------- ISO date arithmetic (UTC midnights, so no daylight-saving drift) ----------

const ISO = /^(\d{4})-(\d{2})-(\d{2})$/;
/** Day-first, as written in the region: 27/11/2026, 27-11-2026, 27.11.2026. */
const DAY_FIRST = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/;

function utc(y: number, m: number, d: number): Date | null {
  const date = new Date(Date.UTC(y, m - 1, d));
  return date.getUTCFullYear() === y && date.getUTCMonth() === m - 1 && date.getUTCDate() === d ? date : null;
}

function toIso(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** A typed date as ISO `YYYY-MM-DD`, or null when it is not a real date. Accepts ISO and day-first input. */
export function parseDate(text: string): string | null {
  const t = text.trim();
  const iso = ISO.exec(t);
  if (iso) {
    const d = utc(+iso[1], +iso[2], +iso[3]);
    return d ? toIso(d) : null;
  }
  const df = DAY_FIRST.exec(t);
  if (df) {
    const d = utc(+df[3], +df[2], +df[1]);
    return d ? toIso(d) : null;
  }
  return null;
}

function fromIso(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d));
}

export function addDays(iso: string, n: number): string {
  const d = fromIso(iso);
  d.setUTCDate(d.getUTCDate() + n);
  return toIso(d);
}

/** Same day n months on, clamped to the month's last day (31 Jan + 1 month = 28/29 Feb). */
export function addMonths(iso: string, n: number): string {
  const d = fromIso(iso);
  const target = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + n, 1));
  const last = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
  target.setUTCDate(Math.min(d.getUTCDate(), last));
  return toIso(target);
}

/** Monday = 0 … Sunday = 6. */
function weekday(iso: string): number {
  return (fromIso(iso).getUTCDay() + 6) % 7;
}

function todayIso(now = new Date()): string {
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${m}-${d}`;
}

function clamp(iso: string, min: string | null, max: string | null): string {
  if (min && iso < min) return min;
  if (max && iso > max) return max;
  return iso;
}

const MONTH = new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric', timeZone: 'UTC' });
const LONG = new Intl.DateTimeFormat('en-GB', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'UTC',
});
const WEEKDAYS = [
  { short: 'Mo', long: 'Monday' },
  { short: 'Tu', long: 'Tuesday' },
  { short: 'We', long: 'Wednesday' },
  { short: 'Th', long: 'Thursday' },
  { short: 'Fr', long: 'Friday' },
  { short: 'Sa', long: 'Saturday' },
  { short: 'Su', long: 'Sunday' },
];

interface Day {
  iso: string;
  day: number;
  label: string;
  disabled: boolean;
}

const POSITIONS: ConnectedPosition[] = [
  { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
  { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
];

let nextId = 0;

/**
 * A date field that replaces `<input type="date">` (whose calendar is drawn by the OS). Type the date (ISO
 * `2026-11-27` or day-first `27/11/2026`) or pick it from a calendar popover on the CDK overlay. Calendar keys
 * (WAI-ARIA APG date picker dialog): arrows move a day or a week, Home/End the week, PageUp/PageDown a month
 * (Shift: a year), Enter/Space choose, Escape closes. `min`/`max` (ISO) grey out days, like the native attributes.
 * A reactive-forms control: the value is ISO `YYYY-MM-DD`, or `''` while the text is empty or not a real date
 * (the native date input reports `''` for an incomplete date too).
 */
@Component({
  selector: 'fg-date',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CdkConnectedOverlay, CdkOverlayOrigin],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DatePicker), multi: true }],
  template: `
    <div class="fg-date" cdkOverlayOrigin #origin="cdkOverlayOrigin">
      <input
        #field
        class="input fg-date-input"
        [id]="inputId()"
        [value]="text()"
        [disabled]="disabled()"
        placeholder="YYYY-MM-DD"
        inputmode="numeric"
        autocomplete="off"
        maxlength="10"
        [attr.aria-invalid]="invalid() || null"
        [attr.aria-describedby]="describedBy()"
        (input)="onInput($any($event.target).value)"
        (blur)="onBlur()"
        (keydown)="onFieldKeydown($event)"
      />
      <button
        #toggleBtn
        type="button"
        class="fg-date-toggle"
        aria-haspopup="dialog"
        [attr.aria-label]="toggleLabel()"
        [attr.aria-expanded]="open()"
        [disabled]="disabled()"
        (click)="toggle()"
      >
        <svg viewBox="0 0 16 16" width="15" height="15" fill="none" aria-hidden="true" focusable="false">
          <path
            d="M3 4h10v9H3zM3 7h10M5.5 2.5v3M10.5 2.5v3"
            stroke="currentColor"
            stroke-width="1.4"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
      </button>
    </div>
    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="origin"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayPositions]="positions"
      (overlayOutsideClick)="onOutsideClick($event)"
      (detach)="open.set(false)"
    >
      <div
        #calendar
        class="fg-cal"
        role="dialog"
        aria-modal="false"
        [attr.aria-labelledby]="titleId"
        tabindex="-1"
        (keydown)="onCalendarKeydown($event)"
        (focusout)="onCalendarFocusOut($event)"
      >
        <div class="fg-cal-head">
          <button
            type="button"
            class="fg-cal-nav"
            aria-label="Previous month"
            [disabled]="!canShift(-1)"
            (click)="shift(-1)"
          >
            <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
              <path d="M10 3.5L5.5 8l4.5 4.5" fill="none" stroke="currentColor" stroke-width="1.6" />
            </svg>
          </button>
          <span class="fg-cal-title" [id]="titleId" aria-live="polite">{{ monthLabel() }}</span>
          <button type="button" class="fg-cal-nav" aria-label="Next month" [disabled]="!canShift(1)" (click)="shift(1)">
            <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
              <path d="M6 3.5L10.5 8 6 12.5" fill="none" stroke="currentColor" stroke-width="1.6" />
            </svg>
          </button>
        </div>
        <table class="fg-cal-grid" role="grid" [attr.aria-labelledby]="titleId">
          <thead>
            <tr>
              @for (w of weekdays; track w.short) {
                <th scope="col" [attr.abbr]="w.long">{{ w.short }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (week of weeks(); track $index) {
              <tr>
                @for (d of week; track $index) {
                  @if (d) {
                    <td [attr.aria-selected]="d.iso === value()">
                      <button
                        type="button"
                        class="fg-day"
                        [attr.data-iso]="d.iso"
                        [attr.tabindex]="d.iso === focused() ? 0 : -1"
                        [attr.aria-label]="d.label"
                        [attr.aria-current]="d.iso === today ? 'date' : null"
                        [attr.aria-disabled]="d.disabled || null"
                        [class.fg-day-selected]="d.iso === value()"
                        [class.fg-day-today]="d.iso === today"
                        (click)="pick(d)"
                      >
                        {{ d.day }}
                      </button>
                    </td>
                  } @else {
                    <td></td>
                  }
                }
              </tr>
            }
          </tbody>
        </table>
        <div class="fg-cal-foot">
          <span class="fg-cal-keys">Arrows move · Enter picks · Esc closes</span>
          @if (!isOut(today)) {
            <button type="button" class="fg-cal-today" (click)="pickIso(today)">Today</button>
          }
        </div>
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .fg-date {
      position: relative;
    }
    .fg-date-input {
      padding-right: 2.6rem;
      font-variant-numeric: tabular-nums;
    }
    .fg-date-toggle {
      position: absolute;
      top: 50%;
      right: 0.3rem;
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      margin-top: -1rem;
      border: 0;
      border-radius: 5px;
      background: none;
      color: var(--muted);
      cursor: pointer;
    }
    .fg-date-toggle:hover:not(:disabled),
    .fg-date-toggle[aria-expanded='true'] {
      background: var(--accent-soft);
      color: var(--accent-text);
    }
    .fg-date-toggle:focus-visible {
      outline: 2px solid var(--focus);
      outline-offset: 1px;
    }
    .fg-date-toggle:disabled {
      cursor: not-allowed;
      opacity: 0.5;
    }
    .fg-cal {
      width: 18.5rem;
      padding: 0.75rem;
      background: var(--surface2);
      border: 1px solid var(--line);
      border-radius: var(--radius-panel);
      box-shadow: var(--shadow-overlay);
      color: var(--text);
      font: 400 0.9rem/1.2 var(--font-ui);
      outline: none;
    }
    .fg-cal-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }
    .fg-cal-title {
      font-weight: 600;
    }
    .fg-cal-nav,
    .fg-cal-today {
      display: grid;
      place-items: center;
      min-width: 2rem;
      height: 2rem;
      padding: 0 0.5rem;
      border: 1px solid var(--line);
      border-radius: var(--radius-control);
      background: var(--surface);
      color: var(--text);
      font: inherit;
      cursor: pointer;
    }
    .fg-cal-nav:disabled {
      opacity: 0.4;
      cursor: not-allowed;
    }
    .fg-cal-grid {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
    }
    .fg-cal-grid th {
      padding: 0.25rem 0;
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--muted);
      text-align: center;
    }
    .fg-cal-grid td {
      padding: 1px;
      text-align: center;
    }
    .fg-day {
      width: 100%;
      height: 2.1rem;
      border: 1px solid transparent;
      border-radius: 6px;
      background: none;
      color: var(--text);
      font: inherit;
      font-variant-numeric: tabular-nums;
      cursor: pointer;
    }
    .fg-day:hover:not([aria-disabled='true']) {
      background: var(--accent-soft);
    }
    .fg-day:focus-visible {
      outline: 2px solid var(--focus);
      outline-offset: 0;
    }
    .fg-day-today {
      border-color: var(--control);
      font-weight: 600;
    }
    .fg-day-selected,
    .fg-day-selected:hover {
      background: var(--accent) !important;
      border-color: var(--accent);
      color: var(--on-accent);
      font-weight: 600;
    }
    .fg-day[aria-disabled='true'] {
      color: color-mix(in oklab, var(--muted) 55%, var(--surface2));
      cursor: not-allowed;
    }
    .fg-cal-foot {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .fg-cal-keys {
      font-size: 0.75rem;
      color: var(--muted);
    }
  `,
})
export class DatePicker implements ControlValueAccessor {
  readonly inputId = input.required<string>();
  readonly min = input<string | null>(null);
  readonly max = input<string | null>(null);
  readonly invalid = input(false);
  readonly describedBy = input<string | null>(null);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private readonly calendar = viewChild<ElementRef<HTMLElement>>('calendar');

  protected readonly titleId = `fg-cal-title-${nextId++}`;
  protected readonly positions = POSITIONS;
  protected readonly weekdays = WEEKDAYS;
  protected readonly today = todayIso();

  /** The control value: ISO or ''. */
  readonly value = signal('');
  /** What the field shows (may be half-typed). */
  protected readonly text = signal('');
  readonly open = signal(false);
  /** The day that holds the grid's one tab stop (roving tabindex). */
  readonly focused = signal(this.today);
  protected readonly disabled = signal(false);
  /** Bumped when keyboard focus should move onto `focused` after the next render. */
  private readonly focusRequest = signal(0);

  private onChange: (v: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  protected readonly monthLabel = computed(() => MONTH.format(fromIso(this.focused())));
  protected readonly toggleLabel = computed(() =>
    this.value() ? `Choose a date, ${LONG.format(fromIso(this.value()))}` : 'Choose a date',
  );

  /** The focused day's month as weeks of 7 (Monday first); cells outside the month are null. */
  protected readonly weeks = computed<(Day | null)[][]>(() => {
    const first = `${this.focused().slice(0, 7)}-01`;
    const cells: (Day | null)[] = Array.from({ length: weekday(first) }, () => null);
    for (let iso = first; iso.slice(0, 7) === first.slice(0, 7); iso = addDays(iso, 1)) {
      cells.push({ iso, day: +iso.slice(8), label: LONG.format(fromIso(iso)), disabled: this.isOut(iso) });
    }
    while (cells.length % 7) cells.push(null);
    return Array.from({ length: cells.length / 7 }, (_, w) => cells.slice(w * 7, w * 7 + 7));
  });

  constructor() {
    afterRenderEffect(() => {
      if (!this.focusRequest() || !this.open()) return;
      const iso = untracked(this.focused);
      this.calendar()?.nativeElement.querySelector<HTMLElement>(`[data-iso="${iso}"]`)?.focus();
    });
  }

  writeValue(v: string | null): void {
    this.value.set(v ?? '');
    this.text.set(v ?? '');
  }
  registerOnChange(fn: (v: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
    if (isDisabled) this.open.set(false);
  }

  protected isOut(iso: string): boolean {
    const min = this.min();
    const max = this.max();
    return (!!min && iso < min) || (!!max && iso > max);
  }

  protected canShift(months: number): boolean {
    const target = addMonths(this.focused(), months).slice(0, 7);
    const min = this.min();
    const max = this.max();
    return !(months < 0 && min && target < min.slice(0, 7)) && !(months > 0 && max && target > max.slice(0, 7));
  }

  protected onInput(raw: string): void {
    this.text.set(raw);
    this.commit(raw.trim() ? (parseDate(raw) ?? '') : '');
  }

  protected onBlur(): void {
    // A typed day-first date is shown back in the one format the form stores.
    const parsed = parseDate(this.text());
    if (parsed) this.text.set(parsed);
    this.onTouched();
  }

  protected onFieldKeydown(e: KeyboardEvent): void {
    if (e.key === 'ArrowDown' && (e.altKey || !this.open())) {
      e.preventDefault();
      this.openCalendar();
    }
  }

  protected toggle(): void {
    if (this.open()) this.close(true);
    else this.openCalendar();
  }

  protected onOutsideClick(event: MouseEvent): void {
    if (this.host.nativeElement.contains(event.target as Node)) return;
    this.close(false);
  }

  protected onCalendarFocusOut(e: FocusEvent): void {
    const to = e.relatedTarget as Node | null;
    if (!to) return;
    if (this.calendar()?.nativeElement.contains(to) || this.host.nativeElement.contains(to)) return;
    // Tabbing out of the calendar moves on through the form; the popover closes behind it.
    this.open.set(false);
    this.onTouched();
  }

  protected shift(months: number): void {
    this.focused.set(clamp(addMonths(this.focused(), months), this.min(), this.max()));
  }

  protected pick(d: Day): void {
    if (!d.disabled) this.pickIso(d.iso);
  }

  protected pickIso(iso: string): void {
    this.text.set(iso);
    this.commit(iso);
    this.close(true);
  }

  protected onCalendarKeydown(e: KeyboardEvent): void {
    const target = e.target as HTMLElement;
    const onDay = target.classList.contains('fg-day');
    const f = this.focused();
    let next: string;
    switch (e.key) {
      case 'Escape':
        // Close the calendar only, never a dialog the field sits in.
        e.preventDefault();
        e.stopPropagation();
        this.close(true);
        return;
      case 'ArrowLeft':
        next = addDays(f, -1);
        break;
      case 'ArrowRight':
        next = addDays(f, 1);
        break;
      case 'ArrowUp':
        next = addDays(f, -7);
        break;
      case 'ArrowDown':
        next = addDays(f, 7);
        break;
      case 'Home':
        next = addDays(f, -weekday(f));
        break;
      case 'End':
        next = addDays(f, 6 - weekday(f));
        break;
      case 'PageUp':
        next = addMonths(f, e.shiftKey ? -12 : -1);
        break;
      case 'PageDown':
        next = addMonths(f, e.shiftKey ? 12 : 1);
        break;
      case 'Enter':
      case ' ':
        if (!onDay) return;
        e.preventDefault();
        if (!this.isOut(f)) this.pickIso(f);
        return;
      default:
        return;
    }
    if (!onDay) return;
    e.preventDefault();
    this.focused.set(clamp(next, this.min(), this.max()));
    this.focusRequest.update((n) => n + 1);
  }

  private openCalendar(): void {
    if (this.disabled()) return;
    this.focused.set(clamp(this.value() || this.today, this.min(), this.max()));
    this.open.set(true);
    this.focusRequest.update((n) => n + 1);
  }

  private close(returnFocus: boolean): void {
    this.open.set(false);
    if (returnFocus) this.field().nativeElement.focus();
  }

  private commit(v: string): void {
    if (v === this.value()) return;
    this.value.set(v);
    this.onChange(v);
  }
}
