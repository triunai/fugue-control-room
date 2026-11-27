import { HoodNote } from '../hood/hood-note';

export const AUDIT_HOOD: HoodNote = {
  why: [
    'Every change and every refused command is recorded in the same transaction, with values masked before they are stored.',
    'Typing a query uses Meilisearch (typo-tolerant). If the engine is off the API answers 503 search.unavailable and the page falls back to the filters.',
  ],
  flow: [
    {
      layer: 'api',
      text: 'Any write (for example saving a customer) gets a correlation id first: an inbound X-Correlation-Id if valid, else a new one.',
      path: 'src/Fugue.Api/Correlation/CorrelationIdMiddleware.cs',
    },
    {
      layer: 'api',
      text: 'The controller action is a one-liner: ApiControllerBase dispatches the command through the mediator.',
      path: 'src/Fugue.Api/Controllers/ApiControllerBase.cs',
    },
    {
      layer: 'handler',
      text: 'AuditBehavior wraps every command: it opens the audit scope (action, actor, command name) and times it.',
      path: 'src/Fugue.Application/Common/Behaviors/AuditBehavior.cs',
    },
    {
      layer: 'audit',
      text: 'On success the interceptor adds one row per changed entity to the SAME SaveChanges: one transaction with the change.',
      path: 'src/Fugue.Infrastructure/Audit/AuditLogSaveChangesInterceptor.cs',
    },
    {
      layer: 'audit',
      text: 'On failure nothing changed, so one failure row (error code, duration) is written on its own connection and survives the rollback.',
      path: 'src/Fugue.Infrastructure/Audit/AuditTrail.cs',
    },
    {
      layer: 'db',
      text: 'audit_log is append-only: a trigger rejects UPDATE, DELETE and TRUNCATE for every role.',
      path: 'src/Fugue.Infrastructure/Persistence/Migrations/20261125162316_AddAuditLog.cs',
    },
    {
      layer: 'job',
      text: 'AuditIndexer, a background service, copies new rows in keyset batches after a checkpoint kept in the engine.',
      path: 'src/Fugue.Infrastructure/Search/AuditIndexer.cs',
    },
    {
      layer: 'search',
      text: 'GET /audit/search queries Meilisearch; GET /audit (this list) reads Postgres, keyset paged, newest first.',
      path: 'src/Fugue.Infrastructure/Search/MeilisearchAuditSearch.cs',
    },
    {
      layer: 'web',
      text: 'Open a row, then "Open The Score": everything its correlation id left behind (command, events, deliveries) on one axis.',
      path: 'web/src/app/features/audit/score-page.ts',
    },
  ],
  how: [
    'Calls GET /audit (keyset paged, filters optional) and, once you type, GET /audit/search; both need AUDIT_READ.',
    'The log is append-only through the API: there is no write endpoint, only reads, and the table trigger refuses changes anyway.',
    'Search is eventually consistent: the indexer copies rows into Meilisearch, so a brand-new row can take one interval to appear.',
    'If Meilisearch is unconfigured or unreachable the search answers 503 search.unavailable; the filters still work from Postgres.',
  ],
  adrs: [
    { label: 'ADR-010 Rich append-only audit log', path: 'docs/adr/ADR-010-audit-log.md' },
    { label: 'ADR-005 Trace, correlation and saga ids', path: 'docs/adr/ADR-005-trace-correlation-saga-ids.md' },
  ],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditCaptureTests.cs',
      name: 'A_save_that_fails_leaves_no_audit_row_because_the_audit_rows_ride_in_the_same_save',
      asserts: 'A save that fails leaves no audit row: the row rides in the same SaveChanges as the change.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditLogSchemaTests.cs',
      name: 'An_update_is_rejected_by_the_append_only_trigger',
      asserts:
        'An UPDATE on audit_log is refused by the append-only trigger (DELETE and TRUNCATE have their own tests).',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditSearchTests.cs',
      name: 'The_indexer_picks_up_new_rows_and_search_finds_them_by_entity_and_by_actor',
      asserts: 'The indexer copies new rows into Meilisearch and search finds them by entity and by actor.',
    },
    {
      layer: 'api',
      path: 'bruno/fugue/audit/02-filter-by-correlation.bru',
      name: 'Filter the audit log by that correlation id (one rich row)',
      asserts: 'Filtering by the correlation id of a create returns exactly its one rich audit row.',
    },
    {
      layer: 'web',
      path: 'web/src/app/features/audit/audit-search.spec.ts',
      name: 'falls back to the filtered log with a one-line note on 503 search.unavailable',
      asserts: 'On 503 search.unavailable the page falls back to the filtered log with a one-line note.',
    },
  ],
  decisions: [
    { label: 'D-028 Capture in the same transaction', path: 'docs/decisions.md' },
    { label: 'D-027 Table shape, append-only trigger, keyset indexes', path: 'docs/decisions.md' },
    { label: 'D-030 Masking at write time', path: 'docs/decisions.md' },
    { label: 'D-058 Audit search on Meilisearch, fed by an indexer', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission (AUDIT_READ)',
      path: 'src/Fugue.Api/Controllers/AuditController.cs',
      code: `[HttpGet("search")]
[HasPermission(Permissions.AuditRead)]`,
      note: 'The list and the trace endpoints carry the same attribute.',
    },
    {
      status: 'tx',
      name: 'same transaction',
      path: 'src/Fugue.Infrastructure/Audit/AuditLogSaveChangesInterceptor.cs',
      code: `public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
{
    Capture(eventData.Context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
}`,
      note: 'The audit rows are added before the save, so the change and its rows commit or fail together.',
    },
    {
      status: 'db',
      name: 'append-only trigger',
      path: 'src/Fugue.Infrastructure/Persistence/Migrations/20261125162316_AddAuditLog.cs',
      code: `CREATE FUNCTION audit_log_reject_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_log is append-only: % is not allowed', TG_OP USING ERRCODE = 'restrict_violation';
END;`,
      note: 'Fires BEFORE UPDATE OR DELETE per row and BEFORE TRUNCATE per statement.',
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
