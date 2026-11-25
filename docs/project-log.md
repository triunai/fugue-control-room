# Fugue — project log (journal)

Latest first. Append-only: never edit an old entry. One entry per session or per notable event. Older `(previous)` RESUME blocks from `docs/hot-state.md` are copied into the entry for their day.

Heading form: `## <YYYY-MM-DD> — <title>`. Entry key = `<date>#<n>`, where n is the order among that date's headings, top to bottom (so a second entry the same day is `<date>#2`).
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)`.
Then: what was done · what was decided (cite D-NNN) · what is open (cite #NNN).

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

## 2026-11-25 — S2: Postgres, EF Core + Dapper, audit, typed ids, Customers
WS: WS-04
Fixed framework 400s to `application/problem+json`; added docker-compose Postgres, `FugueDbContext` and the first migration, `IUnitOfWork`/`SqlExecutor`, audit stamping on both write paths, `Uuid7` and `CustomerId`, the Customers vertical slice, a dev seeder, Testcontainers integration tests, a Bruno suite, runbooks 03 and 04 and ADR-006.
Decided: D-004.
Next: S3 identity. Owner action pending: implement `MapStatus`.
Superseded RESUME block (2026-11-25, S0 and S1 done): S2 persistence was next.

## 2026-11-25 — S1: validation, exceptions, logging, correlation
WS: WS-03
Added the validation and logging behaviors, global exception handler, Serilog, correlation and saga ids (ADR-005), a ping feature that exercises the real pipeline, runbook 02 and tests.
Decided: D-003.
Next: S2 persistence per the roadmap. Owner action pending: implement `MapStatus`.
Superseded RESUME block (2026-11-25, S0 done): net8, FluentResults, Application layer, neutral docs finished; S1 was next.

## 2026-11-25 — S0: net8, FluentResults, Application layer, neutral docs
WS: WS-02
Retargeted to .NET 8 (`Fugue.sln`, SDK pinned 8.0.428), added `Fugue.Application`, replaced the hand-rolled Result with FluentResults + `FugueError`, made `Send*` return `ActionResult<T>`, neutralised the docs, wrote ADR-003/004, the roadmap, runbook 01 and the AI journal.
Decided: D-002.
Next: WS-03 (S1: validation, exception handler, Serilog, correlation and saga IDs).
Superseded RESUME block (2026-11-11): spine just installed, nothing in flight.

## 2026-11-11 — Doc spine installed
WS: WS-01
Installed the router, the spine files, the git hooks and the spine scripts. The link checker runs clean.
Decided: D-001.
Next: add the first real thread to `docs/workstreams.md`.
