import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import { CORRELATION_ID, SEND_IF_MATCH } from '../core/http/context';
import { newCorrelationId } from '../core/http/correlation.service';
import { EtagStore } from '../core/http/etag.store';
import { idempotencyHeaders } from '../core/http/idempotency';
import { getPage, params } from './http-page';
import {
  CreateWebhookSubscriptionRequest,
  Page,
  ReceivedWebhookDto,
  UpdateWebhookSubscriptionRequest,
  WebhookDeliveryDto,
  WebhookSubscriptionDto,
  WebhookSubscriptionSecretDto,
} from './models';

export type DeliveryStatusFilter = '' | 'pending' | 'in_flight' | 'succeeded' | 'dead';

export const EVENT_TYPES = [
  'customer.created.v1',
  'customer.updated.v1',
  'customer.suspended.v1',
  'customer.reactivated.v1',
  'customer.deleted.v1',
] as const;

export const webhookKeys = {
  subscriptions: ['webhooks', 'subscriptions'] as const,
  deliveries: (status: DeliveryStatusFilter) => ['webhooks', 'deliveries', status] as const,
  sink: ['webhooks', 'sink'] as const,
};

@Injectable({ providedIn: 'root' })
export class WebhooksApi {
  private readonly http = inject(HttpClient);
  private readonly etags = inject(EtagStore);
  private readonly config = inject(CONSOLE_CONFIG);
  private readonly base = `${this.config.apiBase}/webhooks`;
  private readonly sinkUrl = `${this.config.apiBase}/dev/webhook-sink`;

  /** The address the API POSTs to when it delivers to its own Development sink. */
  readonly sinkTarget = `${this.config.apiOrigin}/api/v1/dev/webhook-sink`;

  subUrl(id: string): string {
    return `${this.base}/subscriptions/${id}`;
  }

  async subscriptions(cursor: string | null): Promise<Page<WebhookSubscriptionDto>> {
    const page = await getPage<WebhookSubscriptionDto>(
      this.http,
      `${this.base}/subscriptions`,
      params({ limit: 50, cursor }),
    );
    for (const s of page.items) this.etags.seed(this.subUrl(s.id), s.version);
    return page;
  }

  /** `idempotencyKey` makes a retry safe: the same key with the same body returns the first response, with the same one-time secret. */
  create(body: CreateWebhookSubscriptionRequest, idempotencyKey: string): Promise<WebhookSubscriptionSecretDto> {
    return firstValueFrom(
      this.http.post<WebhookSubscriptionSecretDto>(`${this.base}/subscriptions`, body, {
        context: new HttpContext().set(SEND_IF_MATCH, false),
        headers: idempotencyHeaders(idempotencyKey),
      }),
    );
  }

  async update(id: string, body: UpdateWebhookSubscriptionRequest): Promise<void> {
    await firstValueFrom(this.http.put(this.subUrl(id), body));
  }

  rotate(id: string): Promise<WebhookSubscriptionSecretDto> {
    return firstValueFrom(this.http.post<WebhookSubscriptionSecretDto>(`${this.subUrl(id)}/rotate-secret`, null));
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(this.subUrl(id)));
  }

  deliveries(status: DeliveryStatusFilter, cursor: string | null): Promise<Page<WebhookDeliveryDto>> {
    return getPage<WebhookDeliveryDto>(this.http, `${this.base}/deliveries`, params({ limit: 50, status, cursor }));
  }

  async redeliver(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base}/deliveries/${id}/redeliver`, null));
  }

  /** Polled: each poll carries its own correlation id so background traffic never borrows a user action's ref. */
  sink(): Promise<ReceivedWebhookDto[]> {
    return firstValueFrom(
      this.http.get<ReceivedWebhookDto[]>(this.sinkUrl, {
        params: params({ limit: 50 }),
        context: new HttpContext().set(CORRELATION_ID, newCorrelationId()),
      }),
    );
  }

  async setSinkSecret(secret: string): Promise<void> {
    await firstValueFrom(this.http.put(`${this.sinkUrl}/secret`, { secret }));
  }

  async clearSink(): Promise<void> {
    await firstValueFrom(this.http.delete(this.sinkUrl, { context: new HttpContext().set(SEND_IF_MATCH, false) }));
  }
}
