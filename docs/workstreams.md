# Fugue — workstreams (the board)

One block per thread. Add the block at task START. Update at wrap. Prune or mark done when finished.

Heading form (exactly three `#`, one space, em dash with spaces, date in parentheses): `### WS-NN — <title> — <STATUS> (<YYYY-MM-DD>)`. WS numbers use two digits minimum (WS-01). A heading at any other level hides the thread from the link checker.
Body lines: `Status:` where it stands · `Resume:` where you stopped and the literal next action · `Refs:` files, decisions, log dates.
A heading containing the done glyph counts as done. A done thread must be claimed by at least one `docs/project-log.md` entry (`WS: WS-NN`).

Glyph legend:
- `🟢 ACTIVE` — being worked on now
- `⏸ PARKED` — frozen on purpose; `Resume:` says how to restart
- `⛔ BLOCKED` — waiting on something outside the thread; `Status:` names it
- `✅ DONE` — finished and verified

### WS-01 — Bootstrap the doc spine — ✅ DONE (2026-11-11)
Status: spine files, router and gates installed.
Resume: none. Start the next thread with "new task: …".
Refs: `hygiene.md`, `docs/conventions/doc-spine.md`, D-001

### WS-02 — S0 retarget .NET 8, FluentResults, Application layer, neutral docs — ✅ DONE (2026-11-25)
Status: net8 + `Fugue.sln`, `Fugue.Application`, FluentResults/`FugueError`, neutral docs, ADR-003/004, roadmap, runbook 01, AI journal. Expected red: `MapStatus` tests (owner's to write).
Resume: none. Next thread is WS-03.
Refs: `docs/plans/roadmap.md`, ADR-003, ADR-004, D-002

### WS-03 — S1 validation, exception handler, Serilog, correlation and saga IDs — ✅ DONE (2026-11-25)
Status: behaviors, exception handler, Serilog, correlation middleware, ADR-005, runbook 02, tests. Expected red: `MapStatus`-dependent tests only.
Resume: none. Next is S2 (persistence) per `docs/plans/roadmap.md`; open it with "new task: …".
Refs: ADR-005, D-003, `docs/runbooks/02-tracing-a-request.md`

### WS-04 — S2 persistence: Postgres, EF Core + Dapper, audit, typed ids, Customers slice — ✅ DONE (2026-11-25)
Status: compose Postgres, `FugueDbContext` + `InitialCustomers` migration, `IUnitOfWork`/`SqlExecutor`, audit stamping on both paths, `Uuid7` + `CustomerId`, Customers slice, dev seeder, Testcontainers tests, Bruno suite, runbooks 03 and 04, ADR-006. Expected red: tests needing `MapStatus` (see hot-state).
Resume: none. Next is S3 (identity) per `docs/plans/roadmap.md`; open it with "new task: …".
Refs: ADR-006, D-004, `docs/runbooks/03-local-database.md`, `docs/runbooks/04-customers-api.md`, `docs/migrations/migration-catalog.md`
