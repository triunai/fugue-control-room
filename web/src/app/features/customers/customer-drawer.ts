import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { injectMutation, injectQuery, QueryClient } from '@tanstack/angular-query';
import { CustomersApi, customerKeys } from '../../api/customers.api';
import { CustomerDto, UpdateCustomerRequest } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { applyServerErrors, controlError } from '../../core/forms/server-errors';
import { CorrelationService } from '../../core/http/correlation.service';
import { EtagStore } from '../../core/http/etag.store';
import { SubmissionKey } from '../../core/http/idempotency';
import { ApiProblem, isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { RelativeTimePipe } from '../../ui/format';
import { Overlays } from '../../ui/overlays';
import { RefChip } from '../../ui/ref-chip';
import { ErrorState } from '../../ui/states';

export interface CustomerDrawerData {
  id: string | null;
  name?: string;
}

/** What changed between the version you opened and the one on the server, for the 412 banner. */
/** The write that was refused with 412; Overwrite replays exactly this one, never a different operation. */
type PendingOp =
  | { kind: 'save'; body: UpdateCustomerRequest }
  | { kind: 'transition'; to: 'Active' | 'Suspended' }
  | { kind: 'remove' };

interface Conflict {
  op: PendingOp;
  yours: number | null;
  theirs: CustomerDto | null;
  changed: { field: string; value: string }[];
  ref: string | null;
}

const FIELD_LABELS: Record<string, string> = { name: 'Name', email: 'Email', phone: 'Phone', status: 'Status' };

@Component({
  selector: 'fg-customer-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RefChip, RelativeTimePipe, ErrorState, HasPermissionDirective],
  templateUrl: './customer-drawer.html',
  styleUrl: './customer-drawer.css',
})
export class CustomerDrawer {
  protected readonly data = inject<CustomerDrawerData>(DIALOG_DATA);
  private readonly ref = inject(DialogRef);
  private readonly api = inject(CustomersApi);
  private readonly etags = inject(EtagStore);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);
  private readonly overlays = inject(Overlays);
  private readonly correlation = inject(CorrelationService);
  protected readonly access = inject(AccessService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly isNew = this.data.id === null;
  private suppressFill = false;
  /** One Idempotency-Key per create submission: a retry of the same form reuses it, an edited form gets a new one. */
  private readonly submission = new SubmissionKey();
  protected readonly conflict = signal<Conflict | null>(null);
  protected readonly formErrors = signal<string[]>([]);
  protected readonly canEdit = computed(() => this.access.has(this.isNew ? 'CUSTOMERS_INSERT' : 'CUSTOMERS_EDIT'));
  /** Self-service users cannot change an email (the API answers 403 customer.email_change_forbidden). */
  protected readonly emailLocked = computed(() => !this.isNew && !this.access.has('CUSTOMERS_ALL'));

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    phone: [''],
  });

  /** Opening the drawer GETs the customer, which records its ETag: the version every save is checked against. */
  protected readonly customer = injectQuery(() => ({
    queryKey: customerKeys.one(this.data.id ?? 'new'),
    queryFn: () => this.api.get(this.data.id!),
    enabled: !this.isNew,
    staleTime: 0,
    refetchOnWindowFocus: false,
  }));

  protected readonly version = computed(() => {
    this.customer.dataUpdatedAt();
    return this.data.id ? EtagStore.version(this.etags.get(this.api.url(this.data.id))) : null;
  });

  protected readonly save = injectMutation(() => ({
    mutationFn: async (body: UpdateCustomerRequest) => {
      if (this.isNew) {
        const created = await this.api.create(body, this.submission.forBody(body));
        this.submission.reset();
        return created;
      }
      await this.api.update(this.data.id!, body);
      return null;
    },
    meta: { handles: [400, 409, 412] },
    onSuccess: (created) => {
      this.toasts.ok(created ? `${created.name} created` : 'Changes saved', undefined, this.correlation.id());
      void this.queryClient.invalidateQueries({ queryKey: customerKeys.all });
      this.ref.close(true);
    },
    onError: (e, body) => this.handleError(e, { kind: 'save', body }),
  }));

  protected readonly transition = injectMutation(() => ({
    mutationFn: (to: 'Active' | 'Suspended') => this.api.transition(this.data.id!, to),
    meta: { handles: [409, 412] },
    onSuccess: (_d, to) => {
      this.toasts.ok(
        to === 'Suspended' ? 'Customer suspended' : 'Customer reactivated',
        undefined,
        this.correlation.id(),
      );
      void this.queryClient.invalidateQueries({ queryKey: customerKeys.all });
    },
    onError: (e, to) => this.handleError(e, { kind: 'transition', to }),
  }));

  protected readonly remove = injectMutation(() => ({
    mutationFn: () => this.api.remove(this.data.id!),
    meta: { handles: [412] },
    onSuccess: () => {
      this.toasts.ok('Customer deleted', undefined, this.correlation.id());
      void this.queryClient.invalidateQueries({ queryKey: customerKeys.all });
      this.ref.close(true);
    },
    onError: (e) => this.handleError(e, { kind: 'remove' }),
  }));

  constructor() {
    effect(() => {
      const c = this.customer.data();
      if (c && !this.suppressFill) untracked(() => this.fill(c));
    });
    effect(() => {
      if (!this.canEdit()) untracked(() => this.form.disable());
    });
    effect(() => {
      const locked = this.emailLocked();
      untracked(() =>
        locked ? this.form.controls.email.disable() : this.canEdit() && this.form.controls.email.enable(),
      );
    });
  }

  protected err(name: 'name' | 'email' | 'phone'): string | null {
    return controlError(this.form.controls[name], {
      required: name === 'name' ? 'Enter a name.' : 'Enter an email address.',
      email: 'Enter an email like name@example.com.',
      maxlength: 'That is longer than the API allows.',
    });
  }

  protected submit(): void {
    this.formErrors.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.save.mutate(this.body());
  }

  /** Reload: take the server's version (and its ETag) and drop your edits. */
  protected async reload(): Promise<void> {
    this.conflict.set(null);
    await this.customer.refetch();
    const c = this.customer.data();
    if (c) this.fill(c);
  }

  /**
   * Overwrite: fetch the CURRENT ETag, then replay the refused operation against it (a deliberate last-writer-wins).
   * A save resubmits your form values; a refused delete or suspend is retried as that same delete or suspend.
   */
  protected async overwrite(): Promise<void> {
    const op = this.conflict()?.op;
    if (!op) return;
    const body = op.kind === 'save' ? this.body() : null;
    this.correlation.begin();
    this.conflict.set(null);
    this.suppressFill = true;
    try {
      const result = await this.customer.refetch();
      if (result.isError) {
        // Without a fresh ETag the replay would be refused again (or hit a deleted row): say so, change nothing.
        if (isApiProblem(result.error)) this.toasts.problem(result.error);
        return;
      }
    } finally {
      this.suppressFill = false;
    }
    if (op.kind === 'save') this.save.mutate(body!);
    else if (op.kind === 'transition') this.transition.mutate(op.to);
    else this.remove.mutate();
  }

  protected async confirmDelete(): Promise<void> {
    const ok = await this.overlays.confirm({
      title: `Delete ${this.form.controls.name.value || 'this customer'}?`,
      body: 'The customer disappears from every list. The audit log keeps the record of the change.',
      confirmLabel: 'Delete customer',
    });
    if (ok) this.remove.mutate();
  }

  protected close(): void {
    this.ref.close(false);
  }

  private body(): UpdateCustomerRequest {
    const v = this.form.getRawValue();
    return { name: v.name.trim(), email: v.email.trim(), phone: v.phone.trim() || null };
  }

  private fill(c: CustomerDto): void {
    this.form.reset({ name: c.name, email: c.email, phone: c.phone ?? '' });
    this.formErrors.set([]);
  }

  private async handleError(e: unknown, op: PendingOp): Promise<void> {
    if (!isApiProblem(e)) return;
    if (e.status === 412) return this.showConflict(e, op);
    if (e.isValidation) {
      this.formErrors.set(applyServerErrors(this.form, e));
      return;
    }
    if (e.status === 409 && e.code?.includes('email')) {
      this.form.controls.email.setErrors({ server: 'Another of these customers already uses this email.' });
      this.form.controls.email.markAsTouched();
      return;
    }
    if (e.status === 409) {
      this.toasts.show({
        tone: 'warn',
        status: 409,
        title: 'Status already changed',
        body: 'Someone else changed this customer first. Reload to see the current state.',
        code: e.code,
        ref: e.correlationId,
      });
      await this.customer.refetch();
      return;
    }
    this.toasts.problem(e);
  }

  private async showConflict(e: ApiProblem, op: PendingOp): Promise<void> {
    const yours = this.version();
    const conflict: Conflict = { op, yours, theirs: null, changed: [], ref: e.correlationId };
    this.conflict.set(conflict);
    try {
      // Peek without replacing the tracked ETag: until the user chooses, a save must still be refused.
      const theirs = await this.api.peek(this.data.id!);
      const mine = this.customer.data();
      const changed = (['name', 'email', 'phone', 'status'] as const)
        .filter((k) => mine && (mine[k] ?? '') !== (theirs[k] ?? ''))
        .map((k) => ({ field: FIELD_LABELS[k], value: String(theirs[k] ?? 'empty') }));
      this.conflict.set({ ...conflict, theirs, changed });
    } catch {
      /* deleted meanwhile: the banner still offers Reload, which will show Not found */
    }
  }
}
