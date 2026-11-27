import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ClaimDto } from '../../api/models';
import { ClaimStatusPill } from './claim-bits';
import { Actor, Track, claimTrack, marketName, moneyChip, moneyTrack } from './claims-logic';
import { startTour } from './tour-hook';

/** The record banner: number and title line, status chips, meta line, Help, and the Claim and Money tracks of milestones reached. */
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
          @if (staff()) {
            <span class="pill pill-muted" data-testid="money-chip">{{ chip() }}</span>
          }
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
            @if (t.track.note; as n) {
              <p class="track-note" data-testid="track-note">{{ n }}</p>
            } @else {
              <ol class="stepper">
                @for (s of t.track.steps; track s.label) {
                  <li
                    class="step"
                    [class.done]="s.checked"
                    [class.current]="s.state === 'current'"
                    [class.next]="s.state === 'next'"
                    [class.warn]="s.tone === 'warn'"
                    [class.error]="s.tone === 'error'"
                    [attr.aria-current]="s.state === 'current' ? 'step' : null"
                  >
                    <span class="step-dot" aria-hidden="true">{{
                      s.tone === 'error' ? '✕' : s.checked ? '✓' : ''
                    }}</span>
                    <span class="step-label">{{ s.label }}</span>
                    @if (s.checked && s.date) {
                      <span class="step-date">{{ s.date.slice(0, 10) }}</span>
                    }
                    @if (s.state === 'next') {
                      <span class="step-next">Next{{ s.caption ? ' · ' + s.caption : '' }}</span>
                    } @else if (s.caption) {
                      <span class="step-sub">{{ s.caption }}</span>
                    }
                  </li>
                }
              </ol>
            }
          </div>
        }
      </div>
    </header>
  `,
})
export class ClaimRecordBanner {
  readonly claim = input.required<ClaimDto>();
  readonly actor = input.required<Actor>();

  protected readonly staff = computed(() => this.actor().permissions.has('CLAIMS_ALL'));
  protected readonly market = computed(() => marketName(this.claim().market));
  protected readonly chip = computed(() => moneyChip(this.claim()));
  protected readonly tracks = computed<{ name: string; track: Track }[]>(() => [
    { name: 'Claim', track: claimTrack(this.claim(), this.actor()) },
    { name: 'Money', track: moneyTrack(this.claim(), this.actor()) },
  ]);

  protected help(): void {
    startTour('claim-detail');
  }
}
