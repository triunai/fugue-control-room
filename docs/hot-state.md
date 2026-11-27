# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 17:25 +08 (CLAIMS WINDOW OPEN, hard stop 21:56 +08; submission 2026-11-28 15:00)
Branch `main`. Window opened at tag `claims-window-start` (dd46eca). Plan `docs/plans/claims-window.md`. Nothing pushed (no remote).
Last green sha: 8d77574 (build 0 warnings; ArchTests 37 + Tests 462 = 499/499; ledger + adr checks clean; Bruno compose 56/56 requests, 74/74 tests; lead run at wrap).

## Last action done
Wrap mid-window: W-018 closed; W-021, W-022 opened; failure modes 27-29; journal; evening addendum + SEED PROMPT in `docs/ai/handoffs/2026-11-27-lead.md`; claims lane briefs copied to `docs/ai/briefs/`.

## Next action (exact)
Backend lane FINISHED all 7 slices on `claims-core` (9cec1c0, 21f1e33, bcff006, 17a011e, e1e0909, ea968f3, 8f4232b; build 0 warnings, ArchTests 36 + Tests 553 green in the worktree, Bruno claims folder, runbook 11, seed with claimant/officer/manager) but it is NOT rebased on main. Paste the SEED PROMPT (handoff evening addendum), skipping "dispatch claims-core brief": instead (1) rebase `claims-core` on main (regenerate `AddClaims` after `AddIdempotencyKeys`, keep the catalog row, refresh the snapshot, fix the ArchTests count drift), (2) add `[Idempotent]` to `ClaimsController.Submit` + one replay test, (3) dispatch the Angular lane (`docs/ai/briefs/claims-web.md`), (4) Codex review, merge, readability pass, docs, wrap. Delete stray `pnpm-lock.yaml`/`pnpm-workspace.yaml` in the worktree.

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
