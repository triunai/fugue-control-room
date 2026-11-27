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
  adrs: [
    { label: 'ADR-010 Rich append-only audit log', path: 'docs/adr/ADR-010-audit-log.md' },
  ],
  tests: [
    { layer: 'integration', path: 'tests/Fugue.Tests/Audit/AuditSearchTests.cs', name: 'Without_a_search_engine_configured_the_endpoint_answers_503_search_unavailable', asserts: 'With no search engine configured, /audit/search answers 503 with code search.unavailable.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Audit/AuditApiTests.cs', name: 'There_is_no_way_to_write_through_the_API', asserts: 'POST and DELETE on /audit answer 405: the log can only be read through the API.' },
    { layer: 'api', path: 'bruno/fugue/audit/10-search-self-service-is-forbidden.bru', name: 'Without AUDIT_READ the search is 403', asserts: 'A caller without AUDIT_READ gets 403 on the audit search.' },
    { layer: 'web', path: 'web/src/app/features/audit/audit-search.spec.ts', name: 'falls back to the filtered log with a one-line note on 503 search.unavailable', asserts: 'On 503 search.unavailable the page falls back to the filtered log with a one-line note.' },
    { layer: 'arch', path: 'tests/Fugue.ArchTests/FitnessTests.cs', name: 'Fitness_Persistence_every_entity_is_IAuditable_unless_append_only', asserts: 'Every entity is auditable unless on the append-only allowlist, and those must carry OccurredAt.' },
  ],
  decisions: [
    { label: 'D-031 Read API behind AUDIT_READ', path: 'docs/decisions.md' },
    { label: 'D-058 Audit search on Meilisearch', path: 'docs/decisions.md' },
    { label: 'D-030 Masking at write time', path: 'docs/decisions.md' },
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
