# Fugue — project log (journal)

Latest first. Append-only: never edit an old entry. One entry per session or per notable event. Older `(previous)` RESUME blocks from `docs/hot-state.md` are copied into the entry for their day.

Heading form: `## <YYYY-MM-DD> — <title>`. Entry key = `<date>#<n>`, where n is the order among that date's headings, top to bottom (so a second entry the same day is `<date>#2`).
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)`.
Then: what was done · what was decided (cite D-NNN) · what is open (cite #NNN).

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
