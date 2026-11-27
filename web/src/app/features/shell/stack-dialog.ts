import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { STACK } from './stack-info';

/** The "Stack" dialog opened from the env pill. Opened through Overlays.modal: Escape and a backdrop click close it and focus returns to the pill. */
@Component({
  selector: 'fg-stack-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stack" data-testid="stack-dialog">
      <h2 class="stack-title">What Fugue runs on</h2>
      @for (g of groups; track g.title) {
        <h3 class="section-title">{{ g.title }}</h3>
        <dl class="stack-list">
          @for (i of g.items; track i.name) {
            <dt>{{ i.name }}</dt>
            <dd>
              <span class="mono">{{ i.version }}</span>
              @if (i.note) {
                <span class="stack-note">{{ i.note }}</span>
              }
            </dd>
          }
        </dl>
      }
      <div class="stack-actions">
        <button type="button" class="btn btn-primary" data-testid="stack-close" (click)="ref.close()">Close</button>
      </div>
    </div>
  `,
  styles: `
    .stack {
      padding: 1.4rem 1.6rem;
    }
    .stack-title {
      margin: 0 0 0.6rem;
      font-family: var(--font-display);
      font-size: 1.8rem;
      font-weight: 600;
    }
    .stack-list {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.3rem 1.2rem;
      margin: 0.3rem 0 1rem;
    }
    .stack-list dt {
      color: var(--text);
    }
    .stack-list dd {
      margin: 0;
    }
    .stack-note {
      margin-left: 0.6rem;
      color: var(--muted);
      font-size: 0.85rem;
    }
    .stack-actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class StackDialog {
  protected readonly ref = inject<DialogRef<void>>(DialogRef);
  protected readonly groups = STACK;
}
