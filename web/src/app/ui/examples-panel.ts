import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface ExampleRow {
  id: string;
  title: string;
  /** A short label on the right (market and line, a currency, a role). */
  tag?: string;
  /** One quieter line under the title. */
  detail?: string;
}

let nextId = 0;

/**
 * "Fill with an example": the sign-in page's demo-accounts box for any form. Each row is a button that fills the form
 * with made-up values (see `examples.ts`); the form still validates and submits as usual.
 */
@Component({
  selector: 'fg-examples',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="ex" [attr.aria-labelledby]="headId" data-testid="examples">
      <p class="ex-head" [id]="headId">
        Fill with an example
        <span class="pill pill-warn pill-plain">Sample data</span>
      </p>
      <p class="ex-note">{{ note() }}</p>
      <ul role="list">
        @for (r of rows(); track r.id) {
          <li>
            <button type="button" class="ex-row" [disabled]="disabled()" (click)="picked.emit(r.id)">
              <span class="ex-title">{{ r.title }}</span>
              @if (r.tag) {
                <span class="ex-tag">{{ r.tag }}</span>
              }
              @if (r.detail) {
                <span class="ex-detail">{{ r.detail }}</span>
              }
            </button>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .ex {
      padding: 0.75rem 0.85rem 0.4rem;
      border: 1.5px dashed var(--control);
      border-radius: var(--radius-panel);
      background: var(--surface);
    }
    .ex-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      margin: 0;
      font-weight: 600;
    }
    .ex-note {
      margin: 0.2rem 0 0.4rem;
      font-size: 0.8667rem;
      color: var(--muted);
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .ex-row {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 0.1rem 0.75rem;
      width: 100%;
      padding: 0.5rem 0.5rem;
      border: 0;
      border-top: 1px solid var(--line-soft);
      border-radius: 6px;
      background: none;
      color: var(--text);
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    li:first-child .ex-row {
      border-top: 0;
    }
    .ex-row:hover:not(:disabled) {
      background: var(--accent-soft);
    }
    .ex-row:focus-visible {
      outline: 2px solid var(--focus);
      outline-offset: -2px;
    }
    .ex-row:disabled {
      cursor: not-allowed;
      opacity: 0.6;
    }
    .ex-title {
      min-width: 0;
    }
    .ex-tag {
      font-size: 0.8667rem;
      font-weight: 600;
      color: var(--accent-text);
    }
    .ex-detail {
      grid-column: 1 / -1;
      overflow: hidden;
      font-size: 0.8667rem;
      color: var(--muted);
      white-space: nowrap;
      text-overflow: ellipsis;
    }
  `,
})
export class ExamplesPanel {
  readonly rows = input.required<readonly ExampleRow[]>();
  readonly note = input('Made-up values to try the form. Check them, then submit as usual.');
  readonly disabled = input(false);
  readonly picked = output<string>();

  protected readonly headId = `fg-ex-head-${nextId++}`;
}
