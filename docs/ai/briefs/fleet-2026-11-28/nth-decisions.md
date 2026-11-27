# Lane nth-decisions (item 11d) — Sonnet — worktree .worktrees/nth-decisions, branch nth-decisions, base dacfb06

Objective: an interviewer can open one page in the console and follow the project's decision trail over time — every ADR and
D-row, dated, grouped by the release it landed in, linked to its full text.

Current state (verify): 14 ADRs in `docs/adr/ADR-0NN-*.md` (+ `_template.md`, skip it); D-rows in `docs/decisions.md` — an
index table near the top plus `## D-0NN — title — date` sections (format varies: parse defensively, report rows you could not
parse). `CHANGELOG.md` has version headings (`## v0.0.2 — 2026-11-27`, ...). Repo links use `hoodUrl()` from
`web/src/app/features/hood/hood-note.ts` (import it; do not edit that file).

Desired state:
1. `scripts/gen-decisions.mjs` (node, no deps) parses ADRs (id, title, status, date from the file) and D-rows (id, title,
   date, one-line summary), assigns each to the CHANGELOG version whose date range covers it (entries after the newest
   version go to "Unreleased"), writes `web/src/app/features/decisions/decisions.data.json` (sorted, deterministic).
   Add an npm script (e.g. `npm run gen:decisions`) in web/package.json; commit the generated JSON.
2. Page `/decisions` (all signed-in staff/admin; check how other pages gate by permission and pick the least surprising),
   nav entry under the most fitting group: a vertical timeline grouped by version, newest first, each entry: id chip (ADR vs
   D), title, date, one line, link to the repo file/anchor. Filter chips: All / ADRs / Decisions; a text filter. Console
   tokens, serif headings, no new dependency. Help-panel friendly: add a short `data.hood` note only if nth-hooda's model is
   merged — otherwise skip (no edits to features/hood/**).
3. A spec that runs the generator on fixtures and asserts parsing of both formats.

Acceptance: JSON contains all 14 ADRs and every D-row id present in decisions.md (report the count); page renders, filters
work, links point at existing paths; headless as admin and staff, screenshots; 0 console errors; web gates green.
Non-goals: no editing of the docs themselves, no backend endpoint, no hood sidebar changes.
Commits: 1 generator + spec + JSON, 2 page + route + nav.

Recite: generated from the repo docs, every ADR and D-row, grouped by version, linked, filterable, no drift.
