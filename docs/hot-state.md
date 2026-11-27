# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-28 01:00 +08 (submission today 15:00)
`main` = v0.0.2 (tag) + post-tag polish + header claims search (D-063, merge 133c962). FEATURE FREEZE (D-062) otherwise stands. Timed claims build = `claims-window-start..claims-window-end`.
Verified on 133c962: web 226 Vitest, lint, build clean; claims search 7 + ArchTests 37 in isolation; full suite = the pre-push gate (check `git ls-remote origin main` equals `git rev-parse main`). Headless on the demo stack (:8080): claimant finds own claims by number, policy and place and never another claimant's; staff find any; zero console errors; top bar and sidebar brand row share one line at 54 px (full and rail).
No worktrees but `main`; no lanes running.

## Next action (exact, in order) — demo start
1. Right before the call, recreate the clean demo stack: `docker compose -p fugue-demo down -v && docker compose -p fugue-demo up -d --build` (dev data stays in `fugue_fugue-pgdata`; back to it with `docker compose -p fugue-demo down -v && docker compose up -d`).
2. Do NOT run Bruno against the demo stack (it adds BRU- claims).
3. Two windows: claimant `self@fugue.test` left, staff `staff@fugue.test` right, `http://localhost:8080/?tour=off` (add `&hood=off` while presenting), password in runbook 05. Script: `docs/DEMO.md`; rehearsal checklist: section 6 of `docs/reviews/fable/2026-11-27-panel-stress-test.md`. New beat: type a claim number in the header search as the claimant, then another claimant's number (nothing), then the same as staff.
4. Private prep: `~/Repos/personal-work/interview-prep/walkthrough-prep.md` sections 0b, 12, 13.

## Not done (owner's call)
- `./start.sh` from a fresh clone end to end (tested only with a stub docker). It clashes with `fugue-demo` container names: `docker compose -p fugue-demo down` first.
- Hosting (owner: tomorrow, if time): API as a Render free Docker web service (sleeps after 15 min, ~1 min wake) + Neon free Postgres + console as a static site; fallback Azure App Service F1.

## Pending owner questions
Trimming Univer and the calendar for memory-capped deploys (owner's own note). Panel leftovers: backlog #009.

## Owed inbox
nth-quality flagged: context threshold 50% (`docs/conventions/doc-spine.md`) vs 60% elsewhere; `.githooks/README.md` says set `core.hooksPath` (AGENTS.md forbids). nth-polish: banner/tracks/Help sit inside commit c193a2e (message names only the SLA fix).

## Traps hit
Failure modes 33 (signed-in AND reloaded browser check), 34 (push keepalive + `git ls-remote`), 35 (shared test clock and background indexer: prove a flaky fix by injecting its condition). Login limiter 429 on back-to-back Bruno runs: `docker compose restart api` (25). Git hooks need `dotnet` on PATH in the committing shell.
