import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type IconName = 'excel' | 'export' | 'reserve' | 'reload' | 'save' | 'info' | 'check';

/** Line icons on a 16px grid in the same style as the nav and Help icons (stroke = currentColor). No icon library. */
const PATHS: Record<IconName, string> = {
  excel: 'M3 2.5h7l3 3v8H3zM10 2.5v3h3M5.5 8h5M5.5 10.5h5M8 6.5v6',
  export: 'M8 2.5v7M5 7l3 3 3-3M3 12.5h10',
  reserve: 'M8 2l5 2v4c0 3-2.2 5-5 6-2.8-1-5-3-5-6V4zM5.8 8l1.7 1.7L10.4 6.6',
  reload: 'M13 3.5v3h-3M13 6.5A5 5 0 1 0 13.2 9',
  save: 'M3 3h8l2 2v8H3zM5.5 3v3.5h4V3M5.5 13V9.5h5V13',
  check: 'M3.5 8.5l3 3 6-7',
  info: 'M8 7.2v4M8 4.8v.1M8 1.8a6.2 6.2 0 1 0 0 12.4A6.2 6.2 0 0 0 8 1.8z',
};

/** A decorative inline icon: the button's text names the action, so the icon is hidden from assistive tech. */
@Component({
  selector: 'fg-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 16 16" width="15" height="15" fill="none" aria-hidden="true" focusable="false">
      <path
        [attr.d]="path()"
        stroke="currentColor"
        stroke-width="1.4"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
    }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  protected readonly path = computed(() => PATHS[this.name()]);
}
