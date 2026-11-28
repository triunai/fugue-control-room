# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-28 11:50 +08 (submission today 15:00; owner at the keyboard)
Topology (D-066): a DISPATCHER session writes lane contracts (`.worktrees/_briefs/`, state `DISPATCH-STATE.md`, seed
`DISPATCHER-SEED.md`; the 11:36 dispatcher hit its cap, a fresh one takes over from the seed). The OVERNIGHT LEAD is integrator
only: per "<lane> READY": head sha from git, diff inside OWNERS.md, merge --no-ff (ledger conflict: keep ours, drop the row;
OpenAPI + client: `npm run api:openapi` then `cd web && npm run api:gen` inside the merge, never hand-merged; decisions JSON:
`cd web && npm run gen:decisions` after any D-row or merge touching it), web gates, rebuild `fugue-demo`, headless on :8080,
tick, retire. Merge commits: `Refs: W-0NN, D-0NN`. Take `/tmp/fugue-dotnet-suite.lock` around pushes and heavy .NET runs.
A GREEN suite is valid on battery; a red inside Docker/Testcontainers is suspect until `pmset -g batt` says AC (failure mode 36).
Plan: `docs/plans/overnight-2026-11-28.md`. Deferred: 13, 15, 11g (D-076), 14 (D-075).
origin/main = a66442d (push 7, 849 + 37 green); local main is ahead by docs + web merges (push after the refactor merges).
Demo stack `fugue-demo` up on :8080 (throwaway volume, postgres 5433); recreate only at 14:00.

## Live lanes (relay-dispatched)
nth-refactor (item 12, Opus, EXCLUSIVE on src/ + tests/, from afc6fe9; merge on a green 849 + 37, OpenAPI byte-identical, moves +
usings + 7 hood.ts path strings; then apply its "Slice shape" text to ARCHITECTURE.md / CLAUDE.md).
Web-only (W-040, items 18-24, from ddd2ded): nth-tour (18), nth-searchui (19), nth-importui (20), nth-scorefix (24).
Unregistered in the ledger: nth-k6 (load test, 11:33) — ask the dispatcher for scope + W before merging.
PENDING DISPATCH after nth-refactor merges (do NOT dispatch them yourself): nth-useredit (item 25) and nth-hybridcache.

## Next action (exact)
1. Merge READY lanes: refactor first, then the web lanes. Tell the dispatcher the refactor merge sha at once.
2. ~12:30 README refresh (item 11, a lane). 13:00-14:00 fixes only. 14:00 wrap = items 16, 17 (order written in item 16).
3. If no dispatcher has announced itself by 13:30, run the wrap checks yourself (the 14:03 verification cron died with it).

## Known limits (answers, not fixes)
Search is keyword full-text (synonyms, phrases for numbers, highlights), NOT hybrid: no embedder. "Send question" during an API
restart (UNVERIFIED, D-069). PDF letters print non-Windows-1252 text as "?" (D-070). Owner belief corrected: PostgreSQL 17, ids UUID v7.

## Traps hit
33, 34, 35, 36 (host asleep on battery stalls Testcontainers). Hook toolchain leaves stray root pnpm files in worktrees: delete.
Lanes with lefthook off PATH skip the secrets scan; it fires at the merge. Headless: fill CDK drawers after ~1 s; the web container
needs seconds after `up`; the API's general rate limiter answers 429 after many page loads (wait a minute). zsh: no `timeout`.
