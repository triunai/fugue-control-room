import { ChangeDetectionStrategy, Component, output } from '@angular/core';

/** The Help pill: a dark pill with a circled question mark, in the same line-icon style as the nav icons. */
@Component({
  selector: 'fg-help-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="explained" href="/explained/index.html" target="_blank" rel="noopener" data-testid="explained-link" title="Fugue, explained: the whole system on one page (opens in a new tab)">Fugue, explained</a>
    <button type="button" class="help-pill" data-testid="help" data-tour="help" title="Explain this page" (click)="pressed.emit()">
      <svg viewBox="0 0 16 16" width="15" height="15" fill="none" aria-hidden="true">
        <circle cx="8" cy="8" r="6.2" stroke="currentColor" stroke-width="1.4" />
        <path
          d="M6.3 6.2a1.8 1.8 0 1 1 2.6 1.6c-.6.3-.9.7-.9 1.3M8 11.3v.1"
          stroke="currentColor"
          stroke-width="1.4"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
      Help
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 0.75rem;
    }
    .explained {
      font-size: 0.85rem;
      color: var(--muted);
      text-decoration: none;
    }
    .explained:hover {
      color: var(--text);
      text-decoration: underline;
    }
    .help-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font: inherit;
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--on-accent);
      background: var(--text);
      border: 0;
      border-radius: 999px;
      padding: 0.3rem 0.85rem 0.3rem 0.7rem;
      cursor: pointer;
    }
    .help-pill:hover {
      background: color-mix(in oklab, var(--text) 82%, var(--accent));
    }
  `,
})
export class HelpButton {
  readonly pressed = output<void>();
}
