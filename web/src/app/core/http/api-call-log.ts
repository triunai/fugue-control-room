import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { CONSOLE_CONFIG } from '../config';

/** The most calls kept; the oldest fall off. */
export const MAX_CALLS = 20;

/** Problem codes worth showing on a refused write (a guard speaking). */
const CODE_STATUSES = new Set([409, 412, 428]);

/**
 * One recorded API call. By construction it holds no body, no token, no header value and no query value:
 * only the method, a path with ids replaced, the NAMES of query parameters, the status, the time taken,
 * three yes/no markers and (on 409/412/428) the problem code.
 */
export interface ApiCall {
  readonly seq: number;
  readonly method: string;
  readonly path: string;
  readonly status: number;
  readonly ms: number;
  readonly ifMatch: boolean;
  readonly etag: boolean;
  readonly idempotencyKey: boolean;
  readonly code: string | null;
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NUMERIC = /^\d+$/;
const LONG_HEX = /^[0-9a-f]{16,}$/i;

/** "/api/v1/claims/9f..?status=Open&limit=20" -> "/claims/{id}?status&limit". The API base prefix is dropped. */
export function shortenPath(url: string, base: string, paramNames: readonly string[] = []): string {
  const noOrigin = url.replace(/^[a-z][a-z0-9+.-]*:\/\/[^/]+/i, '');
  const [pathPart, query = ''] = noOrigin.split('?', 2);
  const rest = pathPart.startsWith(base) ? pathPart.slice(base.length) : pathPart;
  const path = rest
    .split('/')
    .map((s) => (UUID.test(s) || NUMERIC.test(s) || LONG_HEX.test(s) ? '{id}' : s))
    .join('/');
  const names = [
    ...query
      .split('&')
      .map((kv) => kv.split('=')[0])
      .filter(Boolean),
    ...paramNames,
  ];
  const unique = [...new Set(names.map((n) => decodeURIComponent(n)))];
  return (path || '/') + (unique.length ? `?${unique.join('&')}` : '');
}

/** The calls the page on screen has made since it opened, newest first (cleared when the route changes). */
@Injectable({ providedIn: 'root' })
export class ApiCallLog {
  private seq = 0;
  private readonly _calls = signal<readonly ApiCall[]>([]);
  readonly calls = this._calls.asReadonly();
  readonly count = computed(() => this._calls().length);

  record(call: Omit<ApiCall, 'seq'>): void {
    this._calls.update((list) => [{ ...call, seq: ++this.seq }, ...list].slice(0, MAX_CALLS));
  }

  clear(): void {
    this._calls.set([]);
  }
}

/**
 * Records every API call into ApiCallLog. It sits after the ETag interceptor (so it sees the If-Match that was
 * really sent) and innermost, below the problem interceptor (so an error still carries its raw body for the `code`).
 */
export const callLogInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next) => {
  const base = inject(CONSOLE_CONFIG).apiBase;
  if (!req.url.startsWith(base)) return next(req);
  const log = inject(ApiCallLog);
  const started = performance.now();
  const path = shortenPath(req.url, base, req.params.keys());
  const done = (status: number, etag: boolean, code: string | null) =>
    log.record({
      method: req.method,
      path,
      status,
      ms: Math.round(performance.now() - started),
      ifMatch: req.headers.has('If-Match'),
      etag,
      idempotencyKey: req.headers.has('Idempotency-Key'),
      code: CODE_STATUSES.has(status) ? code : null,
    });

  return next(req).pipe(
    tap({
      next: (event: HttpEvent<unknown>) => {
        if (event instanceof HttpResponse) done(event.status, event.headers.has('ETag'), null);
      },
      error: (err: unknown) => {
        if (!(err instanceof HttpErrorResponse)) return;
        const body = err.error as { code?: unknown } | null;
        done(err.status, false, typeof body?.code === 'string' ? body.code : null);
      },
    }),
  );
};
