import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { injectMutation, injectQuery, QueryClient } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { ClaimDto, ClaimOutcome } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import { controlError } from '../../core/forms/server-errors';
import { CorrelationService } from '../../core/http/correlation.service';
import { ApiProblem, isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { Overlays } from '../../ui/overlays';
import { RefChip } from '../../ui/ref-chip';
import { ErrorState } from '../../ui/states';
import { ClaimStatusPill, MoneyPipe } from './claim-bits';
import { ClaimNextStep } from './claim-next-step';
import { ClaimRecordBanner } from './claim-record-banner';
import { ClaimActionDialog, ClaimDialogData, ClaimDialogMode, ClaimDialogResult } from './claim-action-dialog';
import { ActionState, Actor, ClaimAction, marketName } from './claims-logic';

/** A command the page can run; Reload-and-retry is the user's choice after a 412, never an automatic replay. */
type Command =
  | { kind: 'pickup' }
  | { kind: 'reserve'; amountMinor: number }
  | { kind: 'requestInfo'; question: string }
  | { kind: 'decide'; outcome: 'Approve' | 'Reject'; reason: string; amountMinor?: number }
  | { kind: 'settle' };

const DONE: Record<Command['kind'], string> = {
  pickup: 'Claim picked up',
  reserve: 'Reserve set',
  requestInfo: 'Question sent to the claimant',
  decide: 'Decision recorded',
  settle: 'Claim settled',
};

/** One claim, for both audiences: the claimant's view (status in plain words, timeline, answer box) and the staff view (actions, internal facts). */
@Component({
  selector: 'fg-claim-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ClaimStatusPill,
    ClaimRecordBanner,
    ClaimNextStep,
    MoneyPipe,
    RelativeTimePipe,
    StampPipe,
    RefChip,
    ErrorState,
  ],
  styleUrl: './claims.css',
  template: `
    <div class="page">
      <p class="m-0">
        <a [routerLink]="isStaff() ? '/claims/queue' : '/claims/mine'" class="text-muted"
          >&larr; {{ isStaff() ? 'Claims queue' : 'My claims' }}</a
        >
      </p>

      @if (claimQuery.isPending()) {
        <div class="panel panel-pad"><span class="skeleton" style="width: 40%; height: 2rem"></span></div>
      } @else if (claimQuery.isError() && !claim()) {
        <div class="panel"><fg-error-state [error]="claimQuery.error()" (retry)="claimQuery.refetch()" /></div>
      } @else if (claim(); as c) {
        <fg-claim-record-banner [claim]="c" [actor]="actor()" />

        @if (conflict(); as k) {
          <div class="banner conflict" role="alert" data-testid="conflict-banner">
            <span>
              <span class="banner-title">Changed since you opened it.</span>
              This claim was updated by someone else, so your last action was not applied. Reload to see its current
              state, then try again.
            </span>
            @if (k.ref) {
              <span><fg-ref-chip label="ref" [value]="k.ref" /></span>
            }
            <div><button type="button" class="btn btn-sm" (click)="reload()">Reload the claim</button></div>
          </div>
        }

        <div class="detail">
          <div class="stack">
            <fg-claim-next-step
              [claim]="c"
              [actor]="actor()"
              [busy]="command.isPending()"
              (act)="onNextAction($event)"
            />

            <!-- assessment worksheet slot -->

            @if (c.decisionReason || (c.approvedAmountMinor !== null && c.approvedAmountMinor !== undefined)) {
              <section class="panel panel-pad" aria-labelledby="dec-h">
                <h2 id="dec-h" class="section-title">Decision</h2>
                @if (c.decisionReason) {
                  <p class="mb-0"><span class="label">Reason</span><br />{{ c.decisionReason }}</p>
                }
                @if (c.approvedAmountMinor !== null && c.approvedAmountMinor !== undefined) {
                  <p class="mt-3 mb-0">
                    <span class="label">Approved amount</span><br />
                    <strong class="mono">{{ c.approvedAmountMinor | money: c.currency }}</strong>
                  </p>
                }
              </section>
            }

            @if (c.openInfoRequest && isStaff()) {
              <section class="panel panel-pad" aria-labelledby="oq-h">
                <h2 id="oq-h" class="section-title">Waiting on the claimant</h2>
                <p class="question">{{ c.openInfoRequest.question }}</p>
                <span class="text-muted" [title]="c.openInfoRequest.requestedAt | stamp"
                  >asked {{ c.openInfoRequest.requestedAt | relative }}</span
                >
              </section>
            }

            @if (c.openInfoRequest && canRespond()) {
              <section class="panel panel-pad" aria-labelledby="q-h" data-testid="answer-panel">
                <h2 id="q-h" class="section-title">A question for you</h2>
                <p class="question">{{ c.openInfoRequest.question }}</p>
                <form [formGroup]="answerForm" (ngSubmit)="answer()" novalidate class="flex flex-col gap-3">
                  <div class="field">
                    <label class="label" for="a-text">Your answer</label>
                    <textarea
                      id="a-text"
                      class="input"
                      rows="4"
                      formControlName="answer"
                      [attr.aria-invalid]="!!answerErr()"
                    ></textarea>
                    @if (answerErr(); as m) {
                      <span class="field-error">{{ m }}</span>
                    }
                  </div>
                  <div class="actions">
                    <button type="submit" class="btn btn-primary" [disabled]="respond.isPending()">
                      {{ respond.isPending() ? 'Sending…' : 'Send answer' }}
                    </button>
                  </div>
                </form>
              </section>
            }

            <section class="panel panel-pad" aria-labelledby="t-h">
              <h2 id="t-h" class="section-title">Activity</h2>
              @if (timelineQuery.isPending()) {
                <span class="skeleton" style="width: 60%"></span>
              } @else if (timelineQuery.isError()) {
                <fg-error-state [error]="timelineQuery.error()" (retry)="timelineQuery.refetch()" />
              } @else {
                <ol class="timeline" data-testid="timeline">
                  @for (t of activity(); track t.id) {
                    <li>
                      <fg-claim-status [status]="t.toStatus" />
                      <p class="note">{{ t.note }}</p>
                      <span class="when" [title]="t.occurredAt | stamp"
                        >{{ t.actorDisplay }} · {{ t.occurredAt | relative }}</span
                      >
                    </li>
                  }
                </ol>
              }
            </section>
          </div>

          <aside class="stack" aria-label="Claim details">
            <section class="panel panel-pad">
              <h2 class="section-title">The incident</h2>
              <dl class="facts">
                <div>
                  <dt>Incident date</dt>
                  <dd>{{ c.incidentDate }}</dd>
                </div>
                @if (c.incidentLocation) {
                  <div>
                    <dt>Where</dt>
                    <dd>{{ c.incidentLocation }}</dd>
                  </div>
                }
                <div>
                  <dt>What happened</dt>
                  <dd>{{ c.incidentDescription }}</dd>
                </div>
                <div>
                  <dt>Submitted</dt>
                  <dd [title]="c.submittedAt | stamp">{{ c.submittedAt | relative }}</dd>
                </div>
                <div>
                  <dt>Currency</dt>
                  <dd class="mono">{{ c.currency }}</dd>
                </div>
              </dl>
            </section>
            @if (isStaff()) {
              <section class="panel panel-pad" aria-labelledby="h-h">
                <h2 id="h-h" class="section-title">Handling</h2>
                <dl class="facts" data-testid="handling">
                  <div>
                    <dt>Claimant</dt>
                    <dd>{{ c.claimantName || 'Unknown' }}</dd>
                  </div>
                  <div>
                    <dt>Officer</dt>
                    <dd>{{ c.assignedOfficerName || 'Unassigned' }}</dd>
                  </div>
                  <div>
                    <dt>Reserve (internal)</dt>
                    <dd class="mono">{{ c.reserveMinor | money: c.currency }}</dd>
                  </div>
                  @if (c.approvedAmountMinor !== null && c.approvedAmountMinor !== undefined) {
                    <div>
                      <dt>Approved</dt>
                      <dd class="mono">{{ c.approvedAmountMinor | money: c.currency }}</dd>
                    </div>
                  }
                  <div>
                    <dt>Version</dt>
                    <dd class="mono">{{ c.version }}</dd>
                  </div>
                </dl>
                @if (access.has('AUDIT_READ')) {
                  <p class="mt-3 mb-0">
                    <a [routerLink]="['/audit']" [queryParams]="{ entityId: c.id }">Audit history of this claim</a>
                  </p>
                }
              </section>
            }
          </aside>
        </div>
      }
    </div>
  `,
})
export class ClaimDetailPage {
  /** Route param `:id`, bound by withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly api = inject(ClaimsApi);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly overlays = inject(Overlays);
  protected readonly access = inject(AccessService);

  protected readonly conflict = signal<{ ref: string | null } | null>(null);
  protected readonly market = marketName;

  /** Opening the claim GETs it, which records its ETag: the version every command is checked against. */
  protected readonly claimQuery = injectQuery(() => ({
    queryKey: claimKeys.one(this.id()),
    queryFn: () => this.api.get(this.id()),
    staleTime: 0,
    refetchOnWindowFocus: false,
  }));
  protected readonly claim = computed(() => this.claimQuery.data());

