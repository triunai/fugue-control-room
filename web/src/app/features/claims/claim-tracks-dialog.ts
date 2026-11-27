import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TRACK_HELP, stepHelp } from './claims-logic';

interface TrackDoc {
  name: 'Handling' | 'Payout';
  steps: { label: string; text: string }[];
}

/** "How a claim moves": the Handling and Payout tracks end to end, in plain words. Opened from the record banner. */
@Component({
  selector: 'fg-claim-tracks-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6">
      <h2 class="section-title" id="tracks-title">How a claim moves</h2>
      <p class="mt-2 text-muted">
        Every claim moves along two tracks at once. The banner at the top of the page ticks each milestone as it is
        reached.
      </p>
      @for (t of tracks; track t.name) {
        <h3 class="tracks-h">{{ t.name }}</h3>
        <p class="text-sm text-muted m-0">{{ intro(t.name) }}</p>
        <ol class="tracks-list">
          @for (s of t.steps; track s.label) {
            <li>
              <strong>{{ s.label }}</strong
              >: {{ s.text }}
            </li>
          }
        </ol>
      }
      <p class="text-sm text-muted">
        If the officer needs more information the claim pauses on Picked up until the claimant answers, then the review
        carries on. A rejected claim has no payout.
      </p>
      <div class="mt-4 flex justify-end">
        <button type="button" class="btn btn-primary" (click)="ref.close()">Close</button>
      </div>
    </div>
  `,
  styles: `
    .tracks-h {
      margin: 1.1rem 0 0.2rem;
      font: 600 1rem var(--font-ui);
    }
    .tracks-list {
      margin: 0.5rem 0 0;
      padding-left: 1.2rem;
      display: grid;
      gap: 0.35rem;
    }
  `,
})
export class ClaimTracksDialog {
  protected readonly ref = inject<DialogRef<void>>(DialogRef);
  protected readonly tracks: TrackDoc[] = [
    {
      name: 'Handling',
      steps: ['Reported', 'Picked up', 'Decided', 'Closed'].map((label) => ({ label, text: stepHelp(label) })),
    },
    {
      name: 'Payout',
      steps: ['Reserved', 'Approved', 'Paid'].map((label) => ({ label, text: stepHelp(label) })),
    },
  ];
  protected intro(name: 'Handling' | 'Payout'): string {
    return TRACK_HELP[name];
  }
}
