import { HoodNote } from '../hood/hood-note';

export const AUDIT_HOOD: HoodNote = {
  why: [
    'Every change and every refused command is recorded in the same transaction, with values masked before they are stored.',
    'Typing a query uses Meilisearch (typo-tolerant). If the engine is off the API answers 503 search.unavailable and the page falls back to the filters.',
  ],
  decisions: [
    { label: 'D-031 Read API behind AUDIT_READ', path: 'docs/decisions.md' },
    { label: 'D-058 Audit search on Meilisearch', path: 'docs/decisions.md' },
    { label: 'D-030 Masking at write time', path: 'docs/decisions.md' },
    { label: 'ADR-010 Audit log', path: 'docs/adr/ADR-010-audit-log.md' },
  ],
  guards: ['403 permission (AUDIT_READ)', '503 search unavailable'],
};
