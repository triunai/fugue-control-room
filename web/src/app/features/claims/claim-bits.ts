import { ChangeDetectionStrategy, Component, Pipe, PipeTransform, computed, input } from '@angular/core';
import { ClaimStatus } from '../../api/models';
import { STATUS_LABEL, STATUS_TONE, formatMoney } from './claims-logic';

/** The status as a pill: colour is status only (ok/warn/err), the word is always there too. */
@Component({
  selector: 'fg-claim-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="pill" [class]="'pill pill-' + tone()">{{ label() }}</span>`,
})
export class ClaimStatusPill {
  readonly status = input.required<ClaimStatus>();
  protected readonly tone = computed(() => STATUS_TONE[this.status()]);
  protected readonly label = computed(() => STATUS_LABEL[this.status()]);
}

/** `{{ minor | money: currency }}`: minor units in, "SGD 2,500.00" out (see formatMoney for the digit rule). */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(minor: number | null | undefined, currency: string): string {
    return formatMoney(minor, currency);
  }
}
