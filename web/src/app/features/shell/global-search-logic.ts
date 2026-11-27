import type { AuditSearchResultDto } from '../../api/generated/model';
import { isApiProblem } from '../../core/http/problem';
import { HighlightSegment, highlightSegments } from '../audit/highlight';

/** The header search is for people who can read the audit trail. */
export function searchVisible(has: (code: string) => boolean): boolean {
  return has('AUDIT_READ');
}

export const DROPDOWN_HITS = 8;

export interface SearchRow {
  id: string;
  /** The action, as highlight segments (the engine's <em> snippet when it matched there). */
  action: HighlightSegment[];
  actor: string;
  outcome: string;
  entityType: string;
  entityId: string;
}

/** The top hits for the dropdown: at most 8, each carrying the entity the row links to. */
export function toRows(result: AuditSearchResultDto, max = DROPDOWN_HITS): SearchRow[] {
  return result.hits.slice(0, max).map((h) => {
    const snippet = h.highlights?.['action'];
    return {
      id: h.id,
      action: highlightSegments(typeof snippet === 'string' ? snippet : h.action),
      actor: h.actorDisplay ?? 'system',
      outcome: h.outcome,
      entityType: h.entityType ?? '',
      entityId: h.entityId ?? '',
    };
  });
}

/** 503 `search.unavailable` means the engine is off: the dropdown says "Search is off". */
export function isSearchOff(error: unknown): boolean {
  return isApiProblem(error) && error.status === 503;
}
