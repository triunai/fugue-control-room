import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';

export interface SecretDialogData {
  name: string;
  secret: string;
  rotated: boolean;
}

/** Shows a signing secret exactly once (the API stores it encrypted and never returns it again). */
@Component({
  selector: 'fg-secret-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6">
      <h2 class="section-title">{{ data.rotated ? 'New signing secret' : 'Subscription created' }}</h2>
      <p class="mt-2 mb-0 text-muted">
        Copy the signing secret for <strong class="text-ink">{{ data.name }}</strong> now and store it with the
        receiver. You won't see it again: the API keeps it encrypted and never shows it a second time.
      </p>
      <div class="secret mt-5">
        <code class="mono" data-testid="secret">{{ data.secret }}</code>
        <button type="button" class="btn btn-sm" [class.btn-primary]="!copied()" (click)="copy()">
          {{ copied() ? 'Copied' : 'Copy secret' }}
        </button>
      </div>
      @if (data.rotated) {
        <p class="mt-3 mb-0 text-sm text-muted">The old secret stopped working the moment this one was issued.</p>
      }
      <p class="mt-3 mb-0 text-sm text-muted">
        Receivers verify <span class="mono">X-Fugue-Signature: v1=HMAC-SHA256(secret, timestamp.body)</span>.
      </p>
      <div class="mt-6 flex justify-end">
        <button type="button" class="btn" [class.btn-primary]="copied()" (click)="ref.close()">I've stored it</button>
      </div>
    </div>
  `,
  styles: `
    .secret {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem 0.75rem 0.75rem 1rem;
      border: 1.5px dashed var(--accent);
      border-radius: var(--radius-control);
      background: var(--surface2);
    }
    code {
      flex: 1;
      font-size: 0.9333rem;
      overflow-wrap: anywhere;
      color: var(--accent-text);
    }
  `,
})
export class SecretDialog {
  protected readonly data = inject<SecretDialogData>(DIALOG_DATA);
  protected readonly ref = inject(DialogRef);
  protected readonly copied = signal(false);

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.data.secret);
      this.copied.set(true);
    } catch {
      /* clipboard blocked: the value is selectable */
    }
  }
}
