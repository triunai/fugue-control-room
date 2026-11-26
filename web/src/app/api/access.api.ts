import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import { SEND_IF_MATCH } from '../core/http/context';
import { EtagStore } from '../core/http/etag.store';
import { GroupDto, OverrideDto } from './models';

/** Permission codes the API seeds (migration AddIdentity); used to offer override choices. */
export const PERMISSION_CODES = [
  'CUSTOMERS_VIEW',
  'CUSTOMERS_INSERT',
  'CUSTOMERS_EDIT',
  'CUSTOMERS_DELETE',
  'CUSTOMERS_SUSPEND',
  'CUSTOMERS_ALL',
  'GROUPS_VIEW',
  'GROUPS_MANAGE',
  'OVERRIDES_VIEW',
  'OVERRIDES_MANAGE',
  'MODULES_VIEW',
  'MODULES_MANAGE',
  'AUDIT_READ',
  'WEBHOOKS_MANAGE',
] as const;

export interface SetOverride {
  isAllowed: boolean;
  reason: string | null;
  expiresAt: string | null;
}

export const accessKeys = {
  groups: ['access', 'groups'] as const,
  overrides: (userId: string) => ['access', 'overrides', userId] as const,
};

@Injectable({ providedIn: 'root' })
export class AccessApi {
  private readonly http = inject(HttpClient);
  private readonly etags = inject(EtagStore);
  private readonly base = inject(CONSOLE_CONFIG).apiBase;

  private url(userId: string, permission: string): string {
    return `${this.base}/overrides/${userId}/${permission}`;
  }

  groups(): Promise<GroupDto[]> {
    return firstValueFrom(this.http.get<GroupDto[]>(`${this.base}/groups`));
  }

  async overrides(userId: string): Promise<OverrideDto[]> {
    const list = await firstValueFrom(this.http.get<OverrideDto[]>(`${this.base}/overrides/${userId}`));
    for (const o of list) this.etags.seed(this.url(userId, o.permission), o.version);
    return list;
  }

  /** A NEW override sends no If-Match; replacing an existing one sends its version (412 if someone changed it). */
  async set(userId: string, permission: string, body: SetOverride, exists: boolean): Promise<void> {
    await firstValueFrom(
      this.http.put(this.url(userId, permission), body, { context: new HttpContext().set(SEND_IF_MATCH, exists) }),
    );
  }

  async remove(userId: string, permission: string): Promise<void> {
    await firstValueFrom(this.http.delete(this.url(userId, permission)));
  }
}
