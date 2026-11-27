import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ClaimDto } from '../../api/models';
import { ClaimStatusPill } from './claim-bits';
import { Track, claimTrack, marketName, moneyTrack } from './claims-logic';
import { startTour } from './tour-hook';

/** The record banner: number and title line, status chips, meta line, Help, and the Claim and Money tracks ("n of N"). */
@Component({
  selector: 'fg-claim-record-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ClaimStatusPill],
  styleUrl: './claim-record.css',
  template: `
    <header class="record-banner" data-testid="record-banner">
      <div class="rb-top">
        <div class="rb-title">
          <h1 class="page-title">{{ claim().number }}</h1>
          <span class="rb-sub">{{ claim().line }} · {{ market() }}</span>
        </div>
        <div class="rb-chips">
          <fg-claim-status [status]="claim().status" />
          <span class="pill pill-muted" data-testid="money-chip">{{ moneyChip() }}</span>
          <button type="button" class="btn btn-sm" (click)="help()" data-testid="help-btn">Help</button>
        </div>
      </div>
      <p class="rb-meta">
        <span>Policy {{ claim().policyNumber }}</span>
        <span>Incident {{ claim().incidentDate }}</span>
        <span
          >{{ claim().market }} · <span class="mono">{{ claim().currency }}</span></span
        >
        @if (staff()) {
          <span>Claimant {{ claim().claimantName || 'unknown' }}</span>
        }
        @if (claim().assignedOfficerName; as o) {
          <span>Officer {{ o }}</span>
        }
      </p>
      <div class="rb-tracks">
        @for (t of tracks(); track t.name) {
          <div class="track" [attr.data-testid]="'track-' + t.name.toLowerCase()">
            <div class="track-head">
              <span class="track-name">{{ t.name }}</span>
              <span class="track-pos">{{ t.track.position }}</span>
            </div>
            <ol class="stepper">
              @for (s of t.track.steps; track s.label) {
                <li
                  class="step"
                  [class.done]="s.state === 'done'"
                  [class.current]="s.state === 'current'"
                  [class.next]="s.state === 'next'"
                  [class.error]="s.error"
                  [attr.aria-current]="s.state === 'current' ? 'step' : null"
                >
                  <span class="step-dot" aria-hidden="true">{{ s.state === 'done' ? '✓' : s.error ? '✕' : '' }}</span>
                  <span class="step-label">{{ s.label }}</span>
                  @if (s.dot) {
                    <span class="step-sub" [title]="s.dot">&bull; {{ s.dot }}</span>
                  }
                  @if (s.state === 'next') {
                    <span class="step-next">Next</span>
                  }
                </li>
              }
            </ol>
          </div>
        }
      </div>
    </header>
  `,
})
export class ClaimRecordBanner {
  readonly claim = input.required<ClaimDto>();
  readonly staff = input.required<boolean>();

  protected readonly market = computed(() => marketName(this.claim().market));
  protected readonly tracks = computed<{ name: string; track: Track }[]>(() => [
    { name: 'Claim', track: claimTrack(this.claim().status) },
    { name: 'Money', track: moneyTrack(this.claim(), this.staff()) },
  ]);

  /** The money status as one chip: the current step of the money track. */
  protected readonly moneyChip = computed(() => {
    const t = moneyTrack(this.claim(), this.staff());
    return `Money: ${t.steps.find((s) => s.state === 'current')?.label ?? ''}`;
  });

  protected help(): void {
    startTour('claim-detail');
  }
}
