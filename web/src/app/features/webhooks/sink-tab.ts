import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { injectInfiniteQuery, injectMutation, injectQuery, QueryClient } from '@tanstack/angular-query';
import { ReceivedWebhookDto } from '../../api/models';
import { webhookKeys, WebhooksApi } from '../../api/webhooks.api';
import { RouterLink } from '@angular/router';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { CorrelationService } from '../../core/http/correlation.service';
import { SubmissionKey } from '../../core/http/idempotency';
import { ToastStore } from '../../core/ui/toast.store';
import { RelativeTimePipe, StampPipe } from '../../ui/format';
import { RefChip } from '../../ui/ref-chip';
import { ErrorState } from '../../ui/states';

interface SinkEvent extends ReceivedWebhookDto {
  fresh: boolean;
  data: string;
  subject: string | null;
}

/**
 * The Development sink, live: the API sends signed events to its own receiver, which verifies the HMAC, rejects stale
 * timestamps and dedupes by event id; this tab polls what it accepted. Every event shown here passed verification.
 */
@Component({
  selector: 'fg-sink-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, HasPermissionDirective, RefChip, RelativeTimePipe, StampPipe, ErrorState],
  templateUrl: './sink-tab.html',
  styleUrl: './sink-tab.css',
})
export class SinkTab {
  private readonly api = inject(WebhooksApi);
  private readonly toasts = inject(ToastStore);
  private readonly queryClient = inject(QueryClient);
  private readonly correlation = inject(CorrelationService);

  protected readonly target = this.api.sinkTarget;
  private readonly seen = new Set<string>();
  private firstLoad = true;
  /** Event ids that arrived since the previous poll: highlighted for a moment, the tab's one motion. */
  private readonly freshIds = signal<ReadonlySet<string>>(new Set());
  /** The sink subscription create is a submission: the body and the Idempotency-Key are fixed until one succeeds, so a retry after a lost response replays the first result. */
  private readonly connectKey = new SubmissionKey();

  protected readonly feed = injectQuery(() => ({
    queryKey: webhookKeys.sink,
    queryFn: () => this.api.sink(),
    refetchInterval: 1500,
    refetchIntervalInBackground: false,
    staleTime: 0,
  }));

  /** The subscription that points at the sink, if any (its secret cannot be read back: reconnecting rotates it). */
  private readonly subs = injectInfiniteQuery(() => ({
    queryKey: webhookKeys.subscriptions,
    queryFn: ({ pageParam }) => this.api.subscriptions(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
  }));
  protected readonly sinkSub = computed(() =>
    this.subs
      .data()
      ?.pages.flatMap((p) => p.items)
      .find((s) => s.url === this.target),
  );

  protected readonly events = computed<SinkEvent[]>(() => {
    const fresh = this.freshIds();
    return (this.feed.data() ?? []).map((e) => ({ ...e, fresh: fresh.has(e.eventId), ...parseBody(e.body) }));
  });

  constructor() {
    effect((onCleanup) => {
      const list = this.feed.data();
      if (!list) return;
      untracked(() => {
        const arrived = this.firstLoad ? [] : list.filter((e) => !this.seen.has(e.eventId)).map((e) => e.eventId);
        list.forEach((e) => this.seen.add(e.eventId));
        this.firstLoad = false;
        if (!arrived.length) return;
        this.freshIds.set(new Set(arrived));
        const t = setTimeout(() => this.freshIds.set(new Set()), 2600);
        onCleanup(() => clearTimeout(t));
      });
    });
  }

  protected readonly connect = injectMutation(() => ({
    mutationFn: async () => {
      const existing = this.sinkSub();
      let result;
      if (existing) {
        result = await this.api.rotate(existing.id);
      } else {
        const body = { name: 'Development sink', url: this.target, eventTypes: ['claim.*'] };
        result = await this.api.create(body, this.connectKey.forBody(body));
        this.connectKey.reset();
      }
      await this.api.setSinkSecret(result.secret);
      return existing ? 'reconnected' : 'connected';
    },
    onSuccess: (how) =>
      this.toasts.ok(
        how === 'connected' ? 'Sink connected' : 'Sink reconnected with a new secret',
        'Claim events are now signed and sent to it. Move a claim to see one arrive.',
        this.correlation.id(),
      ),
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: ['webhooks'] }),
  }));

  protected readonly clear = injectMutation(() => ({
    mutationFn: () => this.api.clearSink(),
    onSuccess: () => {
      this.seen.clear();
      this.firstLoad = true;
    },
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: webhookKeys.sink }),
  }));
}

function parseBody(body: string): { data: string; subject: string | null } {
  try {
    const parsed = JSON.parse(body) as { data?: Record<string, unknown> };
    const data: Record<string, unknown> = parsed.data ?? (parsed as Record<string, unknown>);
    const subject = typeof data['name'] === 'string' ? (data['name'] as string) : null;
    return { data: JSON.stringify(parsed, null, 2).replace(/\*{3,}/g, '•••'), subject };
  } catch {
    return { data: body, subject: null };
  }
}
