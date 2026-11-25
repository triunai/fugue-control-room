# Changelog

Tracks notable changes across migrations, API contracts, and project documentation. Append new entries at the top with the date in ISO-8601 format.

> **ADR links.** An entry that reflects a real architectural decision cites it as `(ADR-NNN)`; the ADR lives in `docs/adr/`.

## 2026-11-25 — Closed W-009: MapStatus: ErrorType to HTTP status
WS: WS-09
Closed: W-009 (WS-09) MapStatus: ErrorType to HTTP status
Outcome: MapStatus implemented as an exhaustive ErrorType-to-status switch (owner-approved). New ErrorType.Unavailable maps to 503; GlobalExceptionHandler answers 503 + Retry-After for transient DbException/TimeoutException; SendNoContent<T> returns 204 + ETag. The 16 red-by-design tests are green; suite 82/82.
Commits:
- 4e8ee4e feat(api): map ErrorType to HTTP status, 503 for transient db failures
Decisions: D-009
Follow-ups: none

## 2026-11-25 — Closed W-008: H1 port the AI engineering harness
WS: WS-08
Closed: W-008 (WS-08) H1 port the AI engineering harness
Outcome: Ported the AI engineering harness from my two other repos, public-safe: local beads, W-/D-/ADR- ids, refs-trailer and fix-has-test commit gates, a PreToolUse safety guard, an advisory seam-triggered Codex gate, four project skills, fitness functions with shrink-only baselines, doc-lifecycle scripts (close-workstream, commit-link-check, prune-check, adr-check, archive-project-log), failure modes, branch ledger, handoff and the how-we-use-ai narrative. The Codex invocation is confirmed from --help only.
Commits:
- a3a936a feat(spine): beads-local tracking, refs and fix-has-test commit gates
- c4f9dc1 feat(spine): safe-command guard, advisory codex gate, project skills
- ad58e29 test(arch): fitness functions for critical modules with shrink-only baselines
- b26f99c docs(spine): control-plane gaps and how-we-use-ai narrative
Decisions: D-012, D-013, D-014, D-015, ADR-007
Follow-ups: First real Codex seam run (verify invocation); S3 identity (W-005); MapStatus (W-009, owner)

## 2026-11-25 — Closed W-004: S2 persistence: Postgres, EF Core + Dapper, audit, typed ids, Customers slice
WS: WS-04
Closed: W-004 (WS-04) S2 persistence: Postgres, EF Core + Dapper, audit, typed ids, Customers slice
Outcome: Compose Postgres, EF Core + Dapper over one unit of work, audit stamping on both paths, Uuid7 typed ids, Customers vertical slice, dev seeder, Testcontainers tests, Bruno suite and runbooks 03-04. 16 tests stay red until MapStatus exists.
Commits:
- b30ca07 feat(customers): postgres compose, ef core + dapper unit of work, audit stamping, typed ids, customers slice
- 8ed061b feat(api): bruno regression suite, seed fix, runbooks 03-04 and S2 spine docs
Decisions: D-004, ADR-006
Follow-ups: MapStatus is the owner's (W-009); S3 identity next (W-005)

## 2026-11-25 — Closed W-003: S1 validation, exception handler, Serilog, correlation and saga IDs
WS: WS-03
Closed: W-003 (WS-03) S1 validation, exception handler, Serilog, correlation and saga IDs
Outcome: Validation and logging behaviors, global exception handler, Serilog, correlation and saga ids (ADR-005), a ping feature that exercises the real pipeline, runbook 02 and tests; framework 400s fixed to problem+json.
Commits:
- 914e2ed feat(observability): validation and logging behaviors, global exception handler, correlation and saga IDs
- 7430e44 fix(api): problem+json for framework 400s and empty status-code responses
Decisions: D-003, ADR-005
Follow-ups: S4 webhooks must carry the correlation headers (W-006)

## 2026-11-25 — Closed W-002: S0 retarget .NET 8, FluentResults, Application layer, neutral docs
WS: WS-02
Closed: W-002 (WS-02) S0 retarget .NET 8, FluentResults, Application layer, neutral docs
Outcome: Retargeted to .NET 8 (Fugue.sln, SDK pinned 8.0.428); added Fugue.Application; replaced the hand-rolled Result with FluentResults and FugueError; Send* returns ActionResult<T>; neutral docs, ADR-003/004, roadmap, runbook 01 and the AI journal.
Commits:
- 5382163 refactor(arch): retarget net8, adopt FluentResults, add Application layer, neutral docs
Decisions: D-002, D-009, D-010, D-011, ADR-003, ADR-004
Follow-ups: none

## 2026-11-11 — Closed W-001: Bootstrap the doc spine
WS: WS-01
Closed: W-001 (WS-01) Bootstrap the doc spine
Outcome: Router, spine files, git hooks and spine scripts installed; the link checker runs clean.
Commits:
- 400fa57 chore(spine): bootstrap repo spine, solution skeleton and ApiControllerBase
Decisions: D-001, D-005, D-006, D-007, D-008, ADR-001, ADR-002
Follow-ups: none

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
