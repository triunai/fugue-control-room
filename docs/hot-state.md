# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 18:20 +08 (claims core DONE; window hard stop 21:56 +08; submission 2026-11-28 15:00)
Branch `main`, no worktrees. Nothing pushed: the repo has no remote (the owner allowed pushes mid-session; creating the remote, account and visibility, is the owner's call).
Last green sha: 1658a9f (merge of rd-sql; later commits are docs only). Lead run: build 0 warnings; ArchTests 37 + Tests 623; `dotnet format` clean; OpenAPI snapshot identical; web 62/62, lint clean, build green; Bruno `--env compose` 82/82 requests, 104/104 tests against the rebuilt Docker stack.

## Last action done
Headless lead: verified the claims backend, Codex seam review (D-056: 3 accepted, 2 declined), fix round, Angular claims screens, merged `claims-core`; readability pass in two lanes (D-054 SQL files + read-only role + MCP config; D-055 one file per concept + backlog #005) merged; claims list params camelCase (18c98d4); README, `docs/CLAIMS.md`, ADR-013 amendment, how-we-use-ai refresh, failure modes 30-31, D-057; W-020 and W-007 closed; W-023, W-024 opened.

## Next action (exact)
Optional, owner-asked, inside the window if time allows: W-022 quality wave (`docs/workstreams.md` WS-22): Stryker.NET on the Claim state machine / AuditRedactor / Idempotency-Key parser, PICT pairwise for role x status x action, k6 scripts, harness explainer doc. Otherwise: demo prep from `~/Repos/personal-work/interview-prep/walkthrough-prep.md` section 0b (stack: `docker compose up -d --build`, console http://localhost:8080).

## Uncommitted work in progress
None.

## In flight (open W ids)
W-022 quality wave (unblocked) · W-024 claims follow-ups (#006 counts endpoint + open-first queue, #007 past questions) · W-023 versioning/cache/OTel · W-021 audit search · W-019 users list · W-010 Codex gate (parked, needs a remote).

## Pending owner questions
- Create a git remote (GitHub account, private or public) so pushes and CI can run? Not done: an outward-facing choice.
- The owner's own words for the "Nice to haves" note on trimming Univer and the calendar for memory-capped deploys (which calendar library; what was trimmed). Do not invent them.

## Demo notes
Console http://localhost:8080 · API :5081 · Postgres :5433 (pgAdmin, file logging). Personas: `self@` claimant, `staff@` officer, `manager@` manager, `admin@` (password in runbook 05). Reviewer start-here: `docs/runbooks/00-for-reviewers.md`; claims walk-through `docs/runbooks/11-claims.md`; six-minute demo script in the private prep doc, section 0b. One API instance per DB.

## Owed inbox
- `docs/traceability.md` regenerated after this wrap (commit alone).
- KIV backlog #002 tour, #003 shell rest, #004 roles matrix; #006, #007 now W-024.
- Stray `pnpm-lock.yaml`/`pnpm-workspace.yaml` keep appearing in new worktrees (some tool runs pnpm); delete, never commit.
