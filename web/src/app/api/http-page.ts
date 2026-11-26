import { HttpClient, HttpContext, HttpParams, HttpResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Page } from './models';

/** Drops undefined, null and empty-string params so filters map 1:1 onto the query string. */
export function params(values: Record<string, string | number | boolean | null | undefined>): HttpParams {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(values)) {
    if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
  }
  return p;
}

/** The next cursor from X-Next-Cursor, falling back to the `cursor` of an RFC 8288 Link rel="next". */
export function nextCursor(res: HttpResponse<unknown>): string | null {
  const header = res.headers.get('X-Next-Cursor');
  if (header) return header;
  const link = res.headers.get('Link');
  const next = link?.split(',').find((part) => /rel="?next"?/.test(part));
  const url = next?.match(/<([^>]+)>/)?.[1];
  if (!url) return null;
  try {
    return new URL(url, 'http://x').searchParams.get('cursor');
  } catch {
    return null;
  }
}

export async function getPage<T>(
  http: HttpClient,
  url: string,
  query: HttpParams,
  context?: HttpContext,
): Promise<Page<T>> {
  const res = await firstValueFrom(http.get<T[]>(url, { params: query, observe: 'response', context }));
  return { items: res.body ?? [], nextCursor: nextCursor(res) };
}
