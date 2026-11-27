# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-28 03:42 +08 (submission today 15:00; owner asleep)
Topology (D-066): the RELAY session dispatches lanes (contracts `.worktrees/_briefs/`, archived in `docs/ai/briefs/fleet-2026-11-28/`);
the OVERNIGHT LEAD is integrator only: per "<lane> READY <sha>": head sha from git, `git diff --stat dacfb06..<lane>` inside OWNERS.md,
merge --no-ff (ledger conflict: keep ours, drop the lane row), web gates on merged main, rebuild `fugue-demo`, headless on :8080, tick,
retire worktree + branch. Merge commits carry `Refs: W-0NN, D-064` (W rows W-025..W-038 in workstreams.md). Never spawn lanes.
Take `/tmp/fugue-dotnet-suite.lock` around every push and heavy .NET test run. Regenerate `npm run gen:decisions` (web) after any D-row.
Task list: `docs/plans/overnight-2026-11-28.md` (Fleet map at the top). Done: 1, 1b, 2, 3, 3b, 4, 6, 7, 8, 9, 11d, 17a.
origin/main = add9f6f; a push of 8765f5d under the lock was running at 03:40 (check `git ls-remote origin main`).
Demo stack `fugue-demo` RECREATED at 03:38 from 8765f5d (throwaway volume `fugue-demo_fugue-pgdata`; postgres on 5433).

## Live lanes (relay-dispatched, report to the integrator)
nth-forms (5), nth-import (11a), nth-searchx (10), nth-fixes (3a, 7a, 7b, 11f), nth-pdf (11b), nth-hooda then nth-hoodb (11e;
hoodb HEAD 37db1d0 is READY but merges AFTER hooda), nth-paid (11c / W-037).

## Next action (exact)
1. When the push finishes: ls-remote must equal the pushed sha; then 4a under the lock: in AuditSearchTests change q=Dev%20Admin to a
   name with no full match, run that class, expect red, revert (prove 4a95058's assertion still bites); tick 4a.
2. Merge READY lanes in arrival order (hooda before hoodb). Then 11, 11g, 12-17 per the plan; wrap order is written in item 16.

## Known limits (answers, not fixes)
"Send question" during an API restart closed its dialog with no POST (UNVERIFIED, D-069). Claimants see the SLA calendar for their own
claims by design. No email/SMS consumer: signed webhooks are the integration point (D-069). Owner belief corrected: PostgreSQL 17
(postgres:17-alpine), ids UUID v7 from the API (Uuid7.NewGuid) — "timestamped GUIDs" is right, "Postgres 18" is wrong.

## Traps hit
33 (signed-in + reloaded browser check), 34 (push keepalive + ls-remote), 35 (shared test clock, background indexer). The commit hook
toolchain leaves stray root pnpm-lock.yaml/pnpm-workspace.yaml in worktrees: delete, never commit. Lanes committed with lefthook off
PATH, so the secrets scan first ran at the integrator's merge (66604db): fix in the merge. Fill a CDK drawer form only after it opens
(~1 s) in headless scripts. The web container needs a few seconds after `up` before sign-in works. zsh: no `timeout`, no word-split.
