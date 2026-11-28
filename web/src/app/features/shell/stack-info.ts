/** What Fugue is built on, as shown in the "Stack" dialog. Versions are the ones pinned in package.json,
 *  Directory.Packages.props and docker-compose.yml; stack-info.spec.ts fails when the Angular major drifts. */
export interface StackItem {
  readonly name: string;
  readonly version: string;
  readonly note?: string;
}
export interface StackGroup {
  readonly title: string;
  readonly items: readonly StackItem[];
}

export const STACK: readonly StackGroup[] = [
  {
    title: 'Web',
    items: [
      { name: 'Angular', version: '22', note: 'standalone components, signals' },
      { name: 'Angular CDK', version: '22' },
      { name: 'TanStack Query', version: '5', note: 'server state' },
      { name: 'NgRx Signals', version: '22', note: 'local stores' },
      { name: 'Univer', version: '1.6', note: 'the spreadsheet grid' },
      { name: 'FullCalendar', version: '7', note: 'SLA calendar' },
      { name: 'driver.js', version: '1.14', note: 'the guided tour' },
      { name: 'temporal-polyfill', version: '1.0', note: 'dates and times' },
      { name: 'exceljs', version: '4.4', note: 'XLSX export' },
      { name: 'Tailwind CSS', version: '4' },
      { name: 'Vitest', version: '5', note: 'unit tests' },
      { name: 'Playwright', version: '1.65', note: 'end-to-end tests' },
    ],
  },
  {
    title: 'API',
    items: [
      { name: '.NET / ASP.NET Core', version: '8', note: 'MVC controllers' },
      { name: 'EF Core + Npgsql', version: '8.0', note: 'reads and xmin concurrency; Npgsql provider 8.0.11' },
      { name: 'Dapper', version: '2.1', note: 'writes: inserts and upserts from SQL files (ADR-006)' },
      { name: 'Mediator (source generated)', version: '3.0', note: 'commands and queries' },
      { name: 'FluentValidation', version: '12.2' },
      { name: 'FluentResults', version: '4.0', note: 'Result + ErrorType, mapped to problem+json' },
      { name: 'Mapping', version: 'none', note: 'no mapper library: hand-written mapping to DTO records' },
      { name: 'JwtBearer + Identity.Core', version: '8', note: 'bearer tokens; password hashing' },
      { name: 'DataProtection', version: '8', note: 'webhook secrets encrypted at rest' },
      {
        name: 'Http.Resilience (Polly v8)',
        version: '8.10',
        note: 'webhook delivery: per-receiver circuit breaker, attempt and total timeouts, rate limiter; HTTP retry limited to one and off for POST, because the outbox is the retry authority',
      },
      { name: 'HybridCache', version: '9.10', note: 'L1 only, GET /me' },
      { name: 'Serilog', version: '9.0', note: 'structured logs' },
      { name: 'Swashbuckle', version: '6.9', note: 'OpenAPI; the typed TypeScript client is generated from it' },
      { name: 'Meilisearch client', version: '0.20', note: 'keyword full-text search; server v1.12' },
      {
        name: 'Confluent.Kafka',
        version: '2.16',
        note: 'optional adapter, OFF: claim events go out through the outbox as signed webhooks',
      },
    ],
  },
  {
    title: 'Built in-house',
    items: [
      {
        name: 'UAM / RBAC',
        version: 'custom',
        note: 'permission codes, roles, groups, time-boxed overrides and module switches; [HasPermission] on endpoints; access re-read per request',
      },
      { name: 'Audit log', version: 'custom', note: 'append-only (a database trigger rejects changes); The Score traces one request' },
      { name: 'Outbox + webhooks', version: 'custom', note: 'transactional outbox, HMAC-signed deliveries' },
      { name: 'ETag / If-Match', version: 'custom', note: 'optimistic concurrency on edits' },
      { name: 'Idempotency-Key', version: 'custom', note: 'a repeated write replays the first answer' },
      { name: 'Rate limiting', version: 'custom', note: 'sign-in and general limiters' },
    ],
  },
  {
    title: 'Data',
    items: [
      { name: 'PostgreSQL', version: '17', note: 'postgres:17-alpine in docker-compose.yml' },
      {
        name: 'Entity ids',
        version: 'UUID v7',
        note: 'time-ordered GUIDs, generated in the API (Uuid7.NewGuid), not by the database',
      },
    ],
  },
  {
    title: 'Quality and delivery',
    items: [
      { name: 'xUnit + Testcontainers', version: '2.9 / 4.17', note: 'integration tests on a real PostgreSQL 17' },
      { name: 'NetArchTest', version: '1.3', note: 'architecture rules as tests' },
      { name: 'Stryker', version: 'none', note: 'mutation testing (tests/Fugue.MutationTests)' },
      { name: 'PICT', version: 'none', note: 'pairwise test cases (tests/pict)' },
      { name: 'Bruno', version: 'none', note: 'API contract requests, run in CI' },
      { name: 'k6', version: 'none', note: 'load and stampede scenarios (load/, perf/k6)' },
      { name: 'GitHub Actions', version: 'CI', note: 'four jobs: backend, scripts, web, api-contract' },
      { name: 'Lefthook', version: 'none', note: 'commit gates: Refs trailer, fix-has-test, secrets-scan, test-integrity' },
    ],
  },
];
