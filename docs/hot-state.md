# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 17:02 +08 (CLAIMS WINDOW OPEN; submission 2026-11-28 15:00)
Branch `main`. The take-home brief (`~/Downloads/V2_FullStack_candidate_assessment_brief (1).docx`, outside the repo) is being built in a timed window opened by tag `claims-window-start` (dd46eca, 16:56 +08). Hard stop 21:56 +08. Plan: `docs/plans/claims-window.md` (D-052, D-053). Nothing pushed (no remote).
Last green sha: 8c6d5eb (build 0 warnings; ArchTests 37 + Tests 462 = 499/499; console unit green; lead run).

## Last action done
Scaffold lanes merged and verified after the tag (started before it, `Refs: W-007`): idempotency store + Codex fix round, console shell, modules table, auth readability (`JwtCurrentUser`, `FugueClaimTypes`). Docker stack rebuilt on :8080/:5081 (dev DB has every migration).

## Next action (exact)
1. Wait for lane `claims-core` (worktree `.worktrees/claims`, branch `claims-core`, brief in the lead's scratchpad; slices 1-7 of the plan). It rebases on main before its migration. Re-verify each report against git + `dotnet test`.
2. When it reports the contract commit: dispatch the Angular claims lane (plan decision K) against the regenerated client.
3. Codex review on the state machine / visibility / permissions seam; fix round; merge `claims-core` into main.
4. README first lines: disclosure + the tag + `git diff claims-window-start..HEAD`; runbook 11; demo script; journal entries; wrap.

## Uncommitted work in progress
None on main. `.worktrees/claims` belongs to the claims-core lane.

## In flight (open W ids)
W-020 claims window (ACTIVE) · W-007 (idempotency done; versioning/cache/OTel not built) · W-018 (done, to close at wrap) · W-019 users list (parked until after the window) · W-010 (parked).

## Pending owner questions
None blocking. Owner confirmed: window now, panel accepts a disclosed starter, no live session.

## Demo notes
Console http://localhost:8080 (nginx) · API :5081 · Postgres :5433 (pgAdmin connected; file logging on, see runbook 03). TPMS reference app ran on :3100 against a throwaway local DB `tpms-local-pg` on :5434 (not this repo).

## Owed inbox
- Close W-018 and W-007 (partial) at wrap with close-workstream.mjs; `npm run trace` after.
- Failure mode to add: `codex exec` waits on stdin when not a TTY (pass `< /dev/null`); it was killed by the alarm while the wrapper printed exit 0.
- Backlog KIV #002 tour, #003 shell rest, #004 roles matrix.
