# Lane nth-hooda (item 11e part A) — Opus — worktree .worktrees/nth-hooda, branch nth-hooda, base dacfb06

Objective: the "Under the hood" sidebar gives every platform/access page a proper how-it-works treatment that an interviewer
can follow end to end, and it sets the standard (Audit log first) that part B copies.

Current state (verify): the model is `web/src/app/features/hood/hood-note.ts` (HoodNote: why, how, tests, adrs, decisions,
guards); notes live in `features/<x>/<page>.hood.ts` and are attached as `data.hood` in `app.routes.ts`. Existing notes:
home, access, audit, report, claim detail, my claims, team, claims queue. Missing: modules, webhooks, customers, me (profile),
audit/trace (The Score), sheet, calendar, assessment (the last three are part B). Owner (03:20): "a lot of modules need proper
how it works under the hood treatment — the example was the audit log".

Desired state:
1. Model (you own it): add an optional `flow` to HoodNote — an ordered list of steps showing what really happens for the
   page's main action: `{ layer: 'web'|'api'|'handler'|'db'|'audit'|'outbox'|'webhook'|'search'|'job'; text: string;
   path?: string }` (path = real repo file). Render it in the sidebar as a compact vertical sequence before "how". Keep it
   optional so part B and existing notes still compile. Publish the exact type in your report early (part B reads it after
   merge; part B fills only existing fields).
2. Audit log note rewritten to the standard: flow (request → ApiControllerBase → handler → audit row in the same transaction
   → append-only trigger → background indexer → Meilisearch → search API), real tests by name, ADR-010 and the D-rows that
   really govern it, guards with verbatim code excerpts, link to The Score.
3. New notes: audit/trace (The Score), modules, webhooks (outbox → dispatcher → signed delivery → retries/dead letter),
   customers (the starter example), me/profile (permission resolution: roles + allow − deny − modules off), and refresh access.
4. Bug: a dark tooltip showing a request path (e.g. "/audit?limit&entityType&entityId") floats over the page header at the
   top-left, detached from the sidebar's request row — fix its positioning/teardown.
Every excerpt copied verbatim from the file it names; every test name exists in that file (the existing hood-notes.spec may
already enforce this — extend it to the new notes). Never invent.

Acceptance: hood-notes spec passes and covers new notes; headless: open each page as admin, open the sidebar, screenshot,
flow renders, tooltip stays attached and disappears on leave; 0 console errors; web gates green.
Non-goals: no claims-area notes (nth-hoodb), no backend, no new pages.
Commits: 1 model + sidebar flow rendering + tooltip fix, 2 audit log + The Score notes, 3 modules/webhooks/customers/me/access.

Recite: flow model, Audit log as the standard, six platform pages covered, tooltip fixed, every excerpt and test name real.
