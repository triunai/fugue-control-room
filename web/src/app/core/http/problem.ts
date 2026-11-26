import { HttpErrorResponse } from '@angular/common/http';

/**
 * An RFC 9457 problem from the API, normalised. Every failed request surfaces as one of these, so screens
 * never parse HttpErrorResponse themselves. `errors` keys are camelCased to match form control names.
 */
export class ApiProblem extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly code: string | null,
    readonly detail: string | null,
    readonly errors: Record<string, string[]>,
    readonly correlationId: string | null,
    readonly traceId: string | null,
    readonly retryAfterSeconds: number | null,
    readonly method: string,
    readonly url: string,
  ) {
    super(`${status} ${title}`);
    this.name = 'ApiProblem';
  }

  get isValidation(): boolean {
    return this.status === 400 && Object.keys(this.errors).length > 0;
  }

  get isNetwork(): boolean {
    return this.status === 0;
  }
}

export function isApiProblem(value: unknown): value is ApiProblem {
  return value instanceof ApiProblem;
}

/** "Email" -> "email", "EventTypes[0]" -> "eventTypes", "address.Street" -> "address.street". */
export function camelKey(key: string): string {
  return key
    .replace(/\[\d+\]/g, '')
    .split('.')
    .map((part) => (part ? part[0].toLowerCase() + part.slice(1) : part))
    .join('.');
}

export function parseRetryAfter(value: string | null, now = Date.now()): number | null {
  if (!value) return null;
  const seconds = Number(value);
  if (Number.isFinite(seconds)) return Math.max(0, seconds);
  const date = Date.parse(value);
  return Number.isNaN(date) ? null : Math.max(0, Math.ceil((date - now) / 1000));
}

const FALLBACK_TITLES: Record<number, string> = {
  0: 'The API cannot be reached',
  400: 'The request was not valid',
  401: 'Sign in again to continue',
  403: 'You do not have permission for that',
  404: 'Not found',
  409: 'That conflicts with the current state',
  412: 'Changed since you opened it',
  428: 'If-Match header missing',
  429: 'Too many requests',
  500: 'The server could not complete that',
  503: 'The service is briefly unavailable',
};

export function toApiProblem(
  error: HttpErrorResponse,
  method: string,
  requestCorrelationId: string | null,
): ApiProblem {
  const body = (typeof error.error === 'object' && error.error) || {};
  const raw = body as Record<string, unknown>;
  const errors: Record<string, string[]> = {};
  if (raw['errors'] && typeof raw['errors'] === 'object') {
    for (const [key, messages] of Object.entries(raw['errors'] as Record<string, unknown>)) {
      const list = Array.isArray(messages) ? messages.map(String) : [String(messages)];
      const k = camelKey(key);
      errors[k] = [...(errors[k] ?? []), ...list];
    }
  }
  const status = error.status;
  // A 404 never says "forbidden": the API hides existence, and so does the copy.
  const title =
    status === 404
      ? 'Not found'
      : (typeof raw['title'] === 'string' && raw['title']) ||
        FALLBACK_TITLES[status] ||
        error.statusText ||
        'Request failed';
  return new ApiProblem(
    status,
    title,
    typeof raw['code'] === 'string' ? raw['code'] : null,
    typeof raw['detail'] === 'string' ? raw['detail'] : null,
    errors,
    (typeof raw['correlationId'] === 'string' && raw['correlationId']) ||
      error.headers?.get('X-Correlation-Id') ||
      requestCorrelationId,
    typeof raw['traceId'] === 'string' ? raw['traceId'] : null,
    parseRetryAfter(error.headers?.get('Retry-After') ?? null),
    method,
    error.url ?? '',
  );
}
