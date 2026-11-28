import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { injectMutation, QueryClient } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { SubmitClaimRequest } from '../../api/models';
import { applyServerErrors, controlError } from '../../core/forms/server-errors';
import { CorrelationService } from '../../core/http/correlation.service';
import { SubmissionKey } from '../../core/http/idempotency';
import { isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { DatePicker } from '../../ui/date-picker';
import { daysAgo, INCIDENT_EXAMPLES, incidentExamplesFor, samplePolicy } from '../../ui/examples';
import { ExamplesPanel } from '../../ui/examples-panel';
import { Select, SelectOption } from '../../ui/select';
import { TourService } from '../shell/tour.service';
import { MARKETS, currencyOf, localToday } from './claims-logic';

type Field = 'market' | 'line' | 'policyNumber' | 'incidentDate' | 'incidentDescription' | 'incidentLocation';

/** Report an incident: one form, field errors from the API on the fields, one Idempotency-Key per submission. */
@Component({
  selector: 'fg-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, Select, DatePicker, ExamplesPanel],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Report an incident</h1>
          <p class="page-lede">
            Tell us what happened. We will confirm the claim straight away; an officer then reviews it and may come back
            to you here with questions.
          </p>
        </div>
      </header>

      <form class="report-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <fg-examples
          class="wide"
          [rows]="exampleRows()"
          [disabled]="submitMutation.isPending()"
          note="Made-up incidents for each market and line. Pick a market or line first to narrow the list."
          (picked)="fillExample($event)"
        />
        <div class="field">
          <label class="label" for="r-market">Market</label>
          <fg-select
            inputId="r-market"
            formControlName="market"
            placeholder="Choose a market"
            [options]="marketOptions"
            [invalid]="!!err('market')"
          />
          @if (err('market'); as m) {
            <span class="field-error">{{ m }}</span>
          }
        </div>
        <div class="field">
          <label class="label" for="r-currency">Currency</label>
          <input id="r-currency" class="input input-mono" [value]="currency() || '—'" readonly aria-readonly="true" />
          <span class="field-hint">Set by the market; you cannot change it.</span>
        </div>
        <div class="field">
          <label class="label" for="r-line">Line of cover</label>
          <fg-select
            inputId="r-line"
            formControlName="line"
            placeholder="Choose a line"
            [options]="lineOptions"
            [invalid]="!!err('line')"
          />
          @if (err('line'); as m) {
            <span class="field-error">{{ m }}</span>
          }
        </div>
        <div class="field">
          <label class="label" for="r-policy">Policy number</label>
          <input
            id="r-policy"
            class="input"
            formControlName="policyNumber"
            autocomplete="off"
            [attr.aria-invalid]="!!err('policyNumber')"
          />
          @if (err('policyNumber'); as m) {
            <span class="field-error">{{ m }}</span>
          }
        </div>
        <div class="field">
          <label class="label" for="r-date">Incident date</label>
          <fg-date inputId="r-date" formControlName="incidentDate" [max]="today" [invalid]="!!err('incidentDate')" />
          @if (err('incidentDate'); as m) {
            <span class="field-error">{{ m }}</span>
          } @else {
            <span class="field-hint">Not in the future, and within the last year.</span>
          }
        </div>
        <div class="field">
          <label class="label" for="r-where">Where did it happen? <span class="font-normal">(optional)</span></label>
          <input
            id="r-where"
            class="input"
            formControlName="incidentLocation"
            autocomplete="off"
            [attr.aria-invalid]="!!err('incidentLocation')"
          />
          @if (err('incidentLocation'); as m) {
            <span class="field-error">{{ m }}</span>
          }
        </div>
        <div class="field wide">
          <label class="label" for="r-what">What happened</label>
          <textarea
            id="r-what"
            class="input"
            rows="6"
            formControlName="incidentDescription"
            [attr.aria-invalid]="!!err('incidentDescription')"
          ></textarea>
          @if (err('incidentDescription'); as m) {
            <span class="field-error">{{ m }}</span>
          } @else {
            <span class="field-hint">A few sentences: what, how, and what was damaged.</span>
          }
        </div>
        @if (formErrors().length) {
          <div class="banner banner-err wide" role="alert">
            <span><span class="banner-title">We could not submit this.</span> {{ formErrors().join(' ') }}</span>
          </div>
        }
        <div class="wide actions">
          <button type="submit" class="btn btn-primary" [disabled]="submitMutation.isPending()">
            {{ submitMutation.isPending() ? 'Submitting…' : 'Submit claim' }}
          </button>
          <a routerLink="/claims/mine" class="btn">Cancel</a>
        </div>
      </form>
    </div>
  `,
})
export class ReportPage {
  private readonly api = inject(ClaimsApi);
  private readonly router = inject(Router);
  private readonly tour = inject(TourService);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);
  private readonly fb = inject(NonNullableFormBuilder);
  /** One Idempotency-Key per submission: a retry of the same form reuses it, an edited form gets a new one. */
  private readonly submission = new SubmissionKey();

  protected readonly marketOptions: SelectOption[] = MARKETS.map((m) => ({
    value: m.code,
    label: m.name,
    hint: m.currency,
  }));
  protected readonly lineOptions: SelectOption[] = [
    { value: 'Motor', label: 'Motor' },
    { value: 'Property', label: 'Property' },
  ];
  protected readonly today = localToday();
  protected readonly formErrors = signal<string[]>([]);

  protected readonly form = this.fb.group({
    market: ['', Validators.required],
    line: ['', Validators.required],
    policyNumber: ['', [Validators.required, Validators.maxLength(64)]],
    incidentDate: ['', Validators.required],
    incidentDescription: ['', [Validators.required, Validators.maxLength(4000)]],
    incidentLocation: ['', Validators.maxLength(300)],
  });

  private readonly marketValue = toSignal(this.form.controls.market.valueChanges, { initialValue: '' });
  protected readonly currency = computed(() => currencyOf(this.marketValue()));
  private readonly lineValue = toSignal(this.form.controls.line.valueChanges, { initialValue: '' });
  protected readonly exampleRows = computed(() =>
    incidentExamplesFor(this.marketValue(), this.lineValue()).map((e) => ({
      id: e.id,
      title: e.title,
      tag: `${e.market} · ${e.line}`,
      detail: `${e.daysAgo === 1 ? '1 day' : `${e.daysAgo} days`} ago · ${e.location}`,
    })),
  );

  protected readonly submitMutation = injectMutation(() => ({
    mutationFn: async (body: SubmitClaimRequest) => {
      const claim = await this.api.submit(body, this.submission.forBody(body));
      this.submission.reset();
      return claim;
    },
    meta: { handles: [400] },
    onSuccess: (claim) => {
      this.toasts.ok(`Claim ${claim.number} submitted`, undefined, this.correlation.id());
      void this.queryClient.invalidateQueries({ queryKey: claimKeys.all });
      void this.router
        .navigate(['/claims', claim.id])
        .then(() =>
          this.tour.hint(
            '[data-tour="search"]',
            'psst 👀',
            `Try finding it: type ${claim.number} in the search bar (or press /).`,
          ),
        );
    },
    onError: (e) => {
      if (isApiProblem(e) && e.isValidation) this.formErrors.set(applyServerErrors(this.form, e));
    },
  }));

  protected err(name: Field): string | null {
    return controlError(this.form.controls[name], {
      required: name === 'incidentDescription' ? 'Tell us what happened.' : 'This is required.',
      maxlength: 'That is longer than we can accept.',
    });
  }

  /** Fills every field from one made-up incident; validation and submit are unchanged. */
  protected fillExample(id: string): void {
    const e = INCIDENT_EXAMPLES.find((x) => x.id === id);
    if (!e) return;
    this.formErrors.set([]);
    this.form.setValue({
      market: e.market,
      line: e.line,
      policyNumber: samplePolicy(e),
      incidentDate: daysAgo(e.daysAgo),
      incidentDescription: e.description,
      incidentLocation: e.location,
    });
    this.form.markAsDirty();
  }

  protected submit(): void {
    this.formErrors.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      market: v.market,
      line: v.line,
      policyNumber: v.policyNumber.trim(),
      incidentDate: v.incidentDate,
      incidentDescription: v.incidentDescription.trim(),
      incidentLocation: v.incidentLocation.trim() || null,
    } as SubmitClaimRequest;
    this.submitMutation.mutate(body);
  }
}
