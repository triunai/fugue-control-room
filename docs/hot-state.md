# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-28 13:50 +08, WRAP DONE (submission 15:00)
Release v0.0.3 is written in `CHANGELOG.md`; W-025..W-041 are closed in `docs/project-log.md` and `CHANGELOG.md`. The overnight
plan `docs/plans/overnight-2026-11-28.md` is worked to the end: done or deferred with a D-row (13 IOptions, 15, 11g: D-076;
14 Worker host: D-075). D-066's outcome line has the counts: 35 lanes merged, 17 work items closed, 10+ pushes behind the full suite.
Demo stack `fugue-demo` recreated CLEAN at 13:06 from main 3b903ba (`down -v`, `up`); since then the web image was rebuilt for
docs data only. Use it read-only until the demo. Verified on the clean stack (13:05-13:10): Bruno `--env compose` 131 requests,
157/157 tests (run on the 13:05 stack, then recreated again so it stays clean); four personas (admin, manager, staff, self)
signed in, reloaded and navigated with ZERO console errors; read-only role refuses writes, the MCP answers and rejects writes.
Last pre-push suite: Fugue.Tests 864/864 + ArchTests 37/37 (1 m 39 s). Web 390 Vitest.

## After the tag (13:33-13:50)
Owner asks merged on main after `v0.0.3` (tag stays on eb48905; main is the submission): explained v0.0.3, Next step padding,
Help guide by role, Stack dialog corrected, notifications design (docs/design/notifications.md, design only), search hint after
submit. Web image rebuilt at 13:49, no data change; the demo DB is still the clean 13:21 recreate. Web 403 Vitest. A browser tab
opened before a rebuild shows the old app until a hard reload.

## Known limits (answers, not fixes)
D-079 relay cycle with the broker down (about 200 s per cycle at defaults, one transaction); D-080 an issued access token lives
15 min after deactivation; search is keyword full-text, not hybrid; PDF letters print non-Windows-1252 text as "?" (D-070);
HybridCache covers GET /me only (D-081); owner belief corrected: PostgreSQL 17, ids UUID v7.
Traceability: 14 unlinked commits; tonight's 6 are lanes' "Merge branch 'main' into nth-*" (no trailer), not product changes.

Known polish: on the claim record the shell's header controls ("Fugue, explained · Help") show faded inside the navy hero (seen in claims-record-officer.png); cosmetic, not fixed.

## Traps hit
33, 34, 35, 36 (host asleep on battery stalls Testcontainers), 37 (a relaying test must settle leftover due rows first).
Running Bruno on the demo stack writes data (claims, users): recreate after it before screenshots or a demo.
Lanes whose lefthook is off PATH skip the secrets scan; it fires at the merge. Background jobs from an earlier session can
outlive it: check `ls-remote` before assuming a push failed.
