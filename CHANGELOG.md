# Changelog

Tracks notable changes across migrations, API contracts, and project documentation. Append new entries at the top with the date in ISO-8601 format.

> **ADR links.** An entry that reflects a real architectural decision cites it as `(ADR-NNN)`; the ADR lives in `docs/adr/`.

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
