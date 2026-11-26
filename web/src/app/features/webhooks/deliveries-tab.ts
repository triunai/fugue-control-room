import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { injectInfiniteQuery, injectMutation, keepPreviousData, QueryClient } from '@tanstack/angular-query';
import { WebhookDeliveryDto } from '../../api/models';
import { DeliveryStatusFilter, webhookKeys, WebhooksApi } from '../../api/webhooks.api';
import { CorrelationService } from '../../core/http/correlation.service';
import { ToastStore } from '../../core/ui/toast.store';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';

const STATUSES: { v: DeliveryStatusFilter; l: string }[] = [
  { v: '', l: 'All' },
  { v: 'pending', l: 'Pending' },
  { v: 'succeeded', l: 'Delivered' },
  { v: 'dead', l: 'Dead letter' },
];

@Component({
  selector: 'fg-deliveries-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RelativeTimePipe, StampPipe, SkeletonRows, EmptyState, ErrorState],
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <p class="m-0 text-muted">
        One row per event and subscription. Failed sends retry with backoff, then land in the dead letter.
      </p>
      <div class="segmented" role="radiogroup" aria-label="Delivery status">
        @for (s of statuses; track s.v) {
          <button type="button" role="radio" [attr.aria-checked]="status() === s.v" (click)="status.set(s.v)">
            {{ s.l }}
          </button>
        }
      </div>
    </div>
    <div class="table-wrap mt-4">
      @if (query.isPending()) {
        <fg-skeleton-rows [rows]="6" />
      } @else if (query.isError()) {
        <fg-error-state [error]="query.error()" (retry)="query.refetch()" />
      } @else if (!rows().length) {
        <fg-empty-state
          title="No deliveries here"
          body="Change a customer to raise an event; each active subscription that matches gets a delivery."
        />
      } @else {
        <table class="table">
          <thead>
            <tr>
              <th scope="col">Event</th>
              <th scope="col">Status</th>
              <th scope="col">Attempts</th>
              <th scope="col">Last answer</th>
              <th scope="col">Next retry</th>
              <th scope="col">Raised</th>
              <th scope="col"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (d of rows(); track d.id) {
              <tr>
                <td>
                  <span class="mono">{{ d.eventType }}</span>
                  @if (d.lastError) {
                    <span class="block max-w-[24rem] truncate text-sm text-muted" [title]="d.lastError">{{
                      d.lastError
                    }}</span>
                  }
                </td>
                <td>
                  <span class="pill" [class]="pill(d)">{{ label(d) }}</span>
                </td>
                <td class="mono">{{ d.attempts }}</td>
                <td class="mono">
                  @if (d.lastStatusCode) {
                    <span [class.text-err-ink]="d.lastStatusCode >= 400">{{ d.lastStatusCode }}</span>
                    @if (d.durationMs !== null && d.durationMs !== undefined) {
                      <span class="text-muted"> · {{ d.durationMs }} ms</span>
                    }
                  } @else {
                    <span class="text-muted">none yet</span>
                  }
                </td>
                <td class="text-muted" [title]="d.nextAttemptAt | stamp">
                  {{ d.status === 'pending' || d.status === 'in_flight' ? (d.nextAttemptAt | relative) : '' }}
                </td>
                <td class="text-muted" [title]="d.occurredAt | stamp">{{ d.occurredAt | relative }}</td>
                <td class="text-right">
                  @if (d.status === 'dead') {
                    <button
                      type="button"
                      class="btn btn-sm"
                      (click)="redeliver.mutate(d)"
                      [disabled]="redeliver.isPending()"
                    >
                      Redeliver
                    </button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
        @if (query.hasNextPage()) {
          <div class="p-3 text-right">
            <button type="button" class="btn" (click)="query.fetchNextPage()">Load more</button>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .segmented {
      display: inline-flex;
      padding: 3px;
      gap: 2px;
      border: 1.5px solid var(--control);
      border-radius: var(--radius-control);
      background: var(--surface2);
    }
    .segmented button {
      height: 2rem;
      padding: 0 0.8rem;
      border: 0;
      border-radius: 5px;
      background: none;
      color: var(--muted);
      font: 550 0.9rem var(--font-ui);
      cursor: pointer;
    }
    .segmented button[aria-checked='true'] {
      background: var(--accent);
      color: var(--on-accent);
    }
  `,
})
export class DeliveriesTab {
  private readonly api = inject(WebhooksApi);
  private readonly toasts = inject(ToastStore);
  private readonly queryClient = inject(QueryClient);
  private readonly correlation = inject(CorrelationService);

  protected readonly statuses = STATUSES;
  protected readonly status = signal<DeliveryStatusFilter>('');

  protected readonly query = injectInfiniteQuery(() => ({
    queryKey: webhookKeys.deliveries(this.status()),
    queryFn: ({ pageParam }) => this.api.deliveries(this.status(), pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    placeholderData: keepPreviousData,
    refetchInterval: 4000,
  }));
  protected readonly rows = computed(() => this.query.data()?.pages.flatMap((p) => p.items) ?? []);

  protected readonly redeliver = injectMutation(() => ({
    mutationFn: (d: WebhookDeliveryDto) => this.api.redeliver(d.id),
    onSuccess: (_r, d) =>
      this.toasts.ok(
        `${d.eventType} queued again`,
        'It goes back to pending with a fresh attempt budget.',
        this.correlation.id(),
      ),
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: ['webhooks', 'deliveries'] }),
  }));

  protected label(d: WebhookDeliveryDto): string {
    return (
      { pending: 'Pending', in_flight: 'Sending', succeeded: 'Delivered', dead: 'Dead letter' }[d.status] ?? d.status
    );
  }

  protected pill(d: WebhookDeliveryDto): string {
    return (
      { pending: 'pill-warn', in_flight: 'pill-acc', succeeded: 'pill-ok', dead: 'pill-err' }[d.status] ?? 'pill-muted'
    );
  }
}
