# Changelog

Tracks notable changes across migrations, API contracts, and project documentation. Append new entries at the top with the date in ISO-8601 format.

> **ADR links.** An entry that reflects a real architectural decision cites it as `(ADR-NNN)`; the ADR lives in `docs/adr/`.

## 2026-11-26 — Closed W-006: S4 transactional outbox and signed webhooks
WS: WS-06
Closed: W-006 (WS-06) S4 transactional outbox and signed webhooks
Outcome: Transactional outbox and signed webhooks shipped (ADR-011): outbox rows in the same transaction, a two-stage leased dispatcher with the outbox as the single retry authority, SSRF-guarded sender, encrypted subscription secrets, a Development-only sink, runbook 07 and Bruno. The Codex review (1 HIGH, 4 MED) is fixed and pinned. dotnet test 363 green; Bruno 47 requests, 64 tests green.
Commits:
- aa51ca4 feat(persistence): outbox and webhook tables with the AddOutboxWebhooks migration
- 893a345 feat(persistence): capture domain events as outbox rows in the same transaction
- 01e0305 feat(api): webhook subscriptions with encrypted secrets and an SSRF guard
- dfbec7f feat(api): signed webhook dispatcher, guarded sender, deliveries API and dev sink
- ad9df38 docs(api): webhooks runbook 07, Bruno webhooks folder and S4 journal entries
- 596269e fix(api): close the Codex S4 review findings on delivery fencing, leases, capture and sink
- f34d0b4 test(api): list the WEBHOOKS module in the Bruno modules check
Decisions: ADR-011, D-041, D-038, D-037, D-039, D-040
Follow-ups: Outbox retention and archival (backlog #001, deferred); a broker adapter behind the publisher seam if ever needed

## 2026-11-26 — Closed W-015: Codex W-013 findings: no-op override PUT skips the xmin check, email ceiling charged late
WS: WS-15
Closed: W-015 (WS-15) Codex W-013 findings: no-op override PUT skips the xmin check, email ceiling charged late
Outcome: Both Codex W-013 MED findings fixed with regression tests shown RED first: a no-op override PUT now runs the version-guarded UPDATE (412 on a racing change), and the per-email login ceiling reserves a slot before verification and refunds on success. dotnet test 363 green.
Commits:
- 224b5b2 fix(identity): version-guard no-op override PUTs and reserve the email ceiling up front
Decisions: D-035, D-036
Follow-ups: none

## 2026-11-26 — Closed W-013: S3 review follow-ups
WS: WS-13
Closed: W-013 (WS-13) S3 review follow-ups
Outcome: Email uniqueness is per owner (migration CustomerEmailPerOwner) and login is limited per (IP, email) plus a loose per-email ceiling, both owner calls. Override changes need If-Match (migration OverrideConcurrencyToken); group self-target, 23505 races, tenant is_active, fail-closed module config, access_audit clipping and HMAC subkey fixed and pinned; module PATCH audit was already covered by W-012. Recorded not fixed: raw user ids in createdBy, anonymous access_audit growth. Suite 260 green, Bruno 40/56.
Commits:
- 863d4b8 fix(persistence): make email unique per owner, closing the create-path oracle
- 977821f fix(api): key the login limiter on (IP, email) plus a loose per-email ceiling
- feef681 fix(identity): require If-Match to change or delete an existing permission override
- 3766cbc fix(identity): close the remaining S3 review findings on admin and audit edges
Decisions: D-034, D-035, D-036, ADR-008
Follow-ups: Cross-family review of the W-013 auth and migration changes (not yet done).

## 2026-11-26 — Closed W-012: Rich append-only audit log
WS: WS-12
Closed: W-012 (WS-12) Rich append-only audit log
Outcome: Rich append-only audit_log: who, what, field-level old/new, outcome and error code, request and trace ids, in the SAME transaction as the change (EF SaveChanges interceptor plus IAuditTrail for Dapper writes), one failure row per failed command (AuditBehavior), write-time masking and size bound, trigger blocks UPDATE/DELETE/TRUNCATE, GET /api/v1/audit behind AUDIT_READ (admin) with keyset paging. Codex review: 1 HIGH + 3 MED, all fixed and pinned (fixes not re-reviewed). Suite 222 green, Bruno 40 requests / 56 tests.
Commits:
- 67c7d95 feat(persistence): audit_log table, append-only trigger and AUDIT_READ seed
- 8d8f77a feat(persistence): capture audit rows from EF and Dapper writes and failed commands
- beba6dd feat(api): audit log query endpoint with keyset paging behind AUDIT_READ
- f38e3bf docs(audit): ADR-010, decisions, runbook 06, failure modes and deferred items
- c35a022 fix(persistence): audit forced writes, detach failed-save rows, independent failure row
Decisions: D-027, D-028, D-029, D-030, D-031, D-032, D-033, ADR-010
Follow-ups: W-013 gains the missing OverrideAdmin concurrency check; deferred (docs/archive/deferred.md): audit retention and partitioning, non-owner DB role, tenant filter on audit reads, auditing of reads

## 2026-11-25 — Closed W-014: Commit traceability index
WS: WS-14
Closed: W-014 (WS-14) Commit traceability index
Outcome: Refs: now takes several ids per trailer and resolves closed W via log, archive or CHANGELOG. trace-index.mjs generates docs/traceability.md (commit <-> W <-> ADR <-> D) from git log and the committed docs; npm run trace, a pre-push advisory stale check, and wrap-session wiring added. close-workstream now records D/ADR ids from a W's commits. test:scripts 83 green.
Commits:
- 5704fc6 docs(spine): open W-014 commit traceability index on the board
- e0b8f3c feat(spine): multi-id Refs gate and a generated commit traceability index
Decisions: D-013, ADR-007
Follow-ups: none

## 2026-11-25 — Closed W-005: S3 identity: JWT auth, lean UAM v3, module management
WS: WS-05
Closed: W-005 (WS-05) S3 identity: JWT auth, lean UAM v3, module management
Outcome: JWT HS256 login with refresh rotation and family reuse detection, per-request uncached effective permissions (roles, groups, overrides; deny > allow > role), ownership scoping with 404, module management (config kill switch + registry), tenants, groups, grant_audit and access_audit, 204 mutations. Codex rejected the first plan (10 findings, all accepted); Codex and a Sonnet reviewer reviewed the build (1 HIGH fixed and pinned). Suite 171 green, Bruno 32 requests / 45 assertions.
Commits:
- ae7fbb8 docs(spine): close W-009 into the project log and CHANGELOG
- b4b0dea docs(plans): S3 identity plan
- f6363b8 docs(plans): revise S3 per cross-family review
- 580100e feat(identity): UAM v3 schema, seeded catalog and base-entity contract
- e1b54f3 feat(auth): JWT login, per-request permissions, ownership scoping, 204 mutations
- 142158a feat(identity): module management with a config kill switch and a runtime registry
- aae8b27 feat(identity): groups with audited membership changes, and a tenant column
- 31c91c7 feat(identity): refresh tokens with rotation, family reuse detection and logout
- b7ac795 feat(identity): access_audit events for login, refresh, 403 and disabled modules
- 98f1491 feat(identity): permission overrides with expiry, audited in the same transaction
- 8a24bd7 docs(identity): ADR-008 auth model, ADR-009 module management, failure modes
- 4e01024 fix(identity): family revocation lock, smuggled perm claims, override and owner edge cases
Decisions: D-016..D-026, ADR-008, ADR-009
Follow-ups: W-013 S3 review follow-ups (create email oracle, limiter DoS, audit hardening)

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
