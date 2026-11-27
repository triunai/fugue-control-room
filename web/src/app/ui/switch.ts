import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

let nextId = 0;

/**
 * An accessible toggle switch (role="switch"): green track when on, red when off. The tooltip is real DOM
 * (`role="tooltip"`, linked by aria-describedby) so keyboard focus shows it too. A switch that cannot be used is
 * `aria-disabled`, not `disabled`, so it stays focusable and its tooltip can say why.
 */
@Component({
  selector: 'fg-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      role="switch"
      class="sw"
      [class.sw-on]="checked()"
      [class.sw-pending]="pending()"
      [attr.aria-checked]="checked()"
      [attr.aria-disabled]="inert() ? 'true' : null"
      [attr.aria-label]="label()"
      [attr.aria-describedby]="tipId"
      (click)="onClick()"
    >
      <span class="sw-track">
        <span class="sw-thumb">
          @if (locked()) {
            <svg width="10" height="10" viewBox="0 0 16 16" aria-hidden="true">
              <rect x="3.5" y="7" width="9" height="7" rx="1.5" fill="currentColor" />
              <path d="M5.5 7V5a2.5 2.5 0 0 1 5 0v2" fill="none" stroke="currentColor" stroke-width="1.8" />
            </svg>
          }
        </span>
      </span>
    </button>
    <span class="sw-tip" role="tooltip" [id]="tipId">{{ tooltip() }}</span>
  `,
  styleUrl: './switch.css',
})
export class Switch {
  readonly checked = input.required<boolean>();
  readonly label = input.required<string>();
  readonly disabled = input(false);
  readonly pending = input(false);
  readonly locked = input(false);
  /** Overrides the default "Turn off" / "Turn on" when the switch cannot be used. */
  readonly disabledReason = input<string | null>(null);
  readonly toggled = output<void>();

  protected readonly tipId = `fg-sw-tip-${nextId++}`;
  protected readonly inert = computed(() => this.disabled() || this.pending() || this.locked());
  readonly tooltip = computed(
    () => this.disabledReason() ?? (this.locked() ? 'Always on' : this.checked() ? 'Turn off' : 'Turn on'),
  );

  protected onClick(): void {
    if (!this.inert()) this.toggled.emit();
  }
}