  protected readonly timelineQuery = injectQuery(() => ({
    queryKey: claimKeys.timeline(this.id()),
    queryFn: () => this.api.timeline(this.id()),
    // Always re-read on open: the claim moves on elsewhere (an officer acts), and a cached timeline would lag the status.
    staleTime: 0,
  }));

  /** Staff work claims; a claimant (no CLAIMS_ALL) sees their own. */
  protected readonly isStaff = computed(() => this.access.has('CLAIMS_ALL'));

  protected readonly actor = computed<Actor>(() => ({
    userId: this.access.me()?.userId,
    permissions: this.access.permissions(),
  }));

  /** The activity list, newest first. */
  protected readonly activity = computed(() =>
    [...(this.timelineQuery.data() ?? [])].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt)),
  );

  protected readonly canRespond = computed(() => this.access.has('CLAIMS_RESPOND') && !this.access.has('CLAIMS_ALL'));

  protected readonly answerForm = this.fb.group({ answer: ['', [Validators.required, Validators.maxLength(4000)]] });

  protected readonly respond = injectMutation(() => ({
    mutationFn: (v: { claim: ClaimDto; answer: string }) =>
      this.api.respond(v.claim.id, v.claim.openInfoRequest!.id, v.answer),
    meta: { handles: [412] },
    onSuccess: (c) => {
      this.answerForm.reset();
      this.afterCommand(c, 'Answer sent');
    },
    onError: (e) => this.onCommandError(e),
  }));

  protected readonly command = injectMutation(() => ({
    mutationFn: (op: Command) => {
      const id = this.id();
      switch (op.kind) {
        case 'pickup':
          return this.api.pickup(id);
        case 'reserve':
          return this.api.setReserve(id, op.amountMinor);
        case 'requestInfo':
          return this.api.requestInfo(id, op.question);
        case 'decide':
          return this.api.decide(id, { outcome: op.outcome, reason: op.reason, amountMinor: op.amountMinor });
        case 'settle':
          return this.api.settle(id);
      }
    },
    // 409 (a guard: not your claim, invalid transition) shows the API's own message as a toast; 412 is the banner.
    meta: { handles: [412] },
    onSuccess: (c, op) => this.afterCommand(c, DONE[op.kind]),
    onError: (e) => this.onCommandError(e),
  }));

  constructor() {
    // A different claim (route param change) starts with no stale banner.
    effect(() => {
      this.id();
      untracked(() => this.conflict.set(null));
    });
  }

  /** The card's one primary button: the claimant's answer box takes focus; everything else opens its existing dialog. */
  protected onNextAction(kind: ClaimAction | 'answer'): void {
    if (kind === 'answer') {
      const box = document.getElementById('a-text');
      box?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      box?.focus({ preventScroll: true });
      return;
    }
    void this.run({ action: kind, enabled: true });
  }

  /** Dialog-backed actions collect their input first; pickup runs at once; settle asks for a confirmation. */
  protected async run(a: ActionState): Promise<void> {
    const c = this.claim();
    if (!c) return;
    this.correlation.begin();
    if (a.action === 'pickup') return this.command.mutate({ kind: 'pickup' });
    if (a.action === 'settle') {
      const ok = await this.overlays.confirm({
        title: `Settle ${c.number}?`,
        body: 'This records the payment as made and closes the claim. It cannot be undone.',
        confirmLabel: 'Settle claim',
        tone: 'primary',
      });
      if (ok) this.command.mutate({ kind: 'settle' });
      return;
    }
    const result = await this.ask(a.action as ClaimDialogMode, c);
    if (!result) return;
    switch (a.action) {
      case 'reserve':
        return this.command.mutate({ kind: 'reserve', amountMinor: result.amountMinor! });
      case 'requestInfo':
        return this.command.mutate({ kind: 'requestInfo', question: result.text! });
      case 'approve':
        return this.command.mutate({
          kind: 'decide',
          outcome: ClaimOutcome.Approve,
          reason: result.text!,
          amountMinor: result.amountMinor,
        });
      default:
        return this.command.mutate({ kind: 'decide', outcome: ClaimOutcome.Reject, reason: result.text! });
    }
  }

  private ask(mode: ClaimDialogMode, claim: ClaimDto): Promise<ClaimDialogResult | undefined> {
    const ref = this.overlays.modal<ClaimActionDialog, ClaimDialogData, ClaimDialogResult | undefined>(
      ClaimActionDialog,
      { mode, claim },
    );
    return new Promise((resolve) => ref.closed.subscribe((v) => resolve(v)));
  }

  protected answerErr(): string | null {
    return controlError(this.answerForm.controls.answer, {
      required: 'Write your answer first.',
      maxlength: 'That is longer than we can accept.',
    });
  }

  protected answer(): void {
    const c = this.claim();
    if (!c) return;
    if (this.answerForm.invalid) {
      this.answerForm.markAllAsTouched();
      return;
    }
    this.respond.mutate({ claim: c, answer: this.answerForm.getRawValue().answer.trim() });
  }

  protected async reload(): Promise<void> {
    this.conflict.set(null);
    await Promise.all([this.claimQuery.refetch(), this.timelineQuery.refetch()]);
  }

  /** The command's response IS the claim as it now stands (and its new ETag): show it now, then refresh everything that lists it. */
  protected afterCommand(c: ClaimDto, message: string): void {
    this.conflict.set(null);
    this.queryClient.setQueryData(claimKeys.one(c.id), c);
    this.toasts.ok(message, undefined, this.correlation.id());
    void this.queryClient.invalidateQueries({ queryKey: claimKeys.all });
  }

  protected onCommandError(e: unknown): void {
    if (isApiProblem(e) && e.status === 412) this.conflict.set({ ref: (e as ApiProblem).correlationId });
  }
}
