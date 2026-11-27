# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 (submission 2026-11-28, headless run requested)
Branch: `main`, no other worktrees, nothing pushed (no remote).
Last green sha: 37df552 (build 0 warnings; ArchTests 35 + Tests 371 = 406/406; script tests 83/83; console unit 18/18; console e2e 5/5; Bruno 49/67 green). Spine commit after it is docs only.

## Last action done
Lead wrap: D-045..D-048 captured, W-011 closed, W-018/W-019 opened, failure mode 26 (OrbStack down), handoff + seed prompt written.

## Next action (exact)
1. Paste the SEED PROMPT from `docs/ai/handoffs/2026-11-27-lead.md` into a fresh session.
2. It starts with W-018 (audit import from the read-only source at `~/Repos/career-work/<source-repo>/ (exact path: in the owner's seed prompt)`, copy allowed per D-048, names scrubbed).

## Uncommitted work in progress
None. Local-only and ignored: `.beads/`, `.fugue-local/`, `.env`, `web/node_modules`.

## In flight (open W ids)
W-018 audit import · W-007 S5 remainder (idempotency store, Dockerfile + one-command compose first) · W-019 users list · W-010 Codex gate on a real push (parked: no remote).

## Pending owner questions
- (owner reminder) Study the work repo's model/entity file conventions — the source is now cloned at `~/Repos/career-work/<source-repo>/ (exact path: in the owner's seed prompt)`.
- Review the "pending owner review" entries in `docs/ai-journal.md` when time allows.

## Tests (verified by the lead 2026-11-27)
All green as above. If `Fugue.Tests` shows hundreds of failures in ~6 s, OrbStack is down (failure mode 26): `open -a OrbStack`, wait for `docker ps`, re-run.

## Demo notes
One API instance only (two share the compose DB). Console e2e needs `RateLimiting__Auth__PerEmailPermits=60`. Demo path: `docs/runbooks/09-console.md`.

## Owed inbox
- `access_audit` email hashes written before W-013 do not correlate with newer ones (HMAC subkey changed) — expected, no action.

## Seed prompt
See `docs/ai/handoffs/2026-11-27-lead.md` (SEED PROMPT section).
