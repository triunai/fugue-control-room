import { AbstractControl, FormGroup } from '@angular/forms';
import { ApiProblem } from '../http/problem';

/**
 * Puts a validation problem's `errors` onto the matching form controls (keys are camelCase, matching control names)
 * as `{ server: message }`. Returns the messages that matched no control, for a form-level summary.
 */
export function applyServerErrors(form: FormGroup, problem: ApiProblem): string[] {
  const unmatched: string[] = [];
  for (const [key, messages] of Object.entries(problem.errors)) {
    const control = form.get(key) ?? form.get(key.split('.')[0]);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}

/** The first message to show under a control: the server's wins over client rules. */
export function controlError(control: AbstractControl, messages: Record<string, string> = {}): string | null {
  if (!control.touched || !control.errors) return null;
  const errors = control.errors;
  if (errors['server']) return String(errors['server']);
  for (const key of Object.keys(errors)) if (messages[key]) return messages[key];
  return 'Check this field.';
}
