# Failure modes (known traps and the move that defuses each)

One entry per trap we have actually hit. Format: symptom, cause, defusing move. Add an entry the turn a new trap is found (wrap-session step 5). Architecture catches are also pinned as fitness functions (`tests/Fugue.ArchTests/FitnessTests.cs`, shrink-only baselines): the fitness test is the "pin" form for structural traps; behavioural traps are pinned by a regression test.

| # | Symptom | Cause | Defusing move |
|---|---|---|---|
| 1 | Error responses come back as `application/json`, not `application/problem+json` (hit twice: once on handler failures, once on framework 400s) | A controller-level `[Produces("application/json")]` overwrites the problem content type | No `[Produces]` on controllers. `ProblemResult` builds its `ObjectResult` at execution time; `InvalidModelStateResponseFactory` and `UseStatusCodePages` also go through it. Pinned by content-type assertions in the API tests |
| 2 | Tests and the API cannot reach the compose Postgres; auth or "database does not exist" errors from `localhost:5432` | A host Postgres on 5432 shadows the container for `localhost` | Compose publishes `${POSTGRES_PORT:-5432}`; local `.env` uses 5433 and the connection string `Port=` must match (runbook 03) |
| 3 | `cannot cast type xid to bigint` in Dapper SQL | Postgres `xmin` is `xid`, which has no direct cast to `bigint` | `xmin::text::bigint` in SQL; EF projections widen the `uint` in memory. Never create `xmin` in a migration |
| 4 | "The logger is already frozen" when several `WebApplicationFactory` hosts start in one test process | A reloadable Serilog bootstrap logger is a process-wide singleton | Use a plain (non-reloadable) bootstrap logger (ai-journal, S1 choices) |
| 5 | `Fugue.slnx` fails to load or restore | `.slnx` needs SDK 9.0.200 or newer; `global.json` pins 8.0.428 | Keep `Fugue.sln`. Do not migrate to `.slnx` on .NET 8 |
| 6 | Handlers in the test assembly are not dispatched; "no handler" at runtime | The `Mediator` source generator only sees handlers in assemblies it compiles; a test-assembly handler is invisible to the generator in the app | Use a fake sender (`FakeSender`) in unit tests; real handlers are exercised through the API host |
| 7 | Build fails on NETSDK1138 (net8 out of support) because warnings are errors | .NET 8 passed end of support on 2026-11-10 and `TreatWarningsAsErrors` is on | `CheckEolTargetFramework=false` in `Directory.Build.props`, deliberately; NuGet audit stays on (D-002). Do not "fix" by upgrading without an owner call |
| 8 | `bd init` rewrites `core.hooksPath`, can add a Dolt remote and COMMITS its own config to the current branch | beads installs its own git hooks and auto-commits `.beads/` files | Run `bd init --prefix fg --non-interactive --skip-agents --skip-hooks`; then check `git log -1`, `git config core.hooksPath` (must be empty), `git remote -v`; `git reset --mixed HEAD~1` if it committed; `.beads/` is gitignored; `npx lefthook install` (D-013) |
| 9 | A tool "succeeds" with exit code 0 but did nothing (Codex after a crash, a hook runner) | Exit codes are not evidence | Read the output. `codex-gate.mjs` reads the review text and records "failed" on error-looking output |
| 10 | A parallel agent's staged files ride along in someone else's commit | `git commit` takes the whole index | Scope commits by pathspec (`git commit -- <paths>`); one worktree per write lane |
| 11 | A brief states a fact that is already false | Contracts are written from memory | The verify-the-premises clause in `agent-task-contract`; reconcile against git, not the report |
| 12 | A rule only the prompt holds gets skipped | Prompts are advisory | Make it a gate (commit-msg, pre-push, PreToolUse guard, fitness test) |

Related: `AGENTS.md` (gates), `docs/runbooks/` (local setup), `docs/ai-journal.md` (how each was found).
