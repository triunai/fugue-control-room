import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { AuditApi, auditKeys } from '../../api/audit.api';
import { isApiProblem } from '../../core/http/problem';
import { StampPipe } from '../../ui/format';
import { RefChip } from '../../ui/ref-chip';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { layoutScore } from './score-layout';

const STATUS_PILL: Record<string, string> = { complete: 'pill-ok', pending: 'pill-warn', failed: 'pill-err' };

/**
 * "The Score" (D-064): one correlation id as a timeline built from the stored rows (audit log, outbox, webhook deliveries).
 * The checklist under the figure is the text alternative and carries every value the figure shows.
 */
@Component({
  selector: 'fg-score-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RefChip, StampPipe, SkeletonRows, EmptyState, ErrorState],
  templateUrl: './score-page.html',
  styleUrl: './score-page.css',
})
export class ScorePage {
  private readonly api = inject(AuditApi);

  /** Route parameter: /audit/trace/:correlationId. */
  readonly correlationId = input.required<string>();

  protected readonly query = injectQuery(() => ({
    queryKey: auditKeys.trace(this.correlationId()),
    queryFn: () => this.api.trace(this.correlationId()),
    retry: false,
  }));
  protected readonly notFound = computed(() => {
    const e = this.query.error();
    return isApiProblem(e) && e.status === 404;
  });
  protected readonly layout = computed(() => {
    const t = this.query.data();
    return t ? layoutScore(t.voices) : null;
  });

  protected pill(status: string): string {
    return STATUS_PILL[status] ?? 'pill-muted';
  }

  protected ms(value: number): string {
    return `${Math.round(value).toLocaleString('en')} ms`;
  }
}
