# Overnight task list — 2026-11-28 (submission 15:00 +08)

The lead works this list top to bottom under `/loop` while the owner sleeps (ADR-014, D-065). Each item: build in a lane worktree
(`.worktrees/nth-<topic>`, ledger row first), lane reports gates + a headless screenshot, the lead re-runs the gates, looks at the
screenshot, merges, rebuilds the `fugue-demo` stack, re-checks headless, pushes with the SSH keepalive and `git ls-remote`.
Tick `[x]` with the merge sha. Never: force push, `--no-verify`, drop a non-throwaway database, pkill by name, headed browsers.

Order inside P1: demo-visible first (5 to 9), then 11a and 11b, then 10, 11c. Feature cut-off 13:00. 13:00-14:00 only fixes. 14:00: recreate the clean demo stack, final push, hot-state.

## Fleet (03:27, relay-dispatched, base dacfb06; contracts in `.worktrees/_briefs/`)
Item -> lane: 17a nth-tooling · 5 nth-forms · 11a nth-import · 10 nth-searchx · 3a/7a/7b/11f nth-fixes · 11b nth-pdf · 11c nth-paid (+ D-069) ·
11e nth-hooda (part A: hood model, tooltip, audit log, platform pages) then nth-hoodb (part B: claims area) · 11d nth-decisions.
Integrator (overnight lead) per READY: head sha from git, diff inside OWNERS.md, gates on the merged tree, headless on rebuilt
:8080, merge --no-ff, tick, retire. Merge nth-hooda before nth-hoodb. Not assigned yet: 3b, 4a (integrator), 11, 11g, 12-17. W rows W-025..W-038 map items to lanes (docs/workstreams.md);
every integrator merge commit carries `Refs: W-0NN, D-064` (+ the D-rows it records). Ledger conflicts on merge: keep main's.

## P0 — demo breakers
- [x] 1. (03:08, on rebuilt :8080: welcome centred 465,251 510x397; outside click closed it; nav spam then /audit navigated; 0 errors) Browser re-check of 2ba4e19 (welcome can never freeze the page): fresh context, sign in WITHOUT `?tour=off`, welcome
      centred; click outside closes it; then spam the nav (scratch `check/spam2.mjs` pattern) and every link navigates. Note:
      dev-server sign-in timed out at 03:03 (proxy ECONNREFUSED to :5081, then unexplained); check on the rebuilt :8080 instead.
- [x] 2. (03:37: origin = add9f6f = local at push time; pre-push suite Fugue.Tests 760/760, ArchTests 37/37) Push main (keepalive; origin was fb10684 at 03:02).

- [x] 1b. (stale tab, no code change: 03:27 fresh headless staff context, signed in + reloaded on :8080 at dacfb06+, Open claim -> /claims/01a21cfb-37b2-..., Pick up -> /claims/01a21cfb-37af-..., 0 console errors) Open claim (Closest to its SLA card) and Pick up (Pick up next) on Home as staff must land on /claims/<id>. Owner saw both
      dead at 03:12; the relay found a stale pre-redeploy tab (incognito works). CHECK in a fresh, signed-in, reloaded headless
      context; tick "stale tab, no code change" if both pass, fix in a lane only if one fails. Optional: a "New version, reload" toast.

## P1 — owner asks (D-064)
Ownership from 03:23: the relay session dispatches every new lane (file boundaries up front); the overnight lead only integrates
(serial merges, gates re-run, rebuild :8080, headless check, push, tick). At most 2 lanes run the full .NET suite at once.
- [x] 3. (e182dbb; on :8080 03:24 admin created a user with a role, the user signed in, 0 errors) nth-uam (admin adds users, USERS_MANAGE migration + catalog row): wait for its report, verify, merge.
- [x] 4. (dacfb06; on :8080 The Score of the users.create correlation: 4 steps, 0 errors) nth-score ("The Score" per-correlation inspector from real audit/outbox/delivery rows): wait, verify, merge.
- [x] 3a. (merge 3149854, nth-fixes, W-033; on :8080 the matrix shows 51 Granted check icons, 45 sr-only Not granted) Users & access roles matrix: granted cells become the console check icon (navy, aria-label/title "Granted"); empty cells
      get an sr-only "Not granted". Follow-up commit on main (nth-uam merged); icon source is `ui/icon.ts` from nth-claimpage.
