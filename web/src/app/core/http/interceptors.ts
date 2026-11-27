import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, from, switchMap, tap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../auth/session.store';
import { CONSOLE_CONFIG } from '../config';
import { callLogInterceptor } from './api-call-log';
import { AUTH_RETRIED, CORRELATION_ID, SEND_IF_MATCH, TRACK_ETAG } from './context';
import { CorrelationService } from './correlation.service';
import { EtagStore, resourceKey } from './etag.store';
import { toApiProblem } from './problem';

const CORRELATION_HEADER = 'X-Correlation-Id';
/** Only same-origin API calls (relative to the API base) get headers; a token never travels to another origin. */
const isApi = (req: HttpRequest<unknown>, base: string) => req.url.startsWith(base);
const isAuthEndpoint = (url: string) => /\/auth\/(token|refresh|logout)$/.test(url);

/** 1. X-Correlation-Id from the current user action (or a pinned id). */
export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(CONSOLE_CONFIG);
  if (!isApi(req, config.apiBase) || req.headers.has(CORRELATION_HEADER)) return next(req);
  const id = req.context.get(CORRELATION_ID) ?? inject(CorrelationService).id();
  return next(req.clone({ setHeaders: { [CORRELATION_HEADER]: id } }));
};

/** 2. Bearer token, and on 401 ONE shared refresh (single flight) followed by a replay of the original request. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(CONSOLE_CONFIG);
  if (!isApi(req, config.apiBase) || isAuthEndpoint(req.url)) return next(req);
  const session = inject(SessionStore);
  const auth = inject(AuthService);
  const withToken = (r: HttpRequest<unknown>, token: string | null) =>
    token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;

  return next(withToken(req, session.accessToken())).pipe(
    catchError((err: unknown) => {
      const status = (err as { status?: number }).status;
      if (status !== 401 || req.context.get(AUTH_RETRIED) || !session.refreshToken()) {
        if (status === 401 && !isAuthEndpoint(req.url)) auth.expire();
        return throwError(() => err);
      }
      return from(auth.refreshOnce()).pipe(
        switchMap((token) => {
          const replay = withToken(req, token);
          replay.context.set(AUTH_RETRIED, true);
          return next(replay).pipe(
            catchError((replayErr: unknown) => {
              // Refused even with a fresh token (user deactivated, tenant locked): the session is over.
              if ((replayErr as { status?: number }).status === 401) auth.expire();
              return throwError(() => replayErr);
            }),
          );
        }),
      );
    }),
  );
};

const CONDITIONAL = /^(PUT|PATCH|DELETE)$/;
const CONDITIONAL_POST =
  /\/(suspend|reactivate|rotate-secret|pickup|decision|settle|info-requests|info-requests\/[^/]+\/response)$/;

/** 3. Remember ETags per resource; send If-Match on writes to a resource we have a tag for. */
export const etagInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(CONSOLE_CONFIG);
  if (!isApi(req, config.apiBase)) return next(req);
  const store = inject(EtagStore);

  let outgoing = req;
  const conditional = CONDITIONAL.test(req.method) || (req.method === 'POST' && CONDITIONAL_POST.test(req.url));
  if (conditional && req.context.get(SEND_IF_MATCH) && !req.headers.has('If-Match')) {
    const tag = store.get(req.url);
    if (tag) outgoing = req.clone({ setHeaders: { 'If-Match': tag } });
  }

  return next(outgoing).pipe(
    tap((event: HttpEvent<unknown>) => {
      if (!(event instanceof HttpResponse) || !req.context.get(TRACK_ETAG)) return;
      const etag = event.headers.get('ETag');
      if (req.method === 'DELETE' && event.ok) {
        store.forget(req.url);
        return;
      }
      if (!etag) return;
      if (event.status === 201) {
        const location = event.headers.get('Location');
        store.set(location ? resourceKey(location) : req.url, etag);
      } else {
        store.set(req.url, etag);
      }
    }),
  );
};

/** 4. Every HttpErrorResponse becomes an ApiProblem (title, code, field errors, correlation ref, Retry-After). */
export const problemInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> =>
  next(req).pipe(
    catchError((err: unknown) =>
      throwError(() =>
        err instanceof HttpErrorResponse ? toApiProblem(err, req.method, req.headers.get(CORRELATION_HEADER)) : err,
      ),
    ),
  );

/** Request order: correlation -> auth -> etag -> problem -> call log -> backend (responses unwind in reverse). */
export const consoleInterceptors = [
  correlationInterceptor,
  authInterceptor,
  etagInterceptor,
  problemInterceptor,
  callLogInterceptor,
];
