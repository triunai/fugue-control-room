import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { ClaimsApi } from '../../api/claims.api';
import { ClaimDto, ClaimStatus } from '../../api/models';
import { isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { Icon } from '../../ui/icon';

export type LetterKind = 'settlement' | 'decision';

/** Which sample letter a claim has: Approved and Settled a settlement letter, Rejected a decision letter, anything else none (the API answers 409). */
export function letterKind(status: ClaimStatus): LetterKind | null {
  if (status === 'Approved' || status === 'Settled') return 'settlement';
  if (status === 'Rejected') return 'decision';
  return null;
}

/** The download name, the same one the API puts in Content-Disposition. */
export function letterFileName(claimNumber: string, kind: LetterKind): string {
  return `${claimNumber}-${kind}-letter.pdf`;
}

/** The claim header's one letter action: downloads the sample PDF letter of a decided claim (item 11b). Hidden until the claim is decided. */
@Component({
  selector: 'fg-claim-letter-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    @if (kind(); as k) {
      <button
        type="button"
        class="letter-pill"
        data-testid="download-letter"
        [disabled]="busy()"
        [title]="'Download the ' + k + ' letter as a PDF: a sample document, not a legal letter'"
        (click)="download(k)"
      >
        <fg-icon name="export" />
        {{ busy() ? 'Preparing letter…' : k === 'settlement' ? 'Settlement letter' : 'Decision letter' }}
      </button>
    }
  `,
  styles: `
    .letter-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font: inherit;
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--accent-text);
      background: var(--on-band);
      border: 1.5px solid var(--gold);
      border-radius: 999px;
      padding: 0.25rem 0.85rem 0.25rem 0.7rem;
      cursor: pointer;
    }
    .letter-pill:hover:not(:disabled) {
      background: color-mix(in oklab, var(--gold) 18%, var(--on-band));
    }
    .letter-pill:disabled {
      opacity: 0.7;
      cursor: progress;
    }
  `,
})
export class ClaimLetterButton {
  readonly claim = input.required<ClaimDto>();

  protected readonly kind = computed(() => letterKind(this.claim().status));
  protected readonly busy = signal(false);
  private readonly api = inject(ClaimsApi);
  private readonly toasts = inject(ToastStore);

  protected async download(kind: LetterKind): Promise<void> {
    const claim = this.claim();
    this.busy.set(true);
    try {
      const url = URL.createObjectURL(await this.api.letter(claim.id));
      const a = document.createElement('a');
      a.href = url;
      a.download = letterFileName(claim.number, kind);
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (e) {
      if (!isApiProblem(e)) throw e;
      this.toasts.problem(e, 'The letter could not be downloaded.');
    } finally {
      this.busy.set(false);
    }
  }
}
