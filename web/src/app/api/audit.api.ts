import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { CONSOLE_CONFIG } from '../core/config';
import { getPage, params } from './http-page';
import { AuditLogDto, Page } from './models';

/** Every filter is an exact match; `from` is inclusive and `to` exclusive (ISO instants). */
export interface AuditFilter {
  entityType: string;
  entityId: string;
  actorUserId: string;
  action: string;
  correlationId: string;
  from: string;
  to: string;
}

export const EMPTY_AUDIT_FILTER: AuditFilter = {
  entityType: '',
  entityId: '',
  actorUserId: '',
  action: '',
  correlationId: '',
  from: '',
  to: '',
};

export const auditKeys = {
  list: (f: AuditFilter) => ['audit', 'list', f] as const,
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
}
