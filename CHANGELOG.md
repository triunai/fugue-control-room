# Changelog

Tracks notable changes across migrations, API contracts, and project documentation. Append new entries at the top with the date in ISO-8601 format.

> **ADR links.** An entry that reflects a real architectural decision cites it as `(ADR-NNN)`; the ADR lives in `docs/adr/`.

## 2026-11-25 — S2 — persistence, Customers slice, Bruno

### Added
- `docker-compose.yml` (Postgres 17, named volume, healthcheck), `.env.example`, `dotnet-ef` local tool.
- EF Core 8 + Npgsql + snake_case naming; `FugueDbContext`; migration `InitialCustomers` (catalog row 1).
- `IUnitOfWork`/`EfUnitOfWork`, Dapper `SqlExecutor` on EF's connection and transaction (ADR-006).
- `IAuditable`, `IAuditStamp`, `AuditableSaveChangesInterceptor`; `Uuid7`, `IStronglyTypedId`, `CustomerId` with EF/Dapper/JSON/MVC wiring.
- Customers slice: `/api/v1/customers` (create, get, list with keyset paging, update with `If-Match`, suspend, reactivate, delete); development seeder (`dotnet run -- --seed`).
- Testcontainers integration tests; Bruno collection `bruno/fugue` and `npm run api:test`; runbooks 03 and 04.

### Fixed
- Framework-generated 400s (model binding) and empty 404/405/415 responses are `application/problem+json`; the controller-level `[Produces("application/json")]` is gone.

### Changed
- Route URLs are lower-case.

## 2026-11-25 — S1 — validation, exceptions, logging, correlation and saga IDs

### Added
- `ValidationBehavior` and `LoggingBehavior` (Mediator pipeline), `AddApplication()` validator registration, `ICorrelationContext`.
- `GlobalExceptionHandler` (RFC 9457 500, no detail outside Development, 499 for client aborts); `AddProblemDetails` customization adds `traceId` and `correlationId`.
- `CorrelationIdMiddleware` (`X-Correlation-Id`, `X-Saga-Id`, Activity tags/baggage, Serilog LogContext); Serilog with readable console in Development and compact JSON elsewhere (ADR-005).
- `Diagnostics` ping feature and controller; runbook 02.
- `ApiControllerBase.Send*` overloads for `ICommand` and `IQuery`.

## 2026-11-25 — S0 — retarget .NET 8, FluentResults, Application layer, neutral docs

### Changed
- Target framework .NET 8 (`global.json` SDK 8.0.428); solution is now `Fugue.sln`. `CheckEolTargetFramework=false` is deliberate (ADR-004 context in `Directory.Build.props`).
- Hand-rolled `Result`/`Result<T>`/`Error` replaced by FluentResults + `FugueError`/`FugueErrors`; `ErrorType` kept (ADR-004). `ApiControllerBase.Send*` now return `ActionResult<T>`.
- Docs are domain-neutral. ADR-001 superseded by ADR-003 (single-tenant default, tenancy toggle).

### Added
- `Fugue.Application` project with `Features/` and `Common/Behaviors/`.
- ADR-003, ADR-004, `docs/plans/roadmap.md`, `docs/runbooks/`, `docs/ai-journal.md`.

## 2026-11-11 — Phase 1a — solution skeleton + ApiControllerBase

### Added
- Solution `Fugue.slnx` with `Fugue.Api`, `Fugue.Domain`, `Fugue.Infrastructure`, `Fugue.Tests`, `Fugue.ArchTests`; `global.json`, `Directory.Build.props`, central package management (`Directory.Packages.props`).
- Domain result types (`Result`, `Result<T>`, `Error`, `ErrorType`, `PagedResult<T>`, `IVersioned`) and `ICurrentUser` (ADR-002).
- `ApiControllerBase`: result-to-ProblemDetails (RFC 9457), ETag / `If-Match`, keyset paging headers, `Idempotency-Key` accessor, lazy `Sender` / `CurrentUser`; `ClaimsCurrentUser` implementation.
- Tests over real HTTP using a probe controller and a fake `ISender`; architecture tests for layering and controller rules.

### Notes
- `ApiControllerBase.MapStatus` is deliberately unimplemented (owner decision); `MapStatusTests` and the two error-shape HTTP tests are red until it is written.

## 2026-11-11 — Phase 0 — repo spine

### Added
- Doc spine (`hygiene.md`, `docs/` board, decisions, backlog, log, templates) and the `scripts/spine/` gates.
- Git hooks orchestrated by lefthook; commitlint with Fugue scopes; test-integrity check for C#/xUnit.
- `CLAUDE.md`, `AGENTS.md`, PR template, migration catalog, ADR-001 (shared DB + `tenant_id`), ADR-002 (MVC controllers + `ApiControllerBase`).
