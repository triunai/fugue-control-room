# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 20:50 +08 (release wrap; submission 2026-11-28 15:00)
Released **v0.0.2** (tag) on `main`, pushed to `origin` = github.com/triunai/fugue-control-room with `--tags`. FEATURE FREEZE (D-062): fixes and polish only. Timed claims build = `claims-window-start..claims-window-end`.
Last green sha: 03448e5 (src/ unchanged since 457597b, where ArchTests 37 + Tests 740 passed on Testcontainers Postgres; Bruno 101/101 requests, 123/123 tests on the Docker stack; web 218 Vitest, lint, build clean; headless browser signed in + after a reload + full load of /sign-in: zero console errors). Fresh-database start verified on project `fugue-demo` (migrate + seed from empty, sink delivery 1/1).
No worktrees but `main`; no lanes running.

## Next action (exact, in order) — demo start
1. Right before the call, recreate the clean demo stack (the current one has one smoke-test claim):
   `docker compose -p fugue-demo down -v && docker compose -p fugue-demo up -d --build` (the dev stack is already down; its data stays in `fugue_fugue-pgdata`; back to it later with `docker compose -p fugue-demo down -v && docker compose up -d`).
2. Do NOT run Bruno against the demo stack (it adds BRU- claims); run it on the dev stack only.
3. Two windows: claimant `self@fugue.test` left, staff `staff@fugue.test` right, `http://localhost:8080/?tour=off` (add `&hood=off` while presenting), password in runbook 05. Script: `docs/DEMO.md`; rehearsal checklist: section 6 of `docs/reviews/fable/2026-11-27-panel-stress-test.md`.
4. Private prep: `~/Repos/personal-work/interview-prep/walkthrough-prep.md` sections 0b, 12, 13 (deploy answer, test counts, Excel import answer, prepared answers).

## Owner decisions (today)
Feature freeze; Kafka kept, off by default, out of the demo and README highlights (D-062). v0.0.2. Hood: guards with real code, "How the API does it", "Tested by", ADRs first. D-060 corrected: the shell was adapted, not ported; faithful port parked (backlog #003).

## Pending owner questions
Trimming Univer and the calendar for memory-capped deploys (owner's own note, still owed). Panel leftovers to decide or just answer: backlog #009.

## Owed inbox
nth-quality flagged: context threshold 50% (`docs/conventions/doc-spine.md`) vs 60% elsewhere; `.githooks/README.md` says set `core.hooksPath` (AGENTS.md forbids). nth-polish: banner/tracks/Help sit inside commit c193a2e (message names only the SLA fix); assessment label clipping not reproduced, not proven fixed.

## Traps hit today
Failure mode 33 (a web merge passes unit/lint/build and still does not boot for a returning user: load the page signed in AND after a reload). Failure mode 34 (a push over SSH outlives its connection during the 10-minute pre-push test run: `GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=90"`, then `git ls-remote origin main`). Login limiter 429 on back-to-back Bruno runs: `docker compose restart api` (failure mode 25).
