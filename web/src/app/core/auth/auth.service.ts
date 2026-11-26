import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { QueryClient } from '@tanstack/angular-query';
import { firstValueFrom } from 'rxjs';
import { TokenDto } from '../../api/models';
import { CONSOLE_CONFIG } from '../config';
import { EtagStore } from '../http/etag.store';
import { SessionStore } from './session.store';
import { ME_QUERY_KEY } from './access.service';

/**
 * Sign-in, refresh (single flight) and sign-out. The access token lives in the SessionStore (memory);
 * the refresh token rotates on every use and a reused one revokes the whole family server-side.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly queryClient = inject(QueryClient);
  private readonly etags = inject(EtagStore);
  private readonly base = inject(CONSOLE_CONFIG).apiBase;
  private inflight: Promise<string> | null = null;

  async signIn(email: string, password: string): Promise<void> {
    const token = await firstValueFrom(this.http.post<TokenDto>(`${this.base}/auth/token`, { email, password }));
    this.session.signedIn(token.accessToken, token.expiresIn, token.refreshToken);
    await this.queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
  }

  /**
   * Every concurrent 401 awaits the SAME refresh. Two parallel refreshes would present one rotating token twice,
   * which the API treats as theft and answers by revoking the family.
   */
  refreshOnce(): Promise<string> {
    this.inflight ??= this.refresh().finally(() => (this.inflight = null));
    return this.inflight;
  }

  private async refresh(): Promise<string> {
    const refreshToken = this.session.refreshToken();
    if (!refreshToken) throw new Error('No refresh token');
    try {
      const token = await firstValueFrom(this.http.post<TokenDto>(`${this.base}/auth/refresh`, { refreshToken }));
      this.session.signedIn(token.accessToken, token.expiresIn, token.refreshToken);
      void this.queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
      return token.accessToken;
    } catch (err) {
      this.expire();
      throw err;
    }
  }

  /** Restores a session after a reload from the tab's refresh token. */
  async restore(): Promise<boolean> {
    if (!this.session.refreshToken()) return false;
    this.session.restoring();
    try {
      await this.refreshOnce();
      return true;
    } catch {
      return false;
    }
  }

  async signOut(): Promise<void> {
    const refreshToken = this.session.refreshToken();
    this.session.cleared();
    this.reset();
    if (refreshToken) {
      await firstValueFrom(this.http.post(`${this.base}/auth/logout`, { refreshToken })).catch(() => undefined);
    }
    await this.router.navigate(['/sign-in']);
  }

  /** The session is over (refresh failed or was refused): drop everything and send the user to sign in. */
  expire(): void {
    if (!this.session.isAuthenticated() && this.session.status() !== 'restoring') return;
    this.session.cleared('expired');
    this.reset();
    const returnUrl = this.router.url.startsWith('/sign-in') ? undefined : this.router.url;
    void this.router.navigate(['/sign-in'], { queryParams: { reason: 'expired', returnUrl } });
  }

  private reset(): void {
    this.queryClient.clear();
    this.etags.clear();
  }
}
