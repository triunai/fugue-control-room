import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { injectMutation, injectQuery, QueryClient } from '@tanstack/angular-query';
import { AccessApi } from '../../api/access.api';
import type { CreateUserRequest, UserDto } from '../../api/generated/model';
import { RolesApi } from '../../api/roles.api';
import { applyServerErrors, controlError } from '../../core/forms/server-errors';
import { CorrelationService } from '../../core/http/correlation.service';
import { isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { samplePassword, userExamples } from '../../ui/examples';
import { ExamplesPanel } from '../../ui/examples-panel';
import { ErrorState } from '../../ui/states';
import {
  atLeastOneRole,
  PASSWORD_MAX,
  PASSWORD_MIN,
  placeProblem,
  toCreateUserRequest,
  toggleRole,
} from './user-create.logic';

/** "Add user" (USERS_MANAGE): email, name, an initial password and at least one role. Closes with the created user. */
@Component({
  selector: 'fg-user-create-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, ErrorState, ExamplesPanel],
  templateUrl: './user-create-drawer.html',
  styleUrl: '../customers/customer-drawer.css',
})
export class UserCreateDrawer {
  private readonly ref = inject<DialogRef<UserDto | false>>(DialogRef);
  private readonly api = inject(AccessApi);
  private readonly rolesApi = inject(RolesApi);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly passwordMin = PASSWORD_MIN;
  protected readonly formErrors = signal<string[]>([]);

  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    password: ['', [Validators.required, Validators.minLength(PASSWORD_MIN), Validators.maxLength(PASSWORD_MAX)]], // secrets-scan:ignore
    roles: this.fb.control<string[]>([], atLeastOneRole),
  });

  protected readonly roles = injectQuery(() => ({
    queryKey: ['access', 'roles'] as const,
    queryFn: () => this.rolesApi.list(),
  }));

  protected readonly save = injectMutation(() => ({
    mutationFn: (body: CreateUserRequest) => this.api.createUser(body),
    meta: { handles: [400, 403, 409] },
    onSuccess: (user: UserDto) => {
      this.toasts.ok(
        `${user.email} created`,
        'Hand the initial password over directly; it is not shown again.',
        this.correlation.id(),
      );
      void this.queryClient.invalidateQueries({ queryKey: ['access', 'users'] });
      this.ref.close(user);
    },
    onError: (e: unknown) => this.handleError(e),
  }));

  private readonly examples = userExamples();
  protected readonly exampleRows = this.examples.map((u) => ({
    id: u.id,
    title: u.displayName,
    tag: u.title,
    detail: `${u.email} · ${u.roles.join(', ')}`,
  }));
  /** The sample password the last example filled; its hint shows it while the field still holds it. */
  private readonly samplePw = signal<string | null>(null);

  protected sampleShown(): boolean {
    const pw = this.samplePw();
    return !!pw && this.form.controls.password.value === pw;
  }

  /** A made-up person with a random sample password and the example's roles (only those the API lists). */
  protected fillExample(id: string): void {
    const u = this.examples.find((x) => x.id === id);
    if (!u) return;
    const known = new Set((this.roles.data() ?? []).map((r) => r.code));
    const pw = samplePassword();
    this.formErrors.set([]);
    this.samplePw.set(pw);
    this.form.setValue({
      email: u.email,
      displayName: u.displayName,
      password: pw, // secrets-scan:ignore (random sample password generated in the browser)
      roles: u.roles.filter((r) => known.has(r)),
    });
    this.form.markAsDirty();
  }

  protected hasRole(code: string): boolean {
    return this.form.controls.roles.value.includes(code);
  }

  protected setRole(code: string, checked: boolean): void {
    const roles = this.form.controls.roles;
    roles.setValue(toggleRole(roles.value, code, checked));
    roles.markAsTouched();
  }

  protected err(name: 'email' | 'displayName' | 'password' | 'roles'): string | null {
    return controlError(this.form.controls[name], {
      required: {
        email: 'Enter an email address.',
        displayName: 'Enter a name.',
        password: 'Enter an initial password.', // secrets-scan:ignore
        roles: 'Pick at least one role.',
      }[name],
      email: 'Enter an email like name@example.com.',
      minlength: `At least ${PASSWORD_MIN} characters.`,
      maxlength: 'That is longer than the API allows.',
    });
  }

  protected submit(): void {
    this.formErrors.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.save.mutate(toCreateUserRequest(this.form.getRawValue()));
  }

  protected close(): void {
    this.ref.close(false);
  }

  private handleError(e: unknown): void {
    if (!isApiProblem(e)) return;
    if (e.isValidation) {
      this.formErrors.set(applyServerErrors(this.form, e));
      return;
    }
    const placed = placeProblem(e);
    if (placed) {
      const control = this.form.controls[placed.field];
      control.setErrors({ server: placed.message });
      control.markAsTouched();
      return;
    }
    this.toasts.problem(e);
  }
}
