import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ClaimDto } from '../../api/models';
import { Actor, NextAction, nextStep } from './claims-logic';

/** The "Next step" card: chip, one instruction, why, the checklist, ONE primary button and quiet secondaries. No button when there is nothing to do. */
@Component({
  selector: 'fg-claim-next-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './claim-record.css',
  template: `
    @if (step(); as s) {
      <section [class]="'panel panel-pad next-card tone-' + s.tone" aria-label="Next step" data-testid="next-step">
        <div class="nc-head">
          <div class="nc-label">
            <h2 class="section-title m-0">Next step</h2>
            <span [class]="'pill pill-' + s.tone" data-testid="next-chip">{{ s.chip }}</span>
            @if (s.caption) {
              <span class="text-muted text-sm" data-testid="next-caption">{{ s.caption }}</span>
            }
          </div>
          <div class="nc-actions">
            @for (a of s.secondary; track a.kind) {
              <button
                type="button"
                class="btn btn-sm"
                [class.btn-danger]="a.kind === 'reject'"
                [disabled]="busy()"
                [attr.data-action]="a.kind"
                (click)="act.emit(a.kind)"
              >
                {{ a.label }}
              </button>
            }
            @if (s.primary; as p) {
              <button
                type="button"
                class="btn btn-primary"
                [disabled]="busy()"
                [attr.data-action]="p.kind"
                data-testid="next-primary"
                (click)="act.emit(p.kind)"
              >
                {{ p.label }}
              </button>
            }
          </div>
        </div>
        <p class="nc-instruction" data-testid="next-instruction">{{ s.instruction }}</p>
        @if (s.question) {
          <p class="question" data-testid="next-question">{{ s.question }}</p>
        }
        <p class="nc-why">{{ s.why }}</p>
        @if (s.blockers.length) {
          <ul class="checklist" aria-label="Checklist" data-testid="next-checklist">
            @for (b of s.blockers; track b.label) {
              <li>
                <span [class]="b.ok ? 'check-ok' : 'check-no'" aria-hidden="true">{{ b.ok ? '✓' : '✕' }}</span>
                <span>
                  <span class="visually-hidden">{{ b.ok ? 'Done: ' : 'Not yet: ' }}</span>{{ b.label }}
                </span>
              </li>
            }
          </ul>
        }
      </section>
    }
  `,
})
export class ClaimNextStep {
  readonly claim = input.required<ClaimDto>();
  readonly actor = input.required<Actor>();
  readonly busy = input(false);
  readonly act = output<NextAction>();

  protected readonly step = computed(() => nextStep(this.claim(), this.actor()));
}
