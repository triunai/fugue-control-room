import { HoodNote } from '../hood/hood-note';

export const SCORE_HOOD: HoodNote = {
  why: [
    'One correlation id ties a request to everything it caused: the audited command, its outbox events and each webhook delivery.',
    'The page draws those real rows on one time axis, so a slow receiver, a retry or a dead letter is visible at a glance.',
  ],
  flow: [
    {
      layer: 'web',
      text: 'Opened from an audit row ("Open The Score"); the route carries the correlation id: GET /audit/trace/{correlationId}.',
      path: 'web/src/app/features/audit/score-page.ts',
    },
    {
      layer: 'api',
      text: 'AuditController.Trace is a one-liner behind AUDIT_READ; it dispatches GetAuditTraceQuery.',
      path: 'src/Fugue.Api/Controllers/AuditController.cs',
    },
    {
      layer: 'handler',
      text: 'The validator accepts only the correlation id format [A-Za-z0-9-_]; anything else is a 400 before any query runs.',
      path: 'src/Fugue.Application/Features/Audit/Queries/GetAuditTrace.cs',
    },
    {
      layer: 'db',
      text: 'One raw statement through EF: a UNION over audit_log, outbox_messages and their webhook_deliveries, by time, capped at 500.',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/audit/trace.sql',
    },
    {
      layer: 'handler',
      text: 'Times become ms after the first row; the trace is failed (failed command or dead letter), pending, or complete.',
      path: 'src/Fugue.Application/Features/Audit/Queries/GetAuditTrace.cs',
    },
    {
      layer: 'web',
      text: 'score-layout groups a request's per-entity audit rows into one step and lanes it with its events and deliveries.',
      path: 'web/src/app/features/audit/score-layout.ts',
    },
  ],
  how: [
    'Calls GET /audit/trace/{correlationId}, needing AUDIT_READ; a read through EF only (ADR-006), no Dapper.',
    'Command and event rows are written by the request (a success row in its transaction); deliveries are added later by the dispatcher.',
    'An id with no rows is 404 audit.trace_not_found; a malformed id is a 400 validation problem.',
  ],
  adrs: [
    { label: 'ADR-005 Trace, correlation and saga ids', path: 'docs/adr/ADR-005-trace-correlation-saga-ids.md' },
    { label: 'ADR-010 Rich append-only audit log', path: 'docs/adr/ADR-010-audit-log.md' },
    { label: 'ADR-011 Outbox and signed webhooks', path: 'docs/adr/ADR-011-outbox-and-signed-webhooks.md' },
  ],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditTraceTests.cs',
      name: 'A_reserve_command_its_event_and_its_webhook_delivery_come_back_in_order_under_one_correlation_id',
      asserts: 'A reserve command, its event and its webhook delivery come back in order under one correlation id.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditTraceTests.cs',
      name: 'A_failed_command_is_a_failed_trace_with_its_error_code_and_duration',
      asserts: 'A refused command makes a failed trace that carries its error code and duration.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditTraceTests.cs',
      name: 'An_unknown_id_is_404_and_a_malformed_id_is_400',
      asserts: 'An id nothing was recorded under is 404; a malformed id is 400.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditTraceTests.cs',
      name: 'Without_AUDIT_READ_the_trace_is_401_anonymous_and_403_for_staff_and_self_service',
      asserts: 'Without AUDIT_READ the trace is 401 anonymous and 403 for staff and self-service.',
    },
    {
      layer: 'web',
      path: 'web/src/app/features/audit/score-layout.spec.ts',
      name: 'puts each voice in its lane, a command in both the API and the Handler/Audit lane',
      asserts: 'Each voice lands in its lane; a command shows in both the API and the Handler / Audit lane.',
    },
  ],
  decisions: [
    { label: 'D-064 Submission-day work (the trace endpoint cites it)', path: 'docs/decisions.md' },
    { label: 'D-003 Three-layer correlation', path: 'docs/decisions.md' },
    { label: 'D-039 Dispatcher: leases, SKIP LOCKED, outbox is the retry authority', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission (AUDIT_READ)',
      path: 'src/Fugue.Api/Controllers/AuditController.cs',
      code: `[HttpGet("trace/{correlationId}")]
[HasPermission(Permissions.AuditRead)]`,
    },
    {
      status: '400',
      name: 'malformed id',
      path: 'src/Fugue.Application/Features/Audit/Queries/GetAuditTrace.cs',
      code: `RuleFor(x => x.CorrelationId)
    .Must(CorrelationIdFormat.IsValid)`,
      note: 'The same format check the correlation middleware applies to an inbound X-Correlation-Id.',
    },
    {
      status: '404',
      name: 'trace not found',
      path: 'src/Fugue.Application/Features/Audit/Queries/GetAuditTrace.cs',
      code: `if (rows.Count == 0)
{
    return Result.Fail<AuditTraceDto>(FugueErrors.NotFound("audit.trace_not_found", $"Nothing was recorded under correlation id {query.CorrelationId}."));
}`,
    },
    {
      status: 'rule',
      name: 'at most 500 rows',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/audit/trace.sql',
      code: `ORDER BY at, rank, id
LIMIT 500`,
      note: 'Ordered by time, then command before event before delivery.',
    },
  ],
};
