import { ActivatedRouteSnapshot } from '@angular/router';

/** A link from a note to a document in the public repository. */
export interface HoodLink {
  /** Shown text, e.g. "D-056 The ETag is checked before the state machine". */
  readonly label: string;
  /** Repository path, optionally with an anchor, e.g. `docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity`. */
  readonly path: string;
}

/** What the "Under the hood" sidebar says about a page. The live API calls need no note. */
export interface HoodNote {
  /** Two lines: why the page works the way it does. */
  readonly why: readonly [string, string];
  /** Two to four links to decisions or sections of the claims write-up. */
  readonly decisions: readonly HoodLink[];
  /** Guards that apply on this screen, as short chips ("409 invalid transition"). */
  readonly guards: readonly string[];
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
