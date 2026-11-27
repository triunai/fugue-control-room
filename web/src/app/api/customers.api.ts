import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import { SEND_IF_MATCH, TRACK_ETAG } from '../core/http/context';
import { EtagStore } from '../core/http/etag.store';
import { idempotencyHeaders } from '../core/http/idempotency';
import { getPage, params } from './http-page';
import { CreateCustomerRequest, CustomerDto, CustomerStatus, Page, UpdateCustomerRequest } from './models';

export interface CustomerFilter {
  search: string;
  status: '' | 'active' | 'suspended';
}

export const customerKeys = {
  all: ['customers'] as const,
  list: (f: CustomerFilter) => ['customers', 'list', f] as const,
  one: (id: string) => ['customers', 'one', id] as const,
};

@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);
  private readonly etags = inject(EtagStore);
  private readonly base = `${inject(CONSOLE_CONFIG).apiBase}/customers`;

  url(id: string): string {
    return `${this.base}/${id}`;
  }

  async list(filter: CustomerFilter, cursor: string | null, limit = 20): Promise<Page<CustomerDto>> {
    const page = await getPage<CustomerDto>(
      this.http,
      this.base,
      params({ limit, cursor, search: filter.search.trim(), status: filter.status }),
    );
    // Seed If-Match for row actions (suspend, delete) from each item's version.
    for (const c of page.items) if (!this.etags.get(this.url(c.id))) this.etags.seed(this.url(c.id), c.version);
    return page;
  }

  get(id: string): Promise<CustomerDto> {
    return firstValueFrom(this.http.get<CustomerDto>(this.url(id)));
  }

  /** Reads the latest version WITHOUT replacing the tracked ETag: used to explain a 412 before the user chooses. */
  peek(id: string): Promise<CustomerDto> {
    return firstValueFrom(
      this.http.get<CustomerDto>(this.url(id), { context: new HttpContext().set(TRACK_ETAG, false) }),
    );
  }

  /** `idempotencyKey` makes a retry of this submission safe: the same key with the same body returns the first response. */
  create(body: CreateCustomerRequest, idempotencyKey: string): Promise<CustomerDto> {
    return firstValueFrom(
      this.http.post<CustomerDto>(this.base, body, {
        context: new HttpContext().set(SEND_IF_MATCH, false),
        headers: idempotencyHeaders(idempotencyKey),
      }),
    );
  }

  async update(id: string, body: UpdateCustomerRequest): Promise<void> {
    await firstValueFrom(this.http.put(this.url(id), body));
  }

  async transition(id: string, to: CustomerStatus): Promise<void> {
    await firstValueFrom(this.http.post(`${this.url(id)}/${to === 'Suspended' ? 'suspend' : 'reactivate'}`, null));
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(this.url(id)));
  }
}
