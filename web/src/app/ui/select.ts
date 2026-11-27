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
  viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SelectOption {
  value: string;
  label: string;
  /** A second, quieter line (a code, a currency). */
  hint?: string;
  disabled?: boolean;
}

let nextId = 0;

const POSITIONS: ConnectedPosition[] = [
  { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
  { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
];

/** How long a type-ahead run stays open between key presses. */
const TYPEAHEAD_MS = 600;

/**
 * A select-only combobox (WAI-ARIA APG pattern) that replaces the native `<select>`, whose popup is drawn by the OS.
 * Focus stays on the trigger button; the open option is announced with `aria-activedescendant`. Keys: Arrow keys,
 * Home/End, Enter/Space, Escape, Tab (keeps the highlighted option), and type-ahead. A reactive-forms control
 * (ControlValueAccessor): the value is the option's `value` string, `''` when nothing is chosen.
 */
@Component({
  selector: 'fg-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CdkConnectedOverlay, CdkOverlayOrigin],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => Select), multi: true }],
  template: `
    <button
      #trigger
      type="button"
      cdkOverlayOrigin
      #origin="cdkOverlayOrigin"
      class="select fg-select-trigger"
      role="combobox"
      aria-haspopup="listbox"
      [class.mono]="mono()"
      [id]="inputId()"
      [disabled]="disabled()"
      [attr.aria-expanded]="open()"
      [attr.aria-controls]="listId"
      [attr.aria-activedescendant]="open() && active() >= 0 ? optionId(active()) : null"
      [attr.aria-invalid]="invalid() || null"
      [attr.aria-describedby]="describedBy()"
      (click)="toggle()"
      (keydown)="onKeydown($event)"
      (blur)="onBlur()"
    >
      <span class="fg-select-value" [class.fg-select-placeholder]="!selected()">{{
        selected()?.label ?? placeholder()
      }}</span>
    </button>
    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="origin"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayPositions]="positions"
      [cdkConnectedOverlayMinWidth]="triggerWidth()"
      (overlayOutsideClick)="onOutsideClick($event)"
      (detach)="close()"
    >
      <ul class="fg-listbox" role="listbox" [id]="listId" [attr.aria-labelledby]="inputId()" tabindex="-1">
        @for (o of options(); track o.value; let i = $index) {
          <li
            role="option"
            class="fg-option"
            tabindex="-1"
            [id]="optionId(i)"
            [class.fg-option-active]="i === active()"
            [attr.aria-selected]="o.value === value()"
            [attr.aria-disabled]="o.disabled || null"
            (mousedown)="$event.preventDefault()"
            (mouseenter)="hover(i)"
            (click)="choose(i)"
            (keydown.enter)="choose(i)"
          >
            <span class="fg-option-label" [class.mono]="mono()">{{ o.label }}</span>
            @if (o.hint) {
              <span class="fg-option-hint">{{ o.hint }}</span>
            }
            @if (o.value === value()) {
              <svg class="fg-option-check" viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
                <path
                  d="M3.5 8.5l3 3 6-7"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.8"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
              </svg>
            }
          </li>
        } @empty {
          <li class="fg-option fg-option-empty" role="option" aria-disabled="true" aria-selected="false">
            Nothing to choose
          </li>
        }
      </ul>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .fg-select-trigger {
      display: flex;
      align-items: center;
      text-align: left;
      cursor: pointer;
    }
    .fg-select-trigger:disabled {
      cursor: not-allowed;
      opacity: 0.65;
    }
    .fg-select-trigger.mono {
      font-family: var(--font-mono);
      font-size: 0.8667rem;
    }
    .fg-select-value {
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .fg-select-placeholder {
      color: var(--muted);
    }
    .fg-listbox {
      flex: 1;
      min-width: 0;
      list-style: none;
      margin: 0;
      padding: 0.3rem;
      max-height: 16rem;
      overflow-y: auto;
      background: var(--surface2);
      border: 1px solid var(--line);
      border-radius: var(--radius-control);
      box-shadow: var(--shadow-overlay);
      font: 400 0.9667rem/1.25 var(--font-ui);
      color: var(--text);
      outline: none;
    }
    .fg-option {
      display: flex;
      align-items: baseline;
      gap: 0.6rem;
      padding: 0.5rem 0.6rem;
      border-radius: 5px;
      cursor: pointer;
    }
    .fg-option-active {
      background: var(--accent-soft);
    }
    .fg-option[aria-selected='true'] {
      font-weight: 600;
      color: var(--accent-text);
    }
    .fg-option[aria-disabled='true'] {
      color: var(--muted);
      cursor: default;
    }
    .fg-option-label {
      flex: 1;
      min-width: 0;
    }
    .fg-option-label.mono {
      font-family: var(--font-mono);
      font-size: 0.8667rem;
    }
    .fg-option-hint {
      font-size: 0.8rem;
      color: var(--muted);
      font-weight: 400;
    }
    .fg-option-check {
      align-self: center;
      flex: none;
    }
  `,
})
export class Select implements ControlValueAccessor {
  /** The trigger's id, so an outside `<label for>` names it. */
  readonly inputId = input.required<string>();
  readonly options = input.required<readonly SelectOption[]>();
  readonly placeholder = input('Choose…');
  readonly invalid = input(false);
  readonly mono = input(false);
  readonly describedBy = input<string | null>(null);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  protected readonly listId = `fg-listbox-${nextId++}`;
  protected readonly positions = POSITIONS;

  readonly value = signal('');
  readonly open = signal(false);
  /** The highlighted option while open (visual focus); -1 when none. */
  readonly active = signal(-1);
  protected readonly disabled = signal(false);
  /** The list is at least as wide as the trigger (measured when it opens). */
  protected readonly triggerWidth = signal(0);
  protected readonly selected = computed(() => this.options().find((o) => o.value === this.value()) ?? null);

  private typed = '';
  private typedAt = 0;
  private onChange: (v: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    afterRenderEffect(() => {
      const i = this.active();
      if (!this.open() || i < 0) return;
      // The listbox scrolls so the highlighted option stays visible (jsdom has no scrollIntoView).
      document.getElementById(this.optionId(i))?.scrollIntoView?.({ block: 'nearest' });
    });
  }

  protected optionId(i: number): string {
    return `${this.listId}-opt-${i}`;
  }

  writeValue(v: string | null): void {
    this.value.set(v ?? '');
  }
  registerOnChange(fn: (v: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
    if (isDisabled) this.close();
  }

  protected toggle(): void {
    if (this.open()) this.close();
    else this.openAt(this.selectedIndex());
  }

  protected onBlur(): void {
    // Focus never leaves the trigger while the list is open (options keep it on mousedown), so a blur is a real leave.
    this.close();
    this.onTouched();
  }

  protected onOutsideClick(event: MouseEvent): void {
    // A click on the trigger itself is handled by toggle(); closing here first would reopen it.
    if (this.host.nativeElement.contains(event.target as Node)) return;
    this.close();
  }

  protected hover(i: number): void {
    if (!this.options()[i]?.disabled) this.active.set(i);
  }

  protected choose(i: number): void {
    const o = this.options()[i];
    if (!o || o.disabled) return;
    this.commit(o.value);
    this.close();
  }

  private chooseAndClose(): void {
    this.choose(this.active());
    this.close();
  }

  close(): void {
    this.open.set(false);
  }

  protected onKeydown(e: KeyboardEvent): void {
    if (this.disabled()) return;
    const open = this.open();
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowUp':
        e.preventDefault();
        if (!open) this.openAt(this.selectedIndex());
        else if (e.altKey && e.key === 'ArrowUp') this.chooseAndClose();
        else this.move(e.key === 'ArrowDown' ? 1 : -1);
        return;
      case 'Home':
      case 'End':
        e.preventDefault();
        this.openAt(e.key === 'Home' ? this.firstEnabled(0, 1) : this.firstEnabled(this.options().length - 1, -1));
        return;
      case 'PageDown':
      case 'PageUp':
        if (!open) return;
        e.preventDefault();
        this.move(e.key === 'PageDown' ? 10 : -10);
        return;
      case 'Enter':
        e.preventDefault();
        if (open) this.choose(this.active());
        else this.openAt(this.selectedIndex());
        return;
      case ' ':
        if (open && this.typing()) break;
        e.preventDefault();
        if (open) this.choose(this.active());
        else this.openAt(this.selectedIndex());
        return;
      case 'Escape':
        if (!open) return;
        // Escape closes the list only; it must not also close a dialog the select sits in.
        e.preventDefault();
        e.stopPropagation();
        this.close();
        return;
      case 'Tab':
        // APG: Tab keeps the highlighted option and moves on (no preventDefault).
        if (open) this.chooseAndClose();
        return;
    }
    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault();
      this.typeahead(e.key);
    }
  }

  private typing(): boolean {
    return this.typed.length > 0 && Date.now() - this.typedAt < TYPEAHEAD_MS;
  }

  /** Type-ahead: the first option whose label starts with what was typed; one repeated letter cycles through matches. */
  private typeahead(key: string): void {
    const now = Date.now();
    this.typed = now - this.typedAt < TYPEAHEAD_MS ? this.typed + key.toLowerCase() : key.toLowerCase();
    this.typedAt = now;
    const opts = this.options();
    const cycling = this.typed.length > 1 && [...this.typed].every((c) => c === this.typed[0]);
    const needle = cycling ? this.typed[0] : this.typed;
    const from = this.open() ? this.active() : this.selectedIndex();
    const start = needle.length === 1 ? from + 1 : Math.max(from, 0);
    for (let k = 0; k < opts.length; k++) {
      const i = (start + k) % opts.length;
      if (!opts[i].disabled && opts[i].label.toLowerCase().startsWith(needle)) {
        this.openAt(i);
        return;
      }
    }
    if (!this.open()) this.openAt(this.selectedIndex());
  }

  private openAt(i: number): void {
    if (this.disabled()) return;
    this.triggerWidth.set(this.trigger().nativeElement.getBoundingClientRect().width);
    this.active.set(i >= 0 ? i : this.firstEnabled(0, 1));
    this.open.set(true);
  }

  private move(delta: number): void {
    const opts = this.options();
    if (!opts.length) return;
    let i = this.active();
    const step = Math.sign(delta);
    for (let n = Math.abs(delta); n > 0; n--) {
      const next = this.firstEnabled(i + step, step);
      if (next < 0) break;
      i = next;
    }
    this.active.set(i < 0 ? this.firstEnabled(0, 1) : i);
  }

  /** The first enabled option from `from` walking by `step`, or -1. */
  private firstEnabled(from: number, step: number): number {
    const opts = this.options();
    for (let i = from; i >= 0 && i < opts.length; i += step) if (!opts[i].disabled) return i;
    return -1;
  }

  private selectedIndex(): number {
    return this.options().findIndex((o) => o.value === this.value());
  }

  private commit(v: string): void {
    if (v === this.value()) return;
    this.value.set(v);
    this.onChange(v);
  }
}
