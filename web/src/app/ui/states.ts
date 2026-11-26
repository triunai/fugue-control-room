import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ApiProblem, isApiProblem } from '../core/http/problem';
import { RefChip } from './ref-chip';

/** Skeleton rows for a table body while the first page loads. */
@Component({
  selector: 'fg-skeleton-rows',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (r of rowsArr(); track $index) {
      <div class="flex items-center gap-6 border-b border-[var(--line-soft)] px-4 py-[0.95rem] last:border-0">
        @for (w of widths(); track $index) {
          <span class="skeleton" [style.width]="w" [style.opacity]="1 - $index * 0.08"></span>
        }
      </div>
    }
  `,
})
export class SkeletonRows {
  readonly rows = input(6);
  readonly widths = input<string[]>(['22%', '26%', '10%', '12%', '10%']);
  protected rowsArr = () => Array.from({ length: this.rows() });
}

/** Empty state: one sentence of what this is, one helpful action. */
@Component({
  selector: 'fg-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center gap-2 px-6 py-14 text-center">
      <svg width="44" height="30" viewBox="0 0 44 30" aria-hidden="true" class="mb-2 text-[var(--control)]">
        @for (y of [3, 9, 15, 21, 27]; track y) {
          <line x1="0" [attr.y1]="y" x2="44" [attr.y2]="y" stroke="currentColor" stroke-width="1" />
        }
        <ellipse cx="15" cy="21" rx="4" ry="3" fill="currentColor" transform="rotate(-20 15 21)" />
        <line x1="18.6" y1="20" x2="18.6" y2="5" stroke="currentColor" stroke-width="1.4" />
      </svg>
      <p class="section-title">{{ title() }}</p>
      @if (body()) {
        <p class="m-0 max-w-[46ch] text-muted">{{ body() }}</p>
      }
      <div class="mt-3"><ng-content /></div>
    </div>
  `,
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly body = input<string>('');
}

/** Error state for a failed load: what happened, the ref, and Retry. 404 and 403 read as calm, not alarming. */
@Component({
  selector: 'fg-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RefChip],
  template: `
    <div class="flex flex-col items-center gap-2 px-6 py-12 text-center" role="alert">
      <p class="section-title">{{ heading() }}</p>
      <p class="m-0 max-w-[52ch] text-muted">{{ message() }}</p>
      <div class="mt-2 flex flex-wrap items-center justify-center gap-2">
        @if (problem()?.code) {
          <span class="mono text-muted">{{ problem()!.code }}</span>
        }
        @if (problem()?.correlationId) {
          <fg-ref-chip label="ref" [value]="problem()!.correlationId!" />
        }
      </div>
      @if (canRetry()) {
        <button type="button" class="btn mt-3" (click)="retry.emit()">Try again</button>
      }
    </div>
  `,
})
export class ErrorState {
  readonly error = input<unknown>(null);
  readonly retry = output<void>();

  protected problem(): ApiProblem | null {
    const e = this.error();
    return isApiProblem(e) ? e : null;
  }

  protected heading(): string {
    const p = this.problem();
    if (!p) return 'Something went wrong';
    if (p.status === 404) return 'Not found';
    if (p.status === 403) return 'Not available to you';
    if (p.status === 0) return 'The API cannot be reached';
    return p.title;
  }

  protected message(): string {
    const p = this.problem();
    if (!p) return 'The page could not load its data.';
    if (p.status === 404) return 'It may have been deleted, switched off, or it is outside what you can see.';
    if (p.status === 403) return 'Your permissions do not include this. An administrator can grant it.';
    if (p.status === 0) return 'Start the API on port 5081 (runbook 09), then try again.';
    return p.detail ?? 'Quote the ref below if you report it.';
  }

  protected canRetry(): boolean {
    const s = this.problem()?.status;
    return s !== 404 && s !== 403;
  }
}
