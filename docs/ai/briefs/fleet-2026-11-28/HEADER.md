# Fugue lane contract — shared header (read fully before your lane file)

You are one builder lane in a parallel fleet working on Fugue, a .NET 8 + Angular claims platform that is submitted as a
technical-interview take-home at 15:00 +08 today. Repo: `~/Repos/personal-work/fugue`. Feature cut-off 13:00 +08.
Finished and verified beats new. The owner is asleep: never ask questions, decide, and record the decision in your report.

## Roles
- The DISPATCHER (the session that spawned you) wrote this contract and assigned your files.
- The INTEGRATOR is the agent `overnight-lead`. It alone merges to `main`, rebuilds the `fugue-demo` stack on :8080,
  pushes, and ticks `docs/plans/overnight-2026-11-28.md`. You never merge, never push, never touch `main`.
- Other lanes are building in parallel in sibling worktrees. Your lane file lists who owns what. Stay inside your files.

## Environment (every shell)
```
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.orbstack/bin:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock
```
Work only in your worktree `~/Repos/personal-work/fugue/.worktrees/<lane>` on branch `<lane>`. `web/node_modules` is a
symlink to the main checkout's: never run `npm install`/`npm ci` (it would rewrite the shared folder). If you truly need a
new package, stop and report instead.

## Rules (hard)
1. Before acting on any factual claim in your lane file, verify it against the repository. If a claim is false, say so with
   `file:line` and proceed on the truth.
2. Read the house conventions before writing code: `CLAUDE.md`, `docs/ARCHITECTURE.md`, and the files next to the ones you
   change. Match existing patterns (FluentResults + ErrorType, ApiControllerBase, Mediator handlers, SQL in
   `src/Fugue.Infrastructure/Persistence/Sql/**` with caller headers, audit + outbox in the same transaction, ETag/If-Match,
   EF reads / Dapper writes per ADR-006, web: standalone components, signals, TanStack Query, tokens from `web/src/styles`).
3. Never: force push, `--no-verify`, `npm install`, edit files outside your allowed list, touch the doc spine
   (`docs/hot-state.md`, `docs/plans/*`, `docs/decisions.md`, `docs/branch-ledger.md`, `CHANGELOG.md`, `docs/project-log.md`,
   `docs/state-backlog.md`) — put proposed D-rows and doc text in your REPORT instead; the integrator records them.
   Never pkill by name (stop processes by port). Never headed browsers. Never touch the `fugue-demo` stack or the dev
   volume `fugue_fugue-pgdata` except where your lane file says you may READ :8080.
4. A migration is a critical seam: only if your lane file allows it; it needs a rollback test and a catalog row
   (`docs/migrations/`), and you must state it in the report so the integrator runs the migration QA gate.
5. Honest data only. Never invent backend capability, numbers, provenance, or test names. Labels say what a thing really is.
6. Do not change existing tests to make them pass unless the test is wrong; if you do, prove the new assertion still fails
   when the behaviour breaks (inject the condition) and say so in the report.
7. Stray `pnpm-lock.yaml` / `pnpm-workspace.yaml` at the worktree root are a known commit-hook side effect: delete them,
   never commit them. "Can't find lefthook in PATH" means pre-commit checks may not have run: run the gates yourself.

## Full .NET suite: one at a time across the fleet
The full suite takes ~10 min and Testcontainers contention makes parallel runs fail falsely. Run targeted tests while you
work (`dotnet test --filter ...`). Run the FULL suite exactly once at the end, under this lock:
```
L=/tmp/fugue-dotnet-suite.lock; until mkdir "$L" 2>/dev/null; do sleep 30; done; trap 'rmdir "$L"' EXIT
dotnet test 2>&1 | tail -30
```

## Ports
Use only the ports in your lane file (web dev server, API, throwaway Postgres). A throwaway Postgres for API lanes:
`docker run -d --rm --name <lane>-pg -e POSTGRES_PASSWORD=pw -p <pgport>:5432 postgres:17-alpine`, run the API against it
with `--seed` as runbook 03 describes, and stop/remove both at the end (by port/name you created).
Web-only lanes may proxy the dev server to the demo API on :8080 for reading and for creating throwaway demo data; sign in
once per script (the login limiter allows 20/min per IP and is shared with the integrator).