- [x] 3b. (03:29 `npx bru run users --env compose` on :8080: 6/6 requests, 6/6 tests; 04 -> 201, 05 -> 409, 06 -> 403; role escalation 403 is covered by UserCreationTests, no Bruno request) Item 3 caveats: run the three new Bruno requests (04 admin creates user, 05 duplicate email 409, 06 staff 403) on :8080.
- [ ] 4a. Item 4 caveats: prove 4a95058's loosened AuditSearchTests assertion still fails when the indexer misses the rows (inject
      the condition, failure mode 35); record `outbox_messages.correlation_id` has no index as a backlog row, not a migration today.
- [x] 5. (merge 1e3d63d, nth-forms, W-030; web 335 tests; on :8080 as claimant: /claims/new has 0 native select/date, 6 examples, an example fills policy SAMPLE-SG-MTR-40000 and date, Market listbox 6 options, Esc closes, submit created CLM-SG-2026-000009, 0 errors; add-user sample password is crypto.getRandomValues words, no literal. Follow-up nth-formsfix (queue filters, calendar day jump, e2e claims.spec.ts:30 locator broken by item 7's copy). Three datetime-local inputs: DONE by nth-datetime, merge 047e565 (fg-date time mode; on :8080 /audit and /access have 0 native date/select, audit From 2026-11-01 08:30 (+08) sent as from=2026-11-01T00:30:00.000Z; mid-day cut From 2026-11-28 03:55 (+08) sends from=2026-11-27T19:55:00.000Z and returns 8 rows vs 50 (page full) unfiltered, a strict subset; 0 errors; web 349 tests). nth-formsfix merged 5f7985e: queue Market/Line and the calendar day jump on fg-select/fg-date (on :8080 as manager: 0 native controls on /claims/queue and /claims/calendar, Market filter 7 options, SG cuts 20 rows to 14 with ?market=SG, 0 errors; web 351 tests); e2e claims.spec.ts locator now uses data-testid=next-instruction (lane: 2 passed). Item 5 COMPLETE. NOT fixed by the lane: the /claims/calendar ResizeObserver console error with the hood open (pre-existing, still open for the 14:00 smoke).) Forms: no native OS popups. Console-styled select (CDK overlay listbox, keyboard + a11y) and date picker for every
      form; "Fill with an example" on every form, like the sign-in demo accounts (Report an incident: 3-4 realistic templates per
      market/line; request info; set reserve; decide/reject reason; claimant answer; webhook subscription; customers; add user).
- [x] 6. (0439df5, verified on :8080 03:21: workbook footer+tab, Export CSV, Save to Excel icons, 0 errors) Claim page assessment worksheet: full workbook feel (footer, sheet tab, spare columns), an Export button;
      rename "Open in Excel" to "Save to Excel" everywhere; icons on Save to Excel and Use as reserve (and Reserve sheet buttons).
- [x] 7. (0439df5: next-step copy, step popovers on hover/focus, How a claim moves modal closes on Esc and outside click) Claim page copy: rewrite the "Next step" block (blocked state reads badly); explain the Handling and Payout tracks
      (hover popover per step and a "How a claim moves" modal).
- [x] 7a. (merge 3149854, nth-fixes; CSS fix + step-track-css.spec; lane headless: the line ends at the last node at 1280 and 390 px, both tracks) Claim
      banner tracks: the connector line overshot the last node (Closed, Paid); counters must not collide; partly complete and phone width.
- [x] 7b. (merge 3149854; claim-tracks-dialog.spec; live 03:53 as self@fugue.test the modal says Assessed, not Reserved) Item 6/7 caveats: the "How a claim moves" modal said
      "Reserved" to claimants; "Open in Excel" outside `web/` is history (journal, reviews, traceability) and stays; README done in 30e4ac1.
- [x] 8. (00e5c76: rounded, token cells, today and events in palette) SLA calendar: rounded corners, warmer cells (tokens, not white), events and today styled in the console palette.
- [x] 9. (00e5c76: Stack dialog; FOUND PostgreSQL 17 (postgres:17-alpine in docker-compose.yml), ids UUID v7 from the API (Uuid7.NewGuid): owner's 'Postgres 18' is wrong, 'timestamped GUIDs' is right) Env pill `dev · net8 · v1` opens a "Stack" modal: Angular + key libraries, .NET 8 + key packages, PostgreSQL version and
      ID scheme. VERIFY first (compose image tag; owner believes Postgres 18 and timestamped GUIDs; state what the code really
      uses, fix the owner's belief in the handoff if it differs).
- [ ] 10. Search hardening from `docs/research/2026-11-28-meilisearch.md`: market name and line searchable, synonyms (SG ->
      Singapore ...), typo tolerance off for claim/policy numbers, highlights showing why a hit matched, facets; settings applied
      idempotently at startup; tests. Decide other global-search candidates (users for admins, customers) with a D-row.
- [ ] 11. README counts and screenshots refreshed after the merges (verified numbers only).

- [x] 11d. (merge 2e0ecbf, nth-decisions; D-067; on :8080 admin sees 80 of 80 with D-066, claimant -> /unavailable, 0 errors; web 270 tests, scripts 88) Decisions timeline in the console (owner 03:15): one read-only page or help-panel tab, ADR-001..014 and D-001..065 with
      id, title, date, one line, link to the ADR file or decisions.md anchor, grouped by the CHANGELOG version it landed in;
      generated JSON from the repo docs at build time, no API. Weigh scope first; a cut needs a D-row. Pairs with 11e (help panel).
- [x] 11e. (A 60e978c nth-hooda, B a4df949 nth-hoodb, flows 8ed5cce nth-hoodflow, W-035; on :8080 admin: flow on every checked page (/audit 9, /modules 7, /webhooks 7, /customers 7, /me 6, /access 6, /claims/queue 6, /claims/sheet 6, /claims/calendar 5, / 5, /claims/new 10); call-row tip inside the row; web 344 tests. Found: /claims/calendar logs "ResizeObserver loop completed with undelivered notifications" through the ErrorHandler when the hood sidebar is open (reproducible) -> relay) Under-the-hood coverage, EVERY page (owner 03:20): Audit log first (and link it to The Score), then Modules, Webhooks,
      Users & access / groups / overrides, Customers, Claims queue, claim page, Team and exposure, Reserve sheet, SLA calendar,
      My profile, Home, search. Each: the flow end to end (request -> handler -> DB/outbox/audit -> events), ADR/D links, live
      calls. Real code and decisions only. Two lanes by page group if it does not fit one before 13:00 (record the split here).
- [x] 11f. (merges 3149854 + 42a314a, nth-fixes, W-033; on :8080 03:53 admin: Claims cell "default (on)", AUDIT off -> cells "—" and 0 audit calls in 6 s, on -> 6 refetches; 0 console errors (the 6 audit 404s on switching off are gone); web 311 tests) /modules glitches (owner 03:19, admin, AUDIT off then on): "Last changed" for Claims stuck on "…"; a stray dark tooltip
      "/audit?limit&entityType&entityId" over the page header; the hood panel polls GET /audit while AUDIT is off (404 run): stop
      polling when the module is off. Headless: toggle AUDIT off and on, 0 console errors.
- [ ] 11g. Optional, slack only: one-line description tooltip per permission chip on My profile, from the permission catalog if it
      already has descriptions; never invent provenance (role vs override), /me does not return it. Profile page otherwise stays.
- [ ] 11a. Intake import module (brief: "claimants submit by phone or email", staff work "from shared inboxes and
      spreadsheets"). Staff-only page "Import" with three sources, all producing DRAFT claims a person reviews before they
      are created (never auto-submitted): (a) spreadsheet importer, .xlsx/.csv upload, column mapping, per-row validation
      preview using the same rules as Report an incident, import the valid rows; (b) shared inbox, upload .eml/.mbox
      exports, route by sender domain to market (a domain -> market table in config, IOptions), parse subject/body into a
      draft; (c) WhatsApp, upload a chat export .txt, parse messages into a draft with the transcript attached as a note.
      Honest labels: these are FILE IMPORTS that stand in for live connectors (no Graph/Gmail/WhatsApp Business API calls);
      say so on the page and in a D-row. Audited, idempotent (same file twice does not duplicate), tests per parser.
- [x] 11b. (merge 59e1ad6, nth-pdf, W-034, D-070: hand-written PDF 1.4, no dependency; lane full suite 780/780 + Arch 37; on merged main: build 0 warnings, OpenApi + ClaimLetterTests 31/31, Arch 37/37, web 351 tests; api+web rebuilt; `bru run letters --env compose` 10/10; headless as manager: settled CLM-PH-2026-900011 -> "Settlement letter" downloads a 5.3 KB %PDF-1.4, rejected CLM-MY-2026-900014 -> "Decision letter" 5.0 KB, 0 errors; the PDF reads correctly, marked sample; owning claimant self2 downloads the same settlement letter (%PDF-1.4, 0 errors); unrelated claimant self gets no button and the API's 404 (the browser logs the expected 404s). KNOWN LIMIT: characters outside Windows-1252 print as "?" (e.g. Thai), pinned by a test, D-070) PDF export with a fixed template: a "Settlement letter" (approved/settled claims: claimant, policy, incident,
      approved amount in the claim currency, decision reason, officer, dates, claim number, correlation id) and a decision
      letter for rejections. One fixed, branded template (QuestPDF on the API or a client-side generator; pick the one that
      keeps the bundle and licences clean, record it). Download from the claim page for staff and for the claimant on their
      own claim (scope rules apply: 404 outside scope). Label it a sample document, not a legal letter.
- [ ] 11c. (mapped 03:40: #006/#007 already done in W-024 e3885f2; #009 split by D-069: paid figure = lane nth-paid W-037, outside notification cut, claimant calendar by design, Send-question-during-restart a known limit) Backlog items still open in `docs/state-backlog.md`: #003 (retired in the 03:30 backlog prune), #006 (queue counts in one call), #007 (past info requests list), #009 (panel leftovers: a paid or
      settled figure on Team and exposure, a notification consumer for events, the Send question during an API restart).

## P1 (moved up 03:19) — agent tooling, part of the demo
- [x] 17a. (merged 66604db, nth-tooling, W-029, D-068. Verified 03:40 on the RECREATED stack (down -v, up --build at 8765f5d): role from initdb, count(*) claims = 20 as fugue_readonly; INSERT, UPDATE, CREATE TABLE, SET read_only=off + INSERT all refused by read-only transaction, BEGIN READ WRITE + INSERT refused by privilege; MCP postgres-mcp 0.3.0 over stdio: 9 tools, list_objects answers, UPDATE rejected by restricted mode; full `bru run --env compose` 105/105 requests, 127/127 tests; runbook Playwright snippet: Claims queue, 0 errors, exit 0. Not checked: `claude mcp list` approval in an interactive session. .env on this machine gained FUGUE_READONLY_DATABASE_URI (port 5433)) HARD GATE (owner 03:18/03:19): the read-only role `fugue_readonly` is created by the stack itself (initdb mount of
      `scripts/db/fugue-readonly-role.sql` or idempotent startup, whichever fits runbook 03) so it survives `down -v`;
      FUGUE_READONLY_DATABASE_URI in .env.example and runbook 00; the `.mcp.json` postgres-readonly server starts and answers a
      list-tables query; SELECT works and INSERT/UPDATE/DDL are refused by the ROLE (restricted mode is the second layer).
      Runbook `docs/runbooks/agent-tooling.md` as a demo script: MCP (agent explores data, read-only, two layers) vs CLI (psql
      via the same role, `bru run` for API regressions, the headless Playwright scripts for UI): copy-paste commands, safety
      story; linked from README and runbook 00; a D-row. Verify every command on the RECREATED clean stack, plus the full
      `cd bruno/fugue && npx bru run --env compose` green (restart api first, failure mode 25). Tick with counts.

## P2 — after the lanes merge (backlog #010)
Run 12 + 13 only if every feature lane has merged by ~10:30, else defer with a D-row. 14 (Worker host) is a deferral candidate.
- [ ] 12. Per-slice folders `Commands/ Queries/ Models/ Repos/ Helpers/` + DI extension; namespaces follow folders (IDE fix or
      `dotnet format analyzers --diagnostics IDE0130`); full suite.
- [ ] 13. Remaining hardcoded values to IOptions (page sizes kept in step with validators, webhook replay window, SLA days).
- [ ] 14. `Fugue.Worker` host for the four BackgroundServices + a building-blocks project for pipeline behaviours; ArchTests.
- [ ] 15. Optional: audit page "Sign-ins" tab over `access_audit`; reword the `[NoAudit]` comment on LoginCommand.

## P3 — wrap (by 14:00)
- [ ] 16. CHANGELOG v0.0.3 + tag covering ALL of 11-28 (D-063..D-065+, ADR-014, every merged nth lane), a docs/project-log.md entry
      for 11-28 (newest is 11-27 W-022), journal, hot-state, failure modes (add: the commit hook toolchain leaves stray root
      pnpm-lock.yaml/pnpm-workspace.yaml in worktrees; delete, never commit; consider a .gitignore line), push with tags.
      WRAP ORDER (owner 03:35): close each finished W with `scripts/spine/close-workstream.mjs` (project-log entry + CHANGELOG
      v0.0.3 line with merge shas); retire ledger rows, `git worktree remove` (after `git log main..<b>` is empty), delete merged
      branches; keep `docs/ai/lanes/<lane>-status.md` and `docs/ai/briefs/fleet-2026-11-28/` (re-copy HEADER.md if it gained
      addenda); regenerate `npm run gen:decisions` (web) and `npm run trace`, each its own commit; check docs/traceability.md
      shows every tonight W with its D/ADR and commits and count unlinked commits (target: none new); fill D-066's outcome line.
- [ ] 17. Recreate the clean demo stack (`docker compose -p fugue-demo down -v && docker compose -p fugue-demo up -d --build`),
      headless smoke as all four personas, zero console errors.
