import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { ApiProblem, isApiProblem } from '../../core/http/problem';
import { RefChip } from '../../ui/ref-chip';
import { BrandMark } from '../shell/brand-mark';

@Component({
  selector: 'fg-sign-in',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, BrandMark, RefChip],
  templateUrl: './sign-in.html',
  styleUrl: './sign-in.css',
})
export class SignIn {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly reason = input<string>();
  readonly returnUrl = input<string>();

  protected readonly demoAccounts = environment.demoAccounts;
  protected readonly busy = signal(false);
  protected readonly failure = signal<ApiProblem | null>(null);
  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required], // secrets-scan:ignore (form control, no value)
  });

  protected useDemo(email: string): void {
    this.form.setValue({ email, password: environment.demoPassword }); // secrets-scan:ignore (dev-only env value)
    this.failure.set(null);
    void this.submit();
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.failure.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.signIn(email, password);
      await this.router.navigateByUrl(this.returnUrl() || '/');
    } catch (err) {
      this.failure.set(isApiProblem(err) ? err : null);
      if (!isApiProblem(err)) throw err;
    } finally {
      this.busy.set(false);
    }
  }

  protected failureText(p: ApiProblem): string {
    if (p.status === 401) return 'That email and password do not match an active account.';
    if (p.status === 429) return `Too many attempts. Try again in ${p.retryAfterSeconds ?? 60} seconds.`;
    if (p.status === 0) return 'The API cannot be reached. Start it on port 5081, then try again.';
    return p.title;
  }
}