## Gates (run them, paste real output lines into the report)
- Web, if you touched `web/`: `cd web && npm run lint && npm test && npm run build`
- .NET, if you touched `src/` or `tests/`: `dotnet build` (0 warnings), `dotnet format --verify-no-changes`,
  targeted tests, then the full suite once under the lock. If the OpenAPI changes: `npm run api:openapi && npm run api:gen`
  from the repo root as the repo's scripts define, and commit the regenerated files.
- API behaviour: Bruno requests for new endpoints in `bruno/fugue/<area>/`, run with
  `cd bruno/fugue && npx bru run <area> --env local --env-var baseUrl=http://localhost:<apiport>`.
- UI: a headless Playwright node script (pattern: `web/check/*.mjs` or a scratch file in your worktree, not committed
  unless it is a real test) that signs in, exercises your change, asserts it, collects console errors (must be 0), and
  saves screenshots to `.worktrees/_briefs/reports/<lane>-*.png`.

## Phases
1. INSPECT: map the files, verify the premises, write a short plan (in your report's first section).
2. EXECUTE: the plan, in the commit slices your lane file names.
3. VALIDATE: run the gates; report deviations from the plan ("planned X, found Y, did Z because").

## Commits
Conventional commits (`feat(web): ...`, `fix(api): ...`), body lines <= 100 chars, last paragraph exactly:
```
Refs: D-064
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_013M8hUMmzSgW53sxbR6rjBi
```
Leave `git status` clean at the end (nothing untracked except ignored files).

## Report
Write `.worktrees/_briefs/reports/<lane>.md` (absolute: `~/Repos/personal-work/fugue/.worktrees/_briefs/reports/<lane>.md`):
plan, commits (sha + subject), gate output lines, headless proof + screenshot paths, deviations, proposed D-rows / doc text
for the integrator, merge notes (files likely to conflict with other lanes), and a final section
"What I could NOT verify, and what would settle it". Then send the integrator one message:
`SendMessage to overnight-lead: "<lane> READY <head-sha> report: <path>"` and end with that same single line as your reply.
If you hit a stop condition, write the report anyway with status BLOCKED and the reason, and send `<lane> BLOCKED ...`.

## Addendum 1 (dispatcher, 03:33) — overrides rule 3 for the ledger row only; everything else unchanged
1. House docs are part of your contract. Read them now if you have not: `AGENTS.md` (commit gates: `Refs:` trailer must
   resolve, `check-fix-has-test` = every `fix(...)` commit must touch a test, secrets-scan, test-integrity),
   `docs/ai/harness.md` (lead/lane model, slices, lane report), `docs/ai/how-we-use-ai.md`, `docs/conventions/doc-spine.md`,
   and `CLAUDE.md` "Coding Rules" + "Gotchas". If they conflict with this header, the stricter rule wins; report the conflict.
2. Commit iteratively: one commit per slice AS SOON AS that slice's targeted gates pass. Never hold work uncommitted for more
   than one slice; never end with one big commit. If you are already past a slice boundary with uncommitted work, commit now.
3. Lane status file (house convention, `docs/ai/lanes/<lane>-status.md`, same shape as `docs/ai/lanes/nth-hood-status.md`):
   create it in your first commit and update it in EVERY slice commit (commits so far, gates copied from output, shared files
   changed exactly, deviations, next slice). Your final report file in `.worktrees/_briefs/reports/` still applies.
4. Branch ledger: in each slice commit, update ONLY the Status cell of YOUR OWN row in `docs/branch-ledger.md`
   (e.g. `11a: slice 2/4 committed (abc1234) — API endpoints; next: web page`). Touch no other row or line. The integrator
   resolves ledger conflicts at merge by keeping main's file and retiring your row.
