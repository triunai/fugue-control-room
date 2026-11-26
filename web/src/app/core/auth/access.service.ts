import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { QueryClient, injectQuery } from '@tanstack/angular-query';
import { firstValueFrom } from 'rxjs';
import { MeDto } from '../../api/models';
import { CONSOLE_CONFIG } from '../config';
import { SessionStore } from './session.store';

export const ME_QUERY_KEY = ['me'] as const;

/**
 * Who am I and what may I do: GET /api/v1/me as a TanStack query (server state). Nav items, route guards and
 * *hasPermission all read `has()`, so a refetch (after sign-in, after every token refresh, on window focus)
 * re-evaluates the whole UI. Checks use permission CODES, never role names.
 */
@Injectable({ providedIn: 'root' })
export class AccessService {
  private readonly http = inject(HttpClient);
  private readonly queryClient = inject(QueryClient);
  private readonly session = inject(SessionStore);
  private readonly base = inject(CONSOLE_CONFIG).apiBase;

  private readonly options = () => ({
    queryKey: ME_QUERY_KEY,
    queryFn: () => firstValueFrom(this.http.get<MeDto>(`${this.base}/me`)),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });

  readonly query = injectQuery(() => ({ ...this.options(), enabled: this.session.isAuthenticated() }));

  readonly me = computed(() => (this.session.isAuthenticated() ? this.query.data() : undefined));
  readonly permissions = computed(() => new Set(this.me()?.permissions ?? []));
  readonly modules = computed(() => new Set((this.me()?.modules ?? []).map((m) => m.code)));

  has(code: string | readonly string[]): boolean {
    const set = this.permissions();
    return typeof code === 'string' ? set.has(code) : code.some((c) => set.has(c));
  }

  /** For guards: the cached /me if fresh enough, else one fetch. */
  ensure(): Promise<MeDto> {
    return this.queryClient.ensureQueryData({ ...this.options(), staleTime: 10_000 });
  }

  /** Synchronous check against whatever /me says right now (guards after ensure()). */
  hasIn(me: MeDto, code: string | readonly string[]): boolean {
    return typeof code === 'string' ? me.permissions.includes(code) : code.some((c) => me.permissions.includes(c));
  }
}
