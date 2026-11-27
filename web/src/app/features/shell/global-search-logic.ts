import type { AuditSearchResultDto, ClaimSearchResultDto, ClaimStatus } from '../../api/generated/model';
import { isApiProblem } from '../../core/http/problem';
import { HighlightSegment, highlightSegments } from '../audit/highlight';
import { STATUS_LABEL } from '../claims/claims-logic';

/** The header search is for anyone who can read claims (a Claims group) or the audit trail (an Audit group). */
export function searchVisible(has: (code: string) => boolean): boolean {
  return has('CLAIMS_READ') || has('AUDIT_READ');
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

export interface ClaimRow {
  id: string;
  number: HighlightSegment[];
  status: ClaimSearchResultDto['hits'][number]['status'];
  /** Market, line and place, joined by " · ", with the matched words marked. */
  meta: HighlightSegment[];
  /** The description: the engine's snippet around the match when it matched there, else the whole text. */
  description: HighlightSegment[];
  /** Matches in fields the row does not otherwise show (policy, claimant, market name): the rest of "why this hit". */
  matched: { label: string; text: HighlightSegment[] }[];
}

const SEP: HighlightSegment = { text: ' · ', em: false };

/** The engine's `<em>` snippet for a field when it matched there, else the plain value. */
function field(h: ClaimSearchResultDto['hits'][number], name: string, plain: string): HighlightSegment[] {
  const snippet = h.highlights?.[name];
  return highlightSegments(typeof snippet === 'string' ? snippet : plain);
}

/** Hidden fields that can explain a match, in the order they are listed. */
const MATCHED_ON: readonly { field: string; label: string }[] = [
  { field: 'policyNumber', label: 'Policy' },
  { field: 'claimantName', label: 'Claimant' },
  { field: 'marketName', label: 'Market' },
];

/** The top claim hits for the dropdown: number, status, market, line and place, the description, and why each matched. */
export function toClaimRows(result: ClaimSearchResultDto, max = DROPDOWN_HITS): ClaimRow[] {
  return result.hits.slice(0, max).map((h) => {
    const meta = [field(h, 'market', h.market), field(h, 'line', h.line)];
    if (h.incidentLocation) meta.push(field(h, 'incidentLocation', h.incidentLocation));
    return {
      id: h.id,
      number: field(h, 'number', h.number),
      status: h.status,
      meta: meta.flatMap((part, i) => (i === 0 ? part : [SEP, ...part])),
      description: field(h, 'incidentDescription', h.incidentDescription),
      matched: MATCHED_ON.flatMap((m) => {
        const snippet = h.highlights?.[m.field];
        return typeof snippet === 'string' ? [{ label: m.label, text: highlightSegments(snippet) }] : [];
      }),
    };
  });
}

const FACET_ORDER: readonly { key: string; label: string }[] = [
  { key: 'market', label: 'Market' },
  { key: 'line', label: 'Line' },
  { key: 'status', label: 'Status' },
];

/**
 * The match counts the API returns over ALL matches (not only the hits shown), e.g. "Market SG 3, MY 1 · Line Motor 4".
 * Status values use the console's labels. Empty when there are no facets.
 */
export function facetSummary(result: ClaimSearchResultDto): string {
  const facets = result.facets ?? {};
  return FACET_ORDER.flatMap(({ key, label }) => {
    const counts = Object.entries(facets[key] ?? {}).filter(([, n]) => n > 0);
    if (counts.length === 0) return [];
    counts.sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
    const values = counts.map(([v, n]) => `${key === 'status' ? (STATUS_LABEL[v as ClaimStatus] ?? v) : v} ${n}`);
    return [`${label} ${values.join(', ')}`];
  }).join(' · ');
}
