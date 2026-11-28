import { AbstractControl, ValidationErrors } from '@angular/forms';
import type { CreateUserRequest, UpdateUserRequest } from '../../api/generated/model';
import { ApiProblem } from '../../core/http/problem';

/** The API's initial-password policy (CreateUserValidator): 12 to 128 characters. */
export const PASSWORD_MIN = 12;
export const PASSWORD_MAX = 128;

export interface CreateUserForm {
  email: string;
  displayName: string;
  password: string; // secrets-scan:ignore (a type, not a value)
  roles: string[];
}

/** At least one role: a user with none can sign in but do nothing, and the API refuses it. */
export function atLeastOneRole(control: AbstractControl<string[]>): ValidationErrors | null {
  return control.value?.length ? null : { required: true };
}

/** Adds or removes one role code, keeping the list sorted and free of duplicates. */
export function toggleRole(roles: readonly string[], code: string, checked: boolean): string[] {
  const next = new Set(roles);
  if (checked) next.add(code);
  else next.delete(code);
  return [...next].sort();
}

/** The request body: trimmed email and name; the password is sent exactly as typed. */
export function toCreateUserRequest(v: CreateUserForm): CreateUserRequest {
  return { email: v.email.trim(), displayName: v.displayName.trim(), password: v.password, roles: [...v.roles] }; // secrets-scan:ignore
}

/** The edit body: trimmed name, the active flag and the full role set. Email and password are never part of an edit. */
export function toUpdateUserRequest(v: { displayName: string; isActive: boolean; roles: string[] }): UpdateUserRequest {
  return { displayName: v.displayName.trim(), isActive: v.isActive, roles: [...v.roles] };
}

/** A 412 on edit: someone saved this user after you opened it. */
export function isStaleEdit(problem: ApiProblem): boolean {
  return problem.status === 412;
}

/** Where a refused create or edit belongs in the form: a field and its message, or null for a toast. */
export function placeProblem(problem: ApiProblem): { field: 'email' | 'roles'; message: string } | null {
  if (problem.status === 409 && problem.code === 'user.email_taken') {
    return { field: 'email', message: 'A user with this email already exists.' };
  }
  if (problem.status === 403 && problem.code === 'user.role_escalation') {
    return { field: 'roles', message: problem.detail ?? 'You cannot assign a role that grants more than you hold.' };
  }
  if (problem.status === 409 && problem.code === 'user.self_lockout') {
    return {
      field: 'roles',
      message: 'You cannot deactivate yourself or remove your own last role that manages users.',
    };
  }
  return null;
}
