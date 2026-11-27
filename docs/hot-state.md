# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 17:25 +08 (CLAIMS WINDOW OPEN, hard stop 21:56 +08; submission 2026-11-28 15:00)
Branch `main`. Window opened at tag `claims-window-start` (dd46eca). Plan `docs/plans/claims-window.md`. Nothing pushed (no remote).
Last green sha: 8d77574 (build 0 warnings; ArchTests 37 + Tests 462 = 499/499; ledger + adr checks clean; Bruno compose 56/56 requests, 74/74 tests; lead run at wrap).

## Last action done
Wrap mid-window: W-018 closed; W-021, W-022 opened; failure modes 27-29; journal; evening addendum + SEED PROMPT in `docs/ai/handoffs/2026-11-27-lead.md`; claims lane briefs copied to `docs/ai/briefs/`.

## Next action (exact)
Paste the SEED PROMPT from `docs/ai/handoffs/2026-11-27-lead.md` (evening addendum) into a fresh session. Its first step: inspect `.worktrees/claims` (branch `claims-core`) and its `docs/ai/lanes/claims-core-status.md`, rebase on main if needed, then run the backend and Angular claims lanes in parallel.

## Uncommitted work in progress
`.worktrees/claims` (branch `claims-core`) may hold the claims-core lane's last slice or a stash named `claims-core wip: ...`; check `git -C .worktrees/claims status` and `git -C .worktrees/claims stash list`. Main: none.

## In flight (open W ids)
W-020 claims window (ACTIVE) · W-007 (readability pass after the claims merge) · W-021 audit search · W-022 post-core quality wave + nice-to-haves · W-019 users list · W-010 (parked).

## Pending owner questions
- The owner's own words for the "Nice to haves" note on trimming Univer and the calendar for memory-capped deploys (which calendar library; what was trimmed). Do not invent them.

## Demo notes
Console http://localhost:8080 · API :5081 · Postgres :5433 (pgAdmin, file logging). Reviewer start-here: `docs/runbooks/00-for-reviewers.md`. One API instance per DB. TPMS reference app (not this repo) ran on :3100 with `tpms-local-pg` on :5434.

## Owed inbox
- `docs/ai/how-we-use-ai.md` is stale (says MapStatus unwritten); refresh in the docs pass.
- `docs/traceability.md` regenerated at this wrap; regenerate again after the claims merge.
- KIV backlog #002 tour, #003 shell rest, #004 roles matrix, #005 filter records.
