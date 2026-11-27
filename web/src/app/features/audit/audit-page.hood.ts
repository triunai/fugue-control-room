import { HoodNote } from '../hood/hood-note';

export const AUDIT_HOOD: HoodNote = {
  why: [
    'Every change and every refused command is recorded in the same transaction, with values masked before they are stored.',
    'Typing a query uses Meilisearch (typo-tolerant). If the engine is off the API answers 503 search.unavailable and the page falls back to the filters.',
  ],
  how: [
    'Calls GET /audit (keyset paged, filters optional) and, once you type, GET /audit/search; both need AUDIT_READ.',
    'The log is append-only through the API: there is no write endpoint, only reads.',
    'Search is eventually consistent: an indexer copies audit rows into Meilisearch.',
    'If Meilisearch is unconfigured or unreachable the search answers 503 search.unavailable; the filters still work from Postgres.',
  ],
  decisions: [
    { label: 'D-031 Read API behind AUDIT_READ', path: 'docs/decisions.md' },
    { label: 'D-058 Audit search on Meilisearch', path: 'docs/decisions.md' },
    { label: 'D-030 Masking at write time', path: 'docs/decisions.md' },
    { label: 'ADR-010 Audit log', path: 'docs/adr/ADR-010-audit-log.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission (AUDIT_READ)',
      path: 'src/Fugue.Api/Controllers/AuditController.cs',
      code: `[HttpGet("search")]
[HasPermission(Permissions.AuditRead)]`,
      note: 'The list endpoint carries the same attribute.',
    },
    {
      status: '503',
      name: 'search unavailable',
      path: 'src/Fugue.Infrastructure/Search/MeilisearchAuditSearch.cs',
      code: `catch (Exception ex) when (ex is not OperationCanceledException)
{
    LogSearchFailed(ex);
    return Result.Fail<AuditSearchResultDto>(FugueErrors.Unavailable("search.unavailable", "Audit search is temporarily unavailable."));
}`,
      note: 'The page falls back to the filter list.',
    },
  ],
};
