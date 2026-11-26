import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';

/** A mono id chip; click copies the full value. Used for correlation refs, saga, trace and entity ids. */
@Component({
  selector: 'fg-ref-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="chip"
      [title]="copied() ? 'Copied' : 'Copy ' + (label() || 'value') + ': ' + value()"
      (click)="copy($event)"
    >
      @if (label()) {
        <span class="chip-key">{{ label() }}</span>
      }
      <span class="chip-value" [style.max-width]="maxWidth()">{{ copied() ? 'copied' : display() }}</span>
      <svg width="12" height="12" viewBox="0 0 16 16" aria-hidden="true" class="opacity-60">
        <rect x="5" y="5" width="9" height="9" rx="1.5" fill="none" stroke="currentColor" stroke-width="1.6" />
        <path d="M3 11V3.5A1.5 1.5 0 0 1 4.5 2H11" fill="none" stroke="currentColor" stroke-width="1.6" />
      </svg>
    </button>
  `,
})
export class RefChip {
  readonly value = input.required<string>();
  readonly label = input<string>('');
  /** Show only the first N characters (the full value is copied and in the tooltip). */
  readonly short = input<number>(0);
  readonly maxWidth = input<string>('18ch');
  protected readonly copied = signal(false);

  protected display(): string {
    const v = this.value();
    const n = this.short();
    return n > 0 && v.length > n ? v.slice(0, n) + '…' : v;
  }

  protected async copy(event: Event): Promise<void> {
    event.stopPropagation();
    try {
      await navigator.clipboard.writeText(this.value());
    } catch {
      return;
    }
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1400);
  }
}
