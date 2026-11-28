import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import { SEND_IF_MATCH } from '../core/http/context';
import { EtagStore } from '../core/http/etag.store';
import { idempotencyHeaders } from '../core/http/idempotency';
import { ClaimSearchResultDto } from './generated/model';
import { getPage, params } from './http-page';
import {
  ClaimDto,
  ClaimExposureDto,
  ClaimStatus,
  ClaimTimelineEntryDto,
  ClaimWorkloadDto,
  DecideClaimRequest,
  Page,
  SubmitClaimRequest,
} from './models';

/** One question put to the claimant and its answer (`GET /claims/{id}/info-requests`); hand-written until the client is regenerated. */
export interface ClaimQuestionEntry {
  id: string;
  question: string;
  askedBy: string;
  askedAt: string;
  answer?: string | null;
  answeredAt?: string | null;
}

export type AssignedFilter = 'any' | 'me' | 'none';

/** Everything the list endpoint filters on. `status` is one status or '' for all. */
export interface ClaimFilter {
  /** One status, '' for all, or 'open' for Submitted + UnderReview + InfoRequested (the API takes comma-separated statuses). */
  status: '' | 'open' | ClaimStatus;
  assigned: AssignedFilter;
  market: string;
  line: string;
  q: string;
  /** Optional filters the backend is adding (officerId, age window in days); absent means not filtered. */
  officerId?: string;
  minAgeDays?: number;
  maxAgeDays?: number;
}

export const OPEN_STATUSES = 'Submitted,UnderReview,InfoRequested';

export const EMPTY_CLAIM_FILTER: ClaimFilter = { status: '', assigned: 'any', market: '', line: '', q: '' };

export const claimKeys = {
  all: ['claims'] as const,
  list: (f: ClaimFilter) => ['claims', 'list', f] as const,
  /** An infinite (paged) list needs its own key: sharing `list` with a plain query hands it data without `pages`. */
  listPaged: (f: ClaimFilter) => ['claims', 'list', f, 'paged'] as const,
  counts: (f: Omit<ClaimFilter, 'status'>) => ['claims', 'counts', f] as const,
  search: (q: string) => ['claims', 'search', q] as const,
  one: (id: string) => ['claims', 'one', id] as const,
  questions: (id: string) => ['claims', 'questions', id] as const,
  timeline: (id: string) => ['claims', 'timeline', id] as const,
  workload: ['claims', 'workload'] as const,
  exposure: ['claims', 'exposure'] as const,
};

/** The list endpoint caps a page at 100: a count read from one page is "100+" when a cursor remains. */
export const COUNT_PAGE = 100;

export interface StatusCount {
  n: number;
  more: boolean;
}

@Injectable({ providedIn: 'root' })
export class ClaimsApi {
  private readonly http = inject(HttpClient);
  private readonly etags = inject(EtagStore);
  private readonly base = `${inject(CONSOLE_CONFIG).apiBase}/claims`;

  url(id: string): string {
    return `${this.base}/${id}`;
  }

  async list(filter: ClaimFilter, cursor: string | null, limit = 20): Promise<Page<ClaimDto>> {
    const page = await getPage<ClaimDto>(
      this.http,
      this.base,
      params({
        limit,
        cursor,
        status: filter.status === 'open' ? OPEN_STATUSES : filter.status,
        assigned: filter.assigned === 'any' ? '' : filter.assigned,
        market: filter.market,
        line: filter.line,
        q: filter.q.trim(),
        officerId: filter.officerId,
        minAgeDays: filter.minAgeDays,
        maxAgeDays: filter.maxAgeDays,
      }),
    );
    // Seed If-Match for the detail page's first action from each item's version.
    for (const c of page.items) if (!this.etags.get(this.url(c.id))) this.etags.seed(this.url(c.id), c.version);
    return page;
  }

  /** Counts per status for the KPI strip and tabs, read from the list endpoint (there is no count endpoint: see the lane report). */
  async counts(filter: Omit<ClaimFilter, 'status'>): Promise<Record<ClaimStatus, StatusCount>> {
    const statuses = Object.values(ClaimStatus);
    const pages = await Promise.all(statuses.map((status) => this.list({ ...filter, status }, null, COUNT_PAGE)));
    const out = {} as Record<ClaimStatus, StatusCount>;
    statuses.forEach((s, i) => (out[s] = { n: pages[i].items.length, more: pages[i].nextCursor !== null }));
    return out;
  }

  /** Typo-tolerant full-text search (D-063); the server scopes it to the caller's own claims unless they hold CLAIMS_ALL. 503 `search.unavailable` when the engine is off. */
  search(q: string, limit = 20): Promise<ClaimSearchResultDto> {
    return firstValueFrom(
      this.http.get<ClaimSearchResultDto>(`${this.base}/search`, { params: params({ q: q.trim(), limit }) }),
    );
  }

  get(id: string): Promise<ClaimDto> {
    return firstValueFrom(this.http.get<ClaimDto>(this.url(id)));
  }

  timeline(id: string): Promise<ClaimTimelineEntryDto[]> {
    return firstValueFrom(this.http.get<ClaimTimelineEntryDto[]>(`${this.url(id)}/timeline`));
  }

  /** The sample letter of a decided claim as a PDF (`GET /claims/{id}/letter`); 409 `claim.letter_not_available` before a decision. */
  letter(id: string): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.url(id)}/letter`, { responseType: 'blob' }));
  }

  /** Every question asked on the claim, newest first. */
  questions(id: string): Promise<ClaimQuestionEntry[]> {
    return firstValueFrom(this.http.get<ClaimQuestionEntry[]>(`${this.url(id)}/info-requests`));
  }

  /** `idempotencyKey` makes a retry of this submission safe (the same key and body return the first claim). */
  submit(body: SubmitClaimRequest, idempotencyKey: string): Promise<ClaimDto> {
    return firstValueFrom(
      this.http.post<ClaimDto>(this.base, body, {
        context: new HttpContext().set(SEND_IF_MATCH, false),
        headers: idempotencyHeaders(idempotencyKey),
      }),
    );
  }

  // Every command below carries If-Match from the ETag store (the interceptor) and answers with the claim as it now stands.
  pickup(id: string): Promise<ClaimDto> {
    return firstValueFrom(this.http.post<ClaimDto>(`${this.url(id)}/pickup`, null));
  }

  setReserve(id: string, amountMinor: number): Promise<ClaimDto> {
    return firstValueFrom(this.http.put<ClaimDto>(`${this.url(id)}/reserve`, { amountMinor }));
  }

  requestInfo(id: string, question: string): Promise<ClaimDto> {
    return firstValueFrom(this.http.post<ClaimDto>(`${this.url(id)}/info-requests`, { question }));
  }

  respond(id: string, requestId: string, answer: string): Promise<ClaimDto> {
    return firstValueFrom(this.http.post<ClaimDto>(`${this.url(id)}/info-requests/${requestId}/response`, { answer }));
  }

  decide(id: string, body: DecideClaimRequest): Promise<ClaimDto> {
    return firstValueFrom(this.http.post<ClaimDto>(`${this.url(id)}/decision`, body));
  }

  settle(id: string): Promise<ClaimDto> {
    return firstValueFrom(this.http.post<ClaimDto>(`${this.url(id)}/settle`, null));
  }

  workload(): Promise<ClaimWorkloadDto> {
    return firstValueFrom(this.http.get<ClaimWorkloadDto>(`${this.base}/workload`));
  }

  exposure(): Promise<ClaimExposureDto> {
    return firstValueFrom(this.http.get<ClaimExposureDto>(`${this.base}/exposure`));
  }
}
