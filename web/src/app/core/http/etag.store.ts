import { Injectable } from '@angular/core';

/** The resource a URL acts on: query string dropped, action suffixes (suspend, reactivate, rotate-secret) stripped. */
export function resourceKey(url: string): string {
  let path = url;
  try {
    path = new URL(url, 'http://x').pathname;
  } catch {
    /* keep as-is */
  }
  return path.replace(/\/+$/, '').replace(/\/(suspend|reactivate|rotate-secret)$/, '');
}

/**
 * The last ETag seen per resource URL (from GET, 201 Location and 204 responses, or seeded from a list item's version).
 * The ETag interceptor sends it back as If-Match on PUT, PATCH, DELETE and state-transition POSTs.
 */
@Injectable({ providedIn: 'root' })
export class EtagStore {
  private readonly tags = new Map<string, string>();

  get(url: string): string | undefined {
    return this.tags.get(resourceKey(url));
  }

  set(url: string, etag: string): void {
    this.tags.set(resourceKey(url), etag);
  }

  /** Lists carry `version` per item but no per-item header; seed from it so row actions can send If-Match. */
  seed(url: string, version: number): void {
    this.tags.set(resourceKey(url), `"${version}"`);
  }

  forget(url: string): void {
    this.tags.delete(resourceKey(url));
  }

  clear(): void {
    this.tags.clear();
  }

  /** Version number of a tag like "7" or W/"7". */
  static version(tag: string | undefined): number | null {
    if (!tag) return null;
    const n = Number(tag.replace(/^W\//, '').replace(/"/g, ''));
    return Number.isFinite(n) ? n : null;
  }
}
