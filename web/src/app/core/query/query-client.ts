import { inject } from '@angular/core';
import { MutationCache, QueryClient } from '@tanstack/angular-query';
import { ApiProblem, isApiProblem } from '../http/problem';
import { ToastStore } from '../ui/toast.store';

const TRANSIENT = new Set([429, 503]);

/** Queries retry only transient failures (429, 503, network), at most 3 times. */
export function shouldRetryQuery(failureCount: number, error: unknown): boolean {
  if (!isApiProblem(error)) return false;
  return failureCount < 3 && (TRANSIENT.has(error.status) || error.status === 0);
}

/** Mutations retry 429 (nothing ran) and a 503 on non-POST (If-Match makes a replayed PUT/DELETE safe). */
export function shouldRetryMutation(failureCount: number, error: unknown): boolean {
  if (!isApiProblem(error) || failureCount >= 2) return false;
  return error.status === 429 || (error.status === 503 && error.method !== 'POST');
}

/** Honour Retry-After when the server sends one; otherwise exponential backoff with jitter, capped at 8 s. */
export function retryDelay(attempt: number, error: unknown): number {
  if (isApiProblem(error) && error.retryAfterSeconds != null) return Math.min(error.retryAfterSeconds, 30) * 1000;
  const base = Math.min(8000, 500 * 2 ** attempt);
  return base / 2 + Math.random() * (base / 2);
}

export interface MutationMeta extends Record<string, unknown> {
  /** Statuses the screen renders itself (field errors, the 412 banner); no toast for them. */
  handles?: number[];
  /** Success toast title. */
  success?: string;
}

declare module '@tanstack/query-core' {
  interface Register {
    mutationMeta: MutationMeta;
  }
}

export function createQueryClient(): QueryClient {
  const toasts = inject(ToastStore);
  return new QueryClient({
    defaultOptions: {
      queries: { retry: shouldRetryQuery, retryDelay, staleTime: 15_000, refetchOnWindowFocus: false },
      mutations: { retry: shouldRetryMutation, retryDelay },
    },
    mutationCache: new MutationCache({
      onError: (error, _vars, _ctx, mutation) => {
        if (!isApiProblem(error)) return;
        const handles = mutation.meta?.handles ?? [];
        if (handles.includes(error.status) && !(error.status === 400 && !error.isValidation)) return;
        if (error.status === 401) return; // the auth interceptor already sent the user to sign in
        toasts.problem(error as ApiProblem);
      },
    }),
  });
}
