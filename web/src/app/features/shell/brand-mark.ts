import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The mark: three voices entering one after another on the same subject (a fugue's exposition),
 * each a note head and a held line. Pure SVG, coloured by the accent token.
 */
@Component({
  selector: 'fg-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 28 28" aria-hidden="true">
      <rect
        x="0.75"
        y="0.75"
        width="26.5"
        height="26.5"
        rx="7"
        [attr.fill]="inverse() ? 'var(--on-accent)' : 'var(--accent)'"
      />
      <g [attr.stroke]="inverse() ? 'var(--accent)' : 'var(--on-accent)'" stroke-width="1.6" stroke-linecap="round">
        <line x1="6.5" y1="8.5" x2="22" y2="8.5" />
        <line x1="10.5" y1="14" x2="22" y2="14" opacity="0.85" />
        <line x1="14.5" y1="19.5" x2="22" y2="19.5" opacity="0.7" />
      </g>
      <g [attr.fill]="inverse() ? 'var(--accent)' : 'var(--on-accent)'">
        <ellipse cx="6.5" cy="8.5" rx="2.3" ry="1.75" transform="rotate(-20 6.5 8.5)" />
        <ellipse cx="10.5" cy="14" rx="2.3" ry="1.75" transform="rotate(-20 10.5 14)" />
        <ellipse cx="14.5" cy="19.5" rx="2.3" ry="1.75" transform="rotate(-20 14.5 19.5)" />
      </g>
    </svg>
  `,
})
export class BrandMark {
  readonly size = input(30);
  readonly inverse = input(false);
}
