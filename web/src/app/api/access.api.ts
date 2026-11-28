import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import { SEND_IF_MATCH } from '../core/http/context';
import { EtagStore } from '../core/http/etag.store';
import type { CreateUserRequest, UpdateUserRequest, UserDto } from './generated/model';
import { getPage, params } from './http-page';
import { GroupDto, OverrideDto, Page } from './models';

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
  users: (q: string) => ['access', 'users', q] as const,
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

  /** Keyset-paged user directory (USERS_VIEW); `q` matches email and display name. */
  users(q: string, cursor: string | null, limit = 25): Promise<Page<UserDto>> {
    return getPage<UserDto>(this.http, `${this.base}/users`, params({ limit, cursor, q: q.trim() }));
  }

  /** Creates a user with an initial password and roles (USERS_MANAGE). The response never carries the password. */
  createUser(body: CreateUserRequest): Promise<UserDto> {
    return firstValueFrom(this.http.post<UserDto>(`${this.base}/users`, body));
  }

  /** One user (USERS_VIEW); the ETag interceptor records its ETag. */
  user(id: string): Promise<UserDto> {
    return firstValueFrom(this.http.get<UserDto>(`${this.base}/users/${id}`));
  }

  /** Edits a user (USERS_MANAGE) against the version you read: If-Match is sent explicitly, so a stale one is a 412. */
  updateUser(id: string, body: UpdateUserRequest, version: number): Promise<UserDto> {
    return firstValueFrom(
      this.http.put<UserDto>(`${this.base}/users/${id}`, body, { headers: { 'If-Match': `"${version}"` } }),
    );
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
