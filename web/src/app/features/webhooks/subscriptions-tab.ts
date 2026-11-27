import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { injectInfiniteQuery, injectMutation, QueryClient } from '@tanstack/angular-query';
import { WebhookSubscriptionDto } from '../../api/models';
import { webhookKeys, WebhooksApi } from '../../api/webhooks.api';
import { CONSOLE_CONFIG } from '../../core/config';
import { CorrelationService } from '../../core/http/correlation.service';
import { SubmissionKey } from '../../core/http/idempotency';
import { ToastStore } from '../../core/ui/toast.store';
import { RelativeTimePipe } from '../../ui/format';
import { Overlays } from '../../ui/overlays';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { SecretDialog, SecretDialogData } from './secret-dialog';
import { SubscriptionDialog, SubscriptionDialogData } from './subscription-dialog';

@Component({
  selector: 'fg-subscriptions-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RelativeTimePipe, SkeletonRows, EmptyState, ErrorState],
  template: `
    <div class="flex items-center justify-between gap-3">
      <p class="m-0 text-muted">Receivers the API signs and sends customer events to, at least once.</p>
      <button type="button" class="btn btn-primary" (click)="create()">New subscription</button>
    </div>
    <div class="table-wrap mt-4">
      @if (query.isPending()) {
        <fg-skeleton-rows [rows]="4" />
      } @else if (query.isError()) {
        <fg-error-state [error]="query.error()" (retry)="query.refetch()" />
      } @else if (!rows().length) {
        <fg-empty-state
          title="No subscriptions yet"
          body="Add a receiver address, or connect the Development sink to watch events arrive."
        >
          <button type="button" class="btn btn-primary" (click)="create()">New subscription</button>
        </fg-empty-state>
      } @else {
        <table class="table">
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col">Receiver</th>
              <th scope="col">Events</th>
              <th scope="col">State</th>
              <th scope="col">Updated</th>
              <th scope="col"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (s of rows(); track s.id) {
              <tr>
                <td class="font-semibold">{{ s.name }}</td>
                <td class="mono max-w-[22rem] truncate" [title]="s.url">{{ s.url }}</td>
                <td>
                  <div class="flex flex-wrap gap-1">
                    @for (t of s.eventTypes; track t) {
                      <span class="pill pill-acc pill-plain mono">{{ t }}</span>
                    }
                  </div>
                </td>
                <td>
                  <span class="pill" [class.pill-ok]="s.isActive" [class.pill-muted]="!s.isActive">{{
                    s.isActive ? 'Active' : 'Paused'
                  }}</span>
                </td>
                <td class="text-muted">{{ s.updatedAt | relative }}</td>
                <td class="whitespace-nowrap text-right">
                  <button type="button" class="btn btn-sm btn-quiet" (click)="setActive(s, !s.isActive)">
                    {{ s.isActive ? 'Pause' : 'Resume' }}
                  </button>
                  <button type="button" class="btn btn-sm btn-quiet" (click)="rotate(s)">Rotate secret</button>
                  <button type="button" class="btn btn-sm btn-quiet text-err-ink" (click)="remove(s)">Delete</button>
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
})
export class SubscriptionsTab {
  private readonly api = inject(WebhooksApi);
  private readonly overlays = inject(Overlays);
  private readonly toasts = inject(ToastStore);
  private readonly queryClient = inject(QueryClient);
  private readonly correlation = inject(CorrelationService);
  private readonly development = inject(CONSOLE_CONFIG).development;

  protected readonly query = injectInfiniteQuery(() => ({
    queryKey: webhookKeys.subscriptions,
    queryFn: ({ pageParam }) => this.api.subscriptions(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
  }));
  protected readonly rows = computed(() => this.query.data()?.pages.flatMap((p) => p.items) ?? []);

  private readonly update = injectMutation(() => ({
    mutationFn: (v: { s: WebhookSubscriptionDto; isActive: boolean }) =>
      this.api.update(v.s.id, { name: v.s.name, url: v.s.url, eventTypes: v.s.eventTypes, isActive: v.isActive }),
    onSuccess: (_d, v) =>
      this.toasts.ok(`${v.s.name} ${v.isActive ? 'resumed' : 'paused'}`, undefined, this.correlation.id()),
    onSettled: () => this.refresh(),
  }));

  private readonly rotateSecret = injectMutation(() => ({
    mutationFn: (s: WebhookSubscriptionDto) => this.api.rotate(s.id),
    onSuccess: (r) => this.reveal({ name: r.name, secret: r.secret, rotated: true }),
    onSettled: () => this.refresh(),
  }));

  private readonly del = injectMutation(() => ({
    mutationFn: (s: WebhookSubscriptionDto) => this.api.remove(s.id),
    onSuccess: (_d, s) => this.toasts.ok(`${s.name} deleted`, undefined, this.correlation.id()),
    onSettled: () => this.refresh(),
  }));

  protected create(): void {
    const submission = new SubmissionKey(); // one Idempotency-Key per dialog: a retried submit returns the first secret instead of creating again
    this.overlays.modal<SubscriptionDialog, SubscriptionDialogData, boolean>(SubscriptionDialog, {
      development: this.development,
      submit: async (body) => {
        const created = await this.api.create(body, submission.forBody(body));
        submission.reset();
        void this.refresh();
        this.reveal({ name: created.name, secret: created.secret, rotated: false });
      },
    });
  }

  protected setActive(s: WebhookSubscriptionDto, isActive: boolean): void {
    this.update.mutate({ s, isActive });
  }

  protected async rotate(s: WebhookSubscriptionDto): Promise<void> {
    const ok = await this.overlays.confirm({
      title: `Rotate the secret for ${s.name}?`,
      body: 'The current secret stops working immediately. Deliveries fail verification until the receiver has the new one.',
      confirmLabel: 'Rotate secret',
    });
    if (ok) this.rotateSecret.mutate(s);
  }

  protected async remove(s: WebhookSubscriptionDto): Promise<void> {
    const ok = await this.overlays.confirm({
      title: `Delete ${s.name}?`,
      body: 'No more events are sent to this receiver. Deliveries already recorded stay in the history.',
      confirmLabel: 'Delete subscription',
    });
    if (ok) this.del.mutate(s);
  }

  private reveal(data: SecretDialogData): void {
    this.overlays.modal(SecretDialog, data, { disableClose: true });
  }

  private refresh(): Promise<void> {
    return this.queryClient.invalidateQueries({ queryKey: ['webhooks'] });
  }
}
