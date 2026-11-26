import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { EVENT_TYPES } from '../../api/webhooks.api';
import { CreateWebhookSubscriptionRequest } from '../../api/models';
import { applyServerErrors, controlError } from '../../core/forms/server-errors';
import { ApiProblem } from '../../core/http/problem';

export interface SubscriptionDialogData {
  development: boolean;
  submit: (body: CreateWebhookSubscriptionRequest) => Promise<void>;
}

const PRIVATE_HOST =
  /^(localhost|127\.|10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.|169\.254\.|0\.0\.0\.0|\[?::1\]?|\[?f[cd][0-9a-f]{2}:|100\.(6[4-9]|[7-9]\d|1[01]\d|12[0-7])\.)/i;

/** Client-side mirror of the API's SSRF rules, so most mistakes are explained before a round trip. The API stays authoritative. */
function webhookUrl(development: boolean) {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const raw = control.value?.trim();
    if (!raw) return null;
    let url: URL;
    try {
      url = new URL(raw);
    } catch {
      return { url: 'Enter a full address, starting with https://' };
    }
    if (url.username || url.password) return { url: 'Remove the user name and password from the address.' };
    if (url.hash) return { url: 'Remove the #fragment; receivers never see it.' };
    if (url.hostname.startsWith('169.254.'))
      return { url: 'Link-local addresses (cloud metadata) are always refused, even in Development.' };
    const local = PRIVATE_HOST.test(url.hostname);
    if (local && !development)
      return {
        url: 'Private, loopback and internal addresses are refused so the server cannot be used to reach internal systems.',
      };
    if (url.protocol !== 'https:' && !(development && local))
      return { url: 'Use https. Plain http is allowed only for local addresses in Development.' };
    return null;
  };
}

@Component({
  selector: 'fg-subscription-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <form class="p-6" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <h2 class="section-title">New webhook subscription</h2>
      <p class="mt-2 mb-0 text-muted">
        The API signs every event it sends here. You get the signing secret once, after saving.
      </p>

      <div class="mt-5 flex flex-col gap-4">
        <div class="field">
          <label class="label" for="w-name">Name</label>
          <input
            id="w-name"
            class="input"
            formControlName="name"
            placeholder="Billing sync"
            [attr.aria-invalid]="!!err('name')"
          />
          @if (err('name'); as m) {
            <span class="field-error">{{ m }}</span>
          }
        </div>
        <div class="field">
          <label class="label" for="w-url">Receiver address</label>
          <input
            id="w-url"
            class="input input-mono"
            formControlName="url"
            placeholder="https://receiver.example.com/hooks/fugue"
            [attr.aria-invalid]="!!err('url')"
          />
          @if (err('url'); as m) {
            <span class="field-error">{{ m }}</span>
          } @else {
            <span class="field-hint">Public https only. Addresses are checked again when each event is sent.</span>
          }
        </div>
        <fieldset class="field">
          <legend class="label mb-1.5">Events</legend>
          <div class="events">
            @for (t of choices; track t) {
              <label class="event" [class.on]="selected().includes(t)">
                <input type="checkbox" [checked]="selected().includes(t)" (change)="toggle(t)" />
                <span class="mono">{{ t }}</span>
              </label>
            }
          </div>
          @if (eventsError(); as m) {
            <span class="field-error">{{ m }}</span>
          } @else {
            <span class="field-hint"
              ><span class="mono">customer.*</span> covers every customer event, including future versions.</span
            >
          }
        </fieldset>
        @if (formErrors().length) {
          <div class="banner banner-err" role="alert">
            <span>{{ formErrors().join(' ') }}</span>
          </div>
        }
      </div>

      <div class="mt-6 flex justify-end gap-2">
        <button type="button" class="btn" (click)="ref.close()">Cancel</button>
        <button type="submit" class="btn btn-primary" [disabled]="busy()">
          {{ busy() ? 'Creating…' : 'Create subscription' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    fieldset {
      border: 0;
      margin: 0;
      padding: 0;
    }
    .events {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.4rem;
    }
    .event {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.45rem 0.6rem;
      border: 1.5px solid var(--line);
      border-radius: var(--radius-control);
      background: var(--surface2);
      cursor: pointer;
    }
    .event.on {
      border-color: var(--accent);
      background: var(--accent-soft);
    }
    .event input {
      accent-color: var(--accent);
      width: 1rem;
      height: 1rem;
    }
  `,
})
export class SubscriptionDialog {
  protected readonly data = inject<SubscriptionDialogData>(DIALOG_DATA);
  protected readonly ref = inject(DialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly choices = ['customer.*', '*', ...EVENT_TYPES];
  protected readonly selected = signal<string[]>(['customer.*']);
  protected readonly eventsError = signal<string | null>(null);
  protected readonly formErrors = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    url: ['', [Validators.required, webhookUrl(this.data.development)]],
  });

  protected err(name: 'name' | 'url'): string | null {
    const c = this.form.controls[name];
    if (c.touched && c.errors?.['url']) return c.errors['url'];
    return controlError(c, {
      required: name === 'name' ? 'Give the subscription a name.' : 'Enter the receiver address.',
    });
  }

  protected toggle(t: string): void {
    this.selected.update((s) => (s.includes(t) ? s.filter((x) => x !== t) : [...s, t]));
    this.eventsError.set(null);
  }

  protected async save(): Promise<void> {
    this.formErrors.set([]);
    if (!this.selected().length) this.eventsError.set('Choose at least one event.');
    if (this.form.invalid || !this.selected().length) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    try {
      const v = this.form.getRawValue();
      await this.data.submit({ name: v.name.trim(), url: v.url.trim(), eventTypes: this.selected() });
      this.ref.close(true);
    } catch (e) {
      if (e instanceof ApiProblem && e.isValidation) {
        const rest = applyServerErrors(this.form, e);
        if (e.errors['eventTypes']) this.eventsError.set(e.errors['eventTypes'].join(' '));
        this.formErrors.set(rest.filter((m) => !e.errors['eventTypes']?.includes(m)));
      } else if (e instanceof ApiProblem) {
        this.formErrors.set([e.detail ?? e.title]);
      }
    } finally {
      this.busy.set(false);
    }
  }
}
