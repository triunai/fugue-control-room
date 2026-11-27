import { ActivatedRouteSnapshot } from '@angular/router';

/** A link from a note to a document in the public repository. */
export interface HoodLink {
  /** Shown text, e.g. "D-056 The ETag is checked before the state machine". */
  readonly label: string;
  /** Repository path, optionally with an anchor, e.g. `docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity`. */
  readonly path: string;
}

/** One guard: the chip (status + name) and the real backend lines that enforce it. */
export interface HoodGuard {
  /** HTTP status, or a short tag when the guard is not an HTTP answer, e.g. `409`. */
  readonly status: string;
  /** Short name, e.g. `invalid transition`. */
  readonly name: string;
  /** Repository path of the file the excerpt is copied from, e.g. `src/Fugue.Api/Controllers/ApiControllerBase.cs`. */
  readonly path: string;
  /** 2 to 8 lines copied verbatim from `path` (indentation trimmed, `…` for elided lines). */
  readonly code: string;
  /** What the client sees, in one plain line. */
  readonly note?: string;
}

/** A test that covers the page, copied by name from the repository so a panelist can find it. */
export interface HoodTest {
  readonly layer: 'unit' | 'integration' | 'arch' | 'api' | 'web' | 'e2e';
  /** Repository path of the test file. */
  readonly path: string;
  /** The real test method, `it()` title or Bruno request name, verbatim: the file must contain it. */
  readonly name: string;
  /** One plain line (at most 140 characters) on what it proves. */
  readonly asserts: string;
}

/** What the "Under the hood" sidebar says about a page. The live API calls need no note. */
export interface HoodNote {
  /** Two lines: why the page works the way it does. */
  readonly why: readonly string[];
  /** Two to four short lines on how the API does it: endpoints, concurrency, idempotency, scope. Only what is true of the page. */
  readonly how: readonly string[];
  /** Three to five tests behind the page, one per layer where one exists. */
  readonly tests: readonly HoodTest[];
  /** ADRs that really govern the page, linked as `docs/adr/ADR-NNN-slug.md`; shown first. */
  readonly adrs: readonly HoodLink[];
  /** Two to four links to decisions or sections of the claims write-up. */
  readonly decisions: readonly HoodLink[];
  /** Guards that apply on this screen: a chip ("409 invalid transition") that opens onto the backend code enforcing it. */
  readonly guards: readonly HoodGuard[];
}

const REPO_BLOB = 'https://github.com/triunai/fugue-control-room/blob/main/';

export function hoodUrl(path: string): string {
  return REPO_BLOB + path;
}

/** The note of the page on screen: `data.hood` of the deepest route that sets one, else null. */
export function noteFor(root: ActivatedRouteSnapshot): HoodNote | null {
  let note: HoodNote | null = null;
  for (let r: ActivatedRouteSnapshot | null = root; r; r = r.firstChild) {
    note = (r.data?.['hood'] as HoodNote | undefined) ?? note;
  }
  return note;
}

/** The URL path without query or fragment: the identity of "the page" for clearing the call log. */
export function pagePath(url: string): string {
  return url.split(/[?#]/, 1)[0];
}
