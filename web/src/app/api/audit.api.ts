import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { CONSOLE_CONFIG } from '../core/config';
import { getPage, params } from './http-page';
import type { AuditSearchResultDto, AuditTraceDto } from './generated/model';
import { AuditLogDto, Page } from './models';

/** Every filter is an exact match; `from` is inclusive and `to` exclusive (ISO instants). */
export interface AuditFilter {
  entityType: string;
  entityId: string;
  actorUserId: string;
  action: string;
  correlationId: string;
  /** Full-text query (Meilisearch); search mode when set. The exact-match filters above still apply. */
  q: string;
  outcome: string;
  from: string;
  to: string;
}

export const EMPTY_AUDIT_FILTER: AuditFilter = {
  entityType: '',
  entityId: '',
  actorUserId: '',
  action: '',
  correlationId: '',
  q: '',
  outcome: '',
  from: '',
  to: '',
};

export const auditKeys = {
  list: (f: AuditFilter) => ['audit', 'list', f] as const,
  search: (f: AuditFilter) => ['audit', 'search', f] as const,
  trace: (correlationId: string) => ['audit', 'trace', correlationId] as const,
};

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(CONSOLE_CONFIG).apiBase}/audit`;

  list(f: AuditFilter, cursor: string | null, limit = 50): Promise<Page<AuditLogDto>> {
    const iso = (local: string) => (local ? new Date(local).toISOString() : '');
    return getPage<AuditLogDto>(
      this.http,
      this.base,
      params({
        limit,
        cursor,
        entityType: f.entityType.trim(),
        entityId: f.entityId.trim(),
        actorUserId: f.actorUserId.trim(),
        action: f.action.trim(),
        correlationId: f.correlationId.trim(),
        from: iso(f.from),
        to: iso(f.to),
      }),
    );
  }

  /** Typo-tolerant search (AUDIT_READ). 503 `search.unavailable` when the engine is off or unreachable. */
  search(f: AuditFilter, limit = 50): Promise<AuditSearchResultDto> {
    const iso = (local: string) => (local ? new Date(local).toISOString() : '');
    return firstValueFrom(
      this.http.get<AuditSearchResultDto>(`${this.base}/search`, {
        params: params({
          q: f.q.trim(),
          entityType: f.entityType.trim(),
          action: f.action.trim(),
          outcome: f.outcome,
          from: iso(f.from),
          to: iso(f.to),
          limit,
        }),
      }),
    );
  }

  /** "The Score" (D-064, AUDIT_READ): the command, events and webhook deliveries of one correlation id. 404 `audit.trace_not_found`. */
  trace(correlationId: string): Promise<AuditTraceDto> {
    return firstValueFrom(this.http.get<AuditTraceDto>(`${this.base}/trace/${encodeURIComponent(correlationId)}`));
  }
}
