# Lane nth-hoodb (item 11e part B) — Sonnet — worktree .worktrees/nth-hoodb, branch nth-hoodb, base dacfb06

Objective: every claims-area page has a complete, accurate "Under the hood" note.

Current state (verify): notes are `features/<x>/<page>.hood.ts` files typed by `HoodNote` in
`web/src/app/features/hood/hood-note.ts`, attached as `data.hood` in `app.routes.ts`. Existing claims-area notes: home,
claims queue, claim detail, my claims, report, team. Missing: reserve sheet (`claims/sheet`), SLA calendar (`claims/calendar`),
assessment (`claims/:id/assessment`). Read the existing best notes as the template.

Desired state: new notes for sheet, calendar, assessment; existing claims-area notes reviewed and completed so each has real
why/how, 3-5 real tests (names copied verbatim from the files), the ADRs and D-rows that really govern it (e.g. ADR-006
xmin ETag, ADR-011 outbox, ADR-013 idempotency, D-056, D-063 search scope — verify each), and guards with verbatim excerpts.
Use ONLY the existing HoodNote fields. nth-hooda is adding an optional `flow` field in parallel: do NOT add it, do NOT edit
anything under `features/hood/**`; the integrator may ask you for flows after their merge.

Acceptance: the hood notes spec (extend its list to the new notes if it enumerates them — that spec file is under
features/hood: if extending it is required, put your additions in a NEW spec file next to your notes instead) passes;
headless: each claims page as staff with the sidebar open, screenshot; 0 console errors; web gates green.
Non-goals: no platform/access notes, no sidebar code, no backend.
Commits: 1 new notes (sheet, calendar, assessment), 2 completed existing notes.

Recite: nine claims-area pages, real tests and excerpts only, existing fields only, nothing under features/hood.
