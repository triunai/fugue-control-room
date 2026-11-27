import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ClaimDto } from '../../api/models';
import { controlError } from '../../core/forms/server-errors';
import { formatMoney, parseMoney } from './claims-logic';

export type ClaimDialogMode = 'reserve' | 'requestInfo' | 'approve' | 'reject';

export interface ClaimDialogData {
  mode: ClaimDialogMode;
  claim: ClaimDto;
}

/** What the dialog hands back; the detail page runs the command (so a 412 lands on its banner). */
export interface ClaimDialogResult {
  amountMinor?: number;
  text?: string;
}

const COPY: Record<ClaimDialogMode, { title: string; confirm: string; textLabel?: string; hint?: string }> = {
  reserve: { title: 'Set the reserve', confirm: 'Set reserve' },
  requestInfo: {
    title: 'Ask the claimant a question',
    confirm: 'Send question',
    textLabel: 'Your question',
    hint: 'The claimant sees this word for word and answers on the claim.',
  },
  approve: {
    title: 'Approve this claim',
    confirm: 'Approve',
    textLabel: 'Reason',
    hint: 'The claimant sees the reason on their claim.',
  },
  reject: {
    title: 'Reject this claim',
    confirm: 'Reject',
    textLabel: 'Reason',
    hint: 'The claimant sees the reason on their claim.',
  },
};

/** An amount field in major units ("2,500.00"): positive, two decimals at most. */
function amount(control: AbstractControl): ValidationErrors | null {
  const v = String(control.value ?? '');
  if (!v.trim()) return { required: true };
  return parseMoney(v) === null ? { amount: true } : null;
}

@Component({
  selector: 'fg-claim-action-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <form class="p-6" [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <h2 class="section-title">{{ copy.title }}</h2>
      <p class="mt-1 mb-4 text-muted">
        <span class="mono">{{ data.claim.number }}</span> · {{ data.claim.currency }}
      </p>

      <div class="flex flex-col gap-4">
        @if (hasAmount) {
          <div class="field">
            <label class="label" for="d-amount">Amount ({{ data.claim.currency }})</label>
            <input
              id="d-amount"
              class="input input-mono"
              inputmode="decimal"
              formControlName="amount"
              autocomplete="off"
              [attr.aria-invalid]="!!amountErr()"
            />
            @if (amountErr(); as m) {
              <span class="field-error">{{ m }}</span>
            } @else if (data.mode === 'approve') {
              <span class="field-hint">At most the reserve, {{ reserveLabel }}.</span>
            } @else {
              <span class="field-hint">Internal: the claimant never sees the reserve.</span>
            }
          </div>
        }
        @if (copy.textLabel) {
          <div class="field">
            <label class="label" for="d-text">{{ copy.textLabel }}</label>
            <textarea
              id="d-text"
              class="input"
              rows="4"
              formControlName="text"
              [attr.aria-invalid]="!!textErr()"
            ></textarea>
            @if (textErr(); as m) {
              <span class="field-error">{{ m }}</span>
            } @else {
              <span class="field-hint">{{ copy.hint }}</span>
            }
          </div>
        }
      </div>

      <div class="mt-6 flex justify-end gap-2">
        <button type="button" class="btn" (click)="ref.close()">Cancel</button>
        <button
          type="submit"
          class="btn"
          [class.btn-danger-solid]="data.mode === 'reject'"
          [class.btn-primary]="data.mode !== 'reject'"
        >
          {{ copy.confirm }}
        </button>
      </div>
    </form>
  `,
})
export class ClaimActionDialog {
  protected readonly data = inject<ClaimDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<ClaimDialogResult | undefined>>(DialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly copy = COPY[this.data.mode];
  protected readonly hasAmount = this.data.mode === 'reserve' || this.data.mode === 'approve';
  protected readonly reserveLabel = formatMoney(this.data.claim.reserveMinor, this.data.claim.currency);

  protected readonly form = this.fb.group({
    amount: [this.initialAmount(), this.hasAmount ? [amount] : []],
    text: ['', this.copy.textLabel ? [Validators.required, Validators.maxLength(2000)] : []],
  });

  private initialAmount(): string {
    const minor = this.data.claim.reserveMinor;
    return minor === null || minor === undefined ? '' : (minor / 100).toFixed(2);
  }

  protected amountErr(): string | null {
    return controlError(this.form.controls.amount, {
      required: 'Enter an amount.',
      amount: 'Enter a positive amount with at most two decimals, like 2500.00.',
      overReserve: `That is more than the reserve (${this.reserveLabel}).`,
    });
  }

  protected textErr(): string | null {
    return controlError(this.form.controls.text, {
      required: this.data.mode === 'requestInfo' ? 'Write your question.' : 'Give a reason.',
      maxlength: 'That is longer than the API allows.',
    });
  }

  protected submit(): void {
    const minor = this.hasAmount ? parseMoney(this.form.controls.amount.value) : null;
    const reserve = this.data.claim.reserveMinor;
    if (
      this.data.mode === 'approve' &&
      minor !== null &&
      reserve !== null &&
      reserve !== undefined &&
      minor > reserve
    ) {
      this.form.controls.amount.setErrors({ overReserve: true });
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.ref.close({ amountMinor: minor ?? undefined, text: this.form.controls.text.value.trim() || undefined });
  }
}
