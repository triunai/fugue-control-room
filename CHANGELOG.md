# Changelog

Tracks notable changes across migrations, API contracts, and project documentation. Append new entries at the top with the date in ISO-8601 format.

> **ADR links.** An entry that reflects a real architectural decision cites it as `(ADR-NNN)`; the ADR lives in `docs/adr/`.

## v0.0.3 — 2026-11-28
Release: the overnight and submission-morning lanes (D-064 to D-081), built by a dispatcher, an integrator and contract-bound lanes (D-066). Tag `v0.0.3`.
Features: admin creates and edits users; The Score (one request as a timeline); console select and date controls with Fill with an example; Import legacy claims (file imports to reviewed drafts, migration 13); claims search hardening; sample PDF letters; Under the hood on every page; the Changelog page; paid figure on Team and exposure; Fugue, explained; the admin tour on the claim flow; HybridCache on GET /me; k6 load scenarios; read-only Postgres role and MCP for agents; per-slice folder layout.
Fixes: Bruno collection under the CI sign-in limit; Kafka no-broker test stall (suite 12 min to 1.5 min); calendar ResizeObserver console error; /modules audit calls while AUDIT is off; group-only user edit.
Verification on the recreated stack (13:05): Fugue.Tests 864 + ArchTests 37 at the last pre-push, web 390 (Vitest), Bruno 131 requests / 157 tests (compose), four personas with zero console errors, read-only role and MCP refuse writes.
Deferred: item 13 IOptions and items 15, 11g (D-076); item 14 Worker host (D-075). Known issues: D-079 (relay cycle with the broker down), D-080 (an access token stays valid for its 15 minutes after deactivation).

## 2026-11-28 — Closed W-041: Load testing and caching: k6 scenarios, HybridCache (owner ask 11:30)
WS: WS-41
Closed: W-041 (WS-41) Load testing and caching: k6 scenarios, HybridCache (owner ask 11:30)
Outcome: k6 smoke, load and stampede scenarios with percentile reports; HybridCache on GET /me (stampede p95 382 to about 241 ms); the Bruno collection stays under the sign-in limit in CI; the Kafka no-broker test relays only its own row (suite 12 min to 1.5 min).
Commits:
- 06639d7 feat(build): merge nth-k6, k6 smoke, load and stampede scenarios with percentile reports (item 26)
- fe7845a feat(infra): merge nth-hybridcache, HybridCache for GET /me with tag invalidation (item 27)
- 766ff6f test(api): merge nth-brunoci, one sign-in per account per Bruno run (item 28)
- 1082413 fix(tests): merge nth-kafkafix, the no-broker Kafka test relays only its own outbox row (item 29)
- 7b93ac8 docs(spine): tick 19 and 20, 22 restyle, W-041 for k6 and HybridCache, ledger rows
- e429245 docs(plans): tick 26, retire nth-k6
- 38db5bd docs(plans): item 26 records the stampede p95 miss as the cache baseline
- 2547202 docs(plans): ledger rows for useredit, hybridcache and brunoci, items 28 and 29
- fca8620 docs(plans): nth-kafkafix dispatched as the critical path
- 2a98aac docs(plans): tick 28, retire nth-brunoci
- 9e455e4 docs(plans): tick 29, DEMO cites the Kafka fix, retire nth-kafkafix
- 719d968 docs(spine): D-079 and backlog #013, the broker-down relay cycle as a known issue
- 1948015 docs(spine): failure mode 37, a relaying test must settle leftover due rows first
- 0e9d56f docs(spine): D-079 and #013 corrected, the default relay batch is 20
- 7adb4d9 docs(spine): D-079 cites the harness override and the hang-kill repro
- d353661 docs(spine): tick 27, D-081 HybridCache for /me, retire nth-hybridcache
- c3a0c3f docs(docs): README counts after HybridCache, honest k6 wording in D-081
Decisions: D-079, D-081, D-064
Follow-ups: backlog #013 (relay cycle with the broker down)

## 2026-11-28 — Closed W-040: Owner asks at 11:26: tour, search bar, import UI, changelog, explained, overrides, The Score grouping, user edit
WS: WS-40
Closed: W-040 (WS-40) Owner asks at 11:26: tour, search bar, import UI, changelog, explained, overrides, The Score grouping, user edit
Outcome: Owner asks at 11:26: overrides formula explained, Fugue explained page and nav entry, the admin tour walks the claim flow, The Score grouping, Import legacy claims page, a clearer search bar, the Changelog, README refresh, user edit.
Commits:
- 5b6cc96 feat(web): merge nth-overrides, the override formula explained on Users & access (item 23)
- 3bf7d4e docs(web): merge nth-explained, the Fugue, Explained page linked from Help (item 22)
- 4ced87c docs(web): merge nth-explained restyle 805a298, the explained page in the console look
- c4c89cd feat(web): merge nth-explained nav entry, Fugue, explained in the sidebar (item 22)
- cdbbecf feat(web): merge nth-tour, the admin tour walks the core claim flow (item 18)
- b4a4376 docs(docs): merge nth-readme2, README counts and links from final main, search comment fix (item 11)
- 8c4ce09 docs(spine): W-040 and ledger rows for the 11:26 owner asks
- 35f5cb6 docs(plans): tick 23, retire nth-overrides
- 615f0d1 docs(docs): merge nth-legacydoc, legacy-sync adapter design over the import module
- d4b7c35 feat(web): merge nth-changelog, Decisions becomes the Changelog with versioning explained (item 21)
- b55270a docs(plans): tick 21 and 22, retire nth-changelog and nth-explained
- 055d763 fix(web): merge nth-scorefix, The Score groups one request's audit rows by command (item 24)
- a945500 docs(plans): tick 24, retire nth-scorefix
- a12c101 feat(web): merge nth-explained (W-040 owner asks)
- 252902d feat(web): merge nth-searchui (W-040 owner asks)
- 4d51e03 feat(web): merge nth-importui (W-040 owner asks)
- 7b93ac8 docs(spine): tick 19 and 20, 22 restyle, W-041 for k6 and HybridCache, ledger rows
- cc5f135 docs(plans): item 22 nav entry merged, retire nth-explained
- 7a2d434 docs(plans): tick 18, retire nth-tour
- 2547202 docs(plans): ledger rows for useredit, hybridcache and brunoci, items 28 and 29
- f1f7ee5 docs(docs): DEMO step 15 and a Software factory section (owner ask 12:10)
- a8d8596 docs(docs): DEMO step 15 maps Dex Horthy's factory slide to the repo (owner ask 12:11)
- 84efaaf docs(docs): DEMO names the Kafka test stall correctly
- 66db90a docs(plans): nth-readme2 ledger row, item 11 mapped
- c7b1234 feat(api): merge nth-useredit, administrators edit users with If-Match (item 25)
- cfb6e75 docs(spine): tick 25, D-080 edit users, retire nth-useredit
- 7554d00 docs(spine): D-080 states the token limit after deactivation
- 38cefb2 docs(plans): item 30, the group-only edit defect
- 0b6773f docs(docs): README Bruno row cites the verified compose run, tick 11
- fd74c0e fix(api): merge nth-editfix, a group-only user can be saved from Edit (item 30)
- cd515ac docs(spine): tick 30, D-080 says fixed, retire nth-editfix
Decisions: D-064, D-080, D-072, D-066, D-067, D-074
Follow-ups: none

## 2026-11-28 — Closed W-039: Structure refactor after the D-064 lanes: slice folders, IOptions, Worker host (backlog #010, items 12-14)
WS: WS-39
Closed: W-039 (WS-39) Structure refactor after the D-064 lanes: slice folders, IOptions, Worker host (backlog #010, items 12-14)
Outcome: Every slice uses Commands, Queries, Models, Repos and Helpers folders with namespaces following folders; OpenAPI byte-identical, suite count unchanged.
Commits:
- 8e0993b refactor(application): merge nth-refactor, one folder layout per slice (item 12)
- 2544cb6 docs(spine): W-039 for the structure refactor, nth-refactor ledger row
- 89977f2 docs(spine): D-075 defers item 14, backlog #010 part 3 stays open
- a66442d docs(spine): failure mode 36 is host sleep on battery, D-076 defers 15 and 11g
- cd9ecd3 docs(plans): item 13 deferred, the refactor missed the 11:00 condition
- ddd2ded docs(plans): backlog #010 part 2 stays open after D-076
- c77f8db docs(spine): tick 12, D-078 slice layout, Slice shape in CLAUDE.md and ARCHITECTURE.md
Decisions: D-078, D-075, D-076, D-064
Follow-ups: #010 part 2 (IOptions, D-076) and part 3 (Worker host, D-075) stay open

## 2026-11-28 — Closed W-038: Submission-morning polish lanes before the overnight list (money, prelude, onboard, readme, claimsearch, shellfix)
WS: WS-38
Closed: W-038 (WS-38) Submission-morning polish lanes before the overnight list (money, prelude, onboard, readme, claimsearch, shellfix)
Outcome: Submission-morning polish lanes before the overnight list: indicative USD view, prelude, onboarding tour, README, claims search, shell fixes.
Commits:
- 37ae182 Merge branch 'nth-money'
- e609089 Merge branch 'nth-prelude'
- 4345366 Merge branch 'nth-onboard'
- be23504 Merge branch 'nth-readme'
- 133c962 Merge branch 'nth-claimsearch': header search finds claims, scoped to the caller
Decisions: D-063, D-064
Follow-ups: none

## 2026-11-28 — Closed W-037: Paid and settled figure on Team and exposure (item 11c, backlog #009)
WS: WS-37
Closed: W-037 (WS-37) Paid and settled figure on Team and exposure (item 11c, backlog #009)
Outcome: Team and exposure shows the paid (settled) count and sum per market and in the totals; the rest of panel backlog #009 answered or cut.
Commits:
- bceb4e5 feat(api): merge nth-paid, paid and settled figures on Team and exposure (item 11c)
- 8765f5d docs(spine): W-025..W-038 for tonight, D-068 and D-069, fleet contracts archived
- f323559 docs(plans): tick 11c and 4a, retire nth-paid
Decisions: D-069, D-066, D-068
Follow-ups: none

## 2026-11-28 — Closed W-036: Decision timeline generated from the docs (item 11d)
WS: WS-36
Closed: W-036 (WS-36) Decision timeline generated from the docs (item 11d)
Outcome: The Changelog page (was Decisions): ADRs, D-rows and release notes generated from the repo docs, with how versioning works.
Commits:
- 2e0ecbf feat(web): merge nth-decisions, the decision timeline generated from the docs (item 11d)
- d4b7c35 feat(web): merge nth-changelog, Decisions becomes the Changelog with versioning explained (item 21)
- 8765f5d docs(spine): W-025..W-038 for tonight, D-068 and D-069, fleet contracts archived
Decisions: D-067, D-066, D-068, D-069, D-064
Follow-ups: none

## 2026-11-28 — Closed W-035: Under-the-hood coverage on every page (item 11e)
WS: WS-35
Closed: W-035 (WS-35) Under-the-hood coverage on every page (item 11e)
Outcome: Under the hood on every page: a flow (request to handler to DB, outbox and audit), decisions and live calls; the floating request tooltip is attached to its row.
Commits:
- 60e978c feat(web): merge nth-hooda, hood flow model, row tooltip fix, audit and platform pages (11e A)
- a4df949 feat(web): merge nth-hoodb, claims-area hood notes (11e B)
- 8ed5cce feat(web): merge nth-hoodflow, flow steps on the claims-area hood notes (11e)
- c191a05 docs(plans): 11e parts A and B merged, flow steps for the claims area still open
- ea3d79c docs(plans): tick 5, retire nth-forms, ledger row for nth-hoodflow
- 0835680 docs(plans): tick 11e and item 5's live submit, retire nth-hoodflow, nth-formsfix row
Decisions: D-064
Follow-ups: none

## 2026-11-28 — Closed W-034: Settlement and decision letters as PDF (item 11b)
WS: WS-34
Closed: W-034 (WS-34) Settlement and decision letters as PDF (item 11b)
Outcome: Sample settlement and decision letters as PDF from a hand-written PDF writer with no dependency, scoped like the claim (404 outside scope).
Commits:
- 59e1ad6 feat(api): merge nth-pdf, sample settlement and decision letters as PDF (item 11b)
- a7ab882 docs(spine): tick 11b, D-070 sample letters without a PDF dependency, retire nth-pdf
- c27b57c docs(plans): 11b claimant checks and the Latin-1 known limit
Decisions: D-070, D-064
Follow-ups: none

## 2026-11-28 — Closed W-033: Demo fixes: matrix checks, track line, claimant wording, /modules glitches (items 3a, 7a, 7b, 11f)
WS: WS-33
Closed: W-033 (WS-33) Demo fixes: matrix checks, track line, claimant wording, /modules glitches (items 3a, 7a, 7b, 11f)
Outcome: Demo fixes: roles matrix check icons, the banner track line stops at its last node, claimant wording in How a claim moves, /modules Last changed and no audit calls while AUDIT is off.
Commits:
- 3149854 fix(web): merge nth-fixes, matrix checks, track line, claimant wording, /modules (3a, 7a, 7b, 11f)
- 42a314a fix(web): merge nth-fixes follow-up, no audit refetch when AUDIT is switched off (11f)
- 9989f7c docs(plans): tick 3a, 7a, 7b; 11f back to nth-fixes for the audit 404s on toggle
- 4c42edc docs(plans): tick 11f, 7b seen live, retire nth-fixes
- a4c2b88 docs(plans): correct the 11f and 7b check times
Decisions: D-064
Follow-ups: none

## 2026-11-28 — Closed W-032: Search hardening on Meilisearch (item 10)
WS: WS-32
Closed: W-032 (WS-32) Search hardening on Meilisearch (item 10)
Outcome: Claims search answers like an officer expects (market and line synonyms, every word must match, exact numbers as phrases, highlights and facets, settings applied only when they differ); a clearer search bar with an honest keyword full-text explainer.
Commits:
- aa401bc feat(api): merge nth-searchx, claims search hardening on Meilisearch (item 10)
- 252902d feat(web): merge nth-searchui (W-040 owner asks)
- 650b2c4 docs(spine): tick 10, D-072 and D-073 for search, retire nth-searchx
Decisions: D-063, D-072, D-073, D-064
Follow-ups: Meilisearch v1.13.3+ then disableOnAttributes as a second guard

## 2026-11-28 — Closed W-031: Intake import: spreadsheet, inbox and WhatsApp file imports to drafts (item 11a)
WS: WS-31
Closed: W-031 (WS-31) Intake import: spreadsheet, inbox and WhatsApp file imports to drafts (item 11a)
Outcome: Import legacy claims: spreadsheet, inbox and WhatsApp file imports become reviewed drafts, idempotent by a claim_imports ledger (migration 13); legacy sync kept as a design.
Commits:
- f967355 feat(api): merge nth-import, spreadsheet, inbox and WhatsApp file imports to reviewed drafts (item 11a)
- 4d51e03 feat(web): merge nth-importui (W-040 owner asks)
- 615f0d1 docs(docs): merge nth-legacydoc, legacy-sync adapter design over the import module
- 3a6fb56 docs(spine): tick 11a, D-074 file imports to reviewed drafts, retire nth-import
- afc6fe9 docs(plans): 11a, the lane's second full suite was green
- 9a85ca5 docs(spine): D-077 legacy sync stays a design, backlog #012, scaling and offline section
Decisions: D-074, D-077, D-064
Follow-ups: backlog #012 (legacy sync adapter, KIV)

## 2026-11-28 — Closed W-030: Console forms: select, date picker, Fill with an example (item 5)
WS: WS-30
Closed: W-030 (WS-30) Console forms: select, date picker, Fill with an example (item 5)
Outcome: Console forms: fg-select and fg-date (with a time mode) replace every native select and date control; Fill with an example on the forms; queue filters and the calendar day jump on console controls; the calendar's ResizeObserver console notice is gone.
Commits:
- 1e3d63d feat(web): merge nth-forms, console select, date picker and Fill with an example (item 5)
- 047e565 feat(web): merge nth-datetime, fg-date time mode for overrides and audit filters (item 5)
- 5f7985e fix(web): merge nth-formsfix, queue filters and calendar day jump on console controls, e2e locator (item 5)
- 3de8f49 fix(web): merge nth-calfix, no ResizeObserver console error on the SLA calendar (17b)
- ea3d79c docs(plans): tick 5, retire nth-forms, ledger row for nth-hoodflow
- 0835680 docs(plans): tick 11e and item 5's live submit, retire nth-hoodflow, nth-formsfix row
- 9ceceab docs(plans): nth-datetime ledger row, item 5 follow-ups
- cb8c13f docs(plans): nth-datetime merged and checked, retire it
- 56fd002 docs(plans): item 5, the audit From filter cuts mid-day
- ac70cb4 docs(plans): item 5 complete with nth-formsfix, retire it
- db0eb20 docs(plans): nth-formsfix worktree recreated from main for slice 3
- 029e926 docs(plans): 17b calendar console error, nth-calfix row, nth-formsfix retired again
- 4dd58e1 docs(plans): tick 17b, retire nth-calfix
- a36df90 docs(spine): D-071 the ErrorHandler drops one benign notice, 17b live toggle check
- 81422de docs(spine): D-071 names the FullCalendar version
Decisions: D-064, D-071
Follow-ups: none

## 2026-11-28 — Closed W-029: Agent tooling: read-only role in the stack, pinned MCP, runbook (item 17a)
WS: WS-29
Closed: W-029 (WS-29) Agent tooling: read-only role in the stack, pinned MCP, runbook (item 17a)
Outcome: The compose Postgres creates the read-only role on a fresh volume; the read-only MCP is pinned and works; agent-tooling runbook; verified on a recreated stack (writes refused by the role and by restricted mode).
Commits:
- 66604db feat(infra): merge nth-tooling, read-only role in the stack, pinned MCP, runbook (17a)
- 8765f5d docs(spine): W-025..W-038 for tonight, D-068 and D-069, fleet contracts archived
- 0517bd4 docs(spine): tick 17a on the recreated stack, hot-state for the integrator role
Decisions: D-068, D-066, D-069, D-064
Follow-ups: none

## 2026-11-28 — Closed W-028: Stack dialog and the SLA calendar styling (items 8, 9)
WS: WS-28
Closed: W-028 (WS-28) Stack dialog and the SLA calendar styling (items 8, 9)
Outcome: The env pill opens a Stack dialog with the real versions (PostgreSQL 17, UUID v7 ids from the API); the SLA calendar is styled in console tokens.
Commits:
- 00e5c76 feat(web): merge nth-stack, the Stack dialog and a warm SLA calendar (overnight items 9, 8)
Decisions: D-064
Follow-ups: none

## 2026-11-28 — Closed W-027: Claim page workbook, Save to Excel, next-step copy, track explainers (items 6, 7)
WS: WS-27
Closed: W-027 (WS-27) Claim page workbook, Save to Excel, next-step copy, track explainers (items 6, 7)
Outcome: Claim page: the assessment worksheet looks like a workbook with Export (CSV) and Save to Excel; plain next-step copy; step popovers and a How a claim moves dialog.
Commits:
- 0439df5 feat(web): merge nth-claimpage, the workbook worksheet and plain claim-page copy (overnight items 6, 7)
Decisions: D-064
Follow-ups: none

## 2026-11-28 — Closed W-026: The Score, one correlation id as a timeline (item 4)
WS: WS-26
Closed: W-026 (WS-26) The Score, one correlation id as a timeline (item 4)
Outcome: The Score: GET /audit/trace/{correlationId} reads one request's audit, outbox and delivery rows and the console draws them as a timeline; one request's audit rows are grouped into one step.
Commits:
- dacfb06 feat(web): merge nth-score, The Score per-correlation inspector (overnight item 4)
- 055d763 fix(web): merge nth-scorefix, The Score groups one request's audit rows by command (item 24)
- f323559 docs(plans): tick 11c and 4a, retire nth-paid
Decisions: D-064, D-069
Follow-ups: backlog #011 (index on outbox_messages.correlation_id)

## 2026-11-28 — Closed W-025: Admin creates users (overnight item 3)
WS: WS-25
Closed: W-025 (WS-25) Admin creates users (overnight item 3)
Outcome: Administrators create users with an initial password and roles (POST /users, USERS_MANAGE, no role escalation) and edit them (PUT /users/{id} with If-Match, self-lockout guard); a group-only user can be saved from Edit. Bruno-created users are claimants, not staff.
Commits:
- e182dbb feat(api): merge nth-uam, administrators create users (overnight item 3)
- c7b1234 feat(api): merge nth-useredit, administrators edit users with If-Match (item 25)
- fd74c0e fix(api): merge nth-editfix, a group-only user can be saved from Edit (item 30)
- 92be068 test(api): merge nth-brunofix, Bruno-created users are claimants, not staff
- 3c477d3 docs(spine): nth-brunofix merged and retired, hot-state trap note
Decisions: D-064, D-080
Follow-ups: none

## v0.0.2 — 2026-11-27
Release: the claims platform plus the owner's polish round, frozen for the walkthrough (D-062). Tag `v0.0.2`; the timed claims build is `claims-window-start..claims-window-end`.
Claims platform: Claim aggregate with a guarded state machine (Submitted, UnderReview, InfoRequested, Approved, Rejected, Settled), claimant timeline and info requests, claimant scope as 404 never 403, If-Match on every action (428/412) checked before the state machine, a required Idempotency-Key on submit (422 on a reused key), status + timeline + audit + outbox in one transaction, claim.* events to signed webhooks and an optional Kafka publisher (off by default), workload, exposure per currency, claim counts and past questions; one Angular console for claimant, officer, manager and admin with persona landings, the two-track claim record with a next-step card, and Team and exposure.
Owner polish: console shell adaptation with breadcrumbs (D-060, corrected: adapted, not ported), reviewer tour, roles x permissions matrix, Univer reserve sheet and assessment worksheet with Open in Excel, SLA calendar, Meilisearch audit search, the "Under the hood" panel (live API calls, how the API does it, guards with the real enforcing code, tests per page, ADRs first), Stryker/PICT/k6.
Fixed in the release round: blank console for a returning signed-in user (NG0203, 284a191), Open in Excel in the production bundle (e66e082), Team and exposure and the landings refresh live (b451cd7), claim events reach the dev sink in Docker (1688126), one definition of "open with no reserve", exposure cells open the queue, sign-in lists the four personas (03448e5).
Verification: ArchTests 37, Tests 740 (Testcontainers Postgres), Bruno 101 requests / 123 tests on the Docker stack, web 218 (Vitest), lint and build clean; live browser checks signed in and after a reload with zero console errors.
Decisions: D-060, D-061, D-062; failure modes 33 and 34. The freeze-round commits cite `Refs: D-062` and no W thread (W-022 was closed at release), so commit-link-check lists them as unlinked; this entry is their home. Reviews: docs/reviews/fable/2026-11-27-interview-readiness.md, docs/reviews/fable/2026-11-27-panel-stress-test.md.

## 2026-11-27 — Closed W-022: Post-core quality wave and nice-to-haves (lands together after the claims build)
WS: WS-22
Closed: W-022 (WS-22) Post-core quality wave and nice-to-haves (lands together after the claims build)
Outcome: Post-core wave landed: AI harness write-up, Stryker/PICT/k6, console shell adaptation (D-060 correction: adapted, not ported), reviewer tour, roles matrix, Univer reserve sheet and assessment worksheet, SLA calendar, Meilisearch audit search, Fable readiness review (D-061) and its 15 changes, owner polish round, under-the-hood sidebar, optional Kafka publisher (D-062). Released as v0.0.2.
Commits:
- 7b9605b docs(spine): wrap mid-window; close W-018; seed the headless continuation
- 2b9758e docs(spine): wrap the headless claims session
- 626d054 docs(trace): regenerate traceability index
- 84dac9b docs(reviews): repo-relative paths in the W-018 Codex review
- 52a61b5 docs(trace): regenerate traceability index
- b875f49 docs(plans): nice-to-haves wave briefs; D-058 to D-060
- f00e2d4 docs(docs): journal the publish and the nice-to-haves wave; ledger rows
- 3ff8ba7 fix(web): the nav scrolls above the pinned account card
- afc0dae docs(plans): brief for the claim record and team pages lane
- e774faf docs(docs): journal the owner's usability critique; ledger row for nth-claimui
- c58eb38 feat(web): console shell from the reference design
- c3f0918 test(domain): mutation testing on three targets
- fa98e36 feat(web): reviewer tour
- f0b5409 feat(web): claim record banner and next step card
- 853fab0 test(application): pairwise claim action cases
- 714ebdd feat(web): roles and permissions matrix
- 3391e13 feat(web): team and exposure page
- a9e1610 docs(plans): briefs for the review follow-up and Kafka lanes
- 1f35e66 docs(docs): service boundaries, sync and async, demo script; Fable review
- 2ff5859 refactor(web): split record and team styles out of claims.css
- f856000 feat(web): a landing page per persona
- 8fedec9 feat(web): route the landing and team pages, retire workload and exposure
- 96d9b5f feat(messaging): Kafka publisher keyed by aggregate id
- d843a4f feat(web): one Team and exposure nav item
- 1649506 test(messaging): Kafka publisher against a real broker
- 1f98f3a build(infra): optional Kafka broker in compose
- 5a6b39e test(perf): k6 scripts for the claims API
- 122beb0 feat(web): end the money track after a rejection and mark the worksheet slot
- 2193122 test(web): screenshots of the record, team page and landings
- 337ac62 docs(docs): nth-claimui lane status
- 42ee9fc docs(docs): how the AI harness runs this repo
- d293b7a docs(docs): nth-quality status trailer note
- 4e9b0b0 Merge branch 'nth-quality': mutation, pairwise, load, harness doc
- d708a8f feat(web): milestone steppers and a stricter next step card
- 3ac0a3e feat(web): matrix step in the admin tour, shell e2e and screenshots
- dbe7b7c docs(runbooks): watching claim events on Kafka
- 02e3b07 docs(docs): nth-shell lane status
- e596429 feat(web): the queue reads its filters from the URL and opens on Open
- 960238c feat(web): reserve review sheet
- fe7f2a0 test(tests): the pairwise test's repository fake implements the new reads
- 6773d48 feat(web): tour by page key, Help entry, Starter example nav group
- e975272 docs(docs): nth-shell lane status addendum
- 0824bf8 feat(web): claims SLA calendar
- f24261a feat(web): team and exposure in the order a manager asks
- f6cac4b feat(web): nav entries and routes for the reserve sheet and the SLA calendar
- 8b42690 feat(web): landings answer what to do next
- a1feeac docs(web): nice-to-haves and their bundle cost
- 69f215b feat(web): claims screens refetch on focus and every 15 s
- 4be6dc0 docs(web): final bundle numbers from the last build
- 06806fa docs(docs): nth-grid lane status
- ec7bf0c Merge branch 'nth-shell': console shell, reviewer tour, roles matrix
- 615a003 fix(web): restore the team page styles lost in the style split
- ce006da test(web): refresh screenshots for the milestone steppers and team page
- aae0fde docs(docs): nth-claimui lane status, addendum round
- 59e7d77 docs(plans): brief for the wire-up lane
- 94b4921 feat(web): wire the tour hook and show past questions on the record
- 8d298ef docs(docs): nth-claimui lane status, round 3
- 3adfa43 feat(web): roles matrix reads the API
- 29f6ad8 Merge branch 'nth-claimui': claim record, next step, team page, landings
- 3e35ef1 feat(web): open the reserve sheet in Excel
- 71dbac4 feat(web): claim assessment worksheet
- 09dc7e5 docs(web): Excel export, assessment worksheet and the final bundle numbers
- 5a2eff0 fix(web): manager tour points at the team page
- 7605975 docs(docs): nth-wire lane status
- ae93069 Merge branch 'nth-grid': reserve sheet, SLA calendar, assessment worksheet
- 8d486c0 feat(web): the assessment worksheet sits on the claim record
- 268c1e2 Merge branch 'nth-claimui' round 3: tour targets and past questions
- 3bca72e Merge branch 'nth-wire': users list, roles matrix, audit search in the console
- 97b6ed8 docs(docs): journal the integration wave; failure mode 32
- 8a103cc fix(web): the roles matrix reads as one grid in one type family
- f0d0443 docs(spine): hot-state during the nice-to-haves wave; ledger row for nth-polish
- b0b3662 docs(spine): record the owner's feature freeze and v0.0.2 in hot-state
- 267fd79 docs(spine): backlog the under-the-hood panel design (#008)
- eb3ce64 docs(spine): backlog #008 as a minimisable sidebar
- 3f633b1 feat(web): Univer sheets with the full toolbar and formula bar
- 94aa8ed fix(web): one type family in breadcrumbs and content group headers
- 8281dfc test(web): screenshots of the sheets and the matrix
- e330ba2 docs(docs): nth-polish lane status
- c193a2e fix(web): next step shows N days past SLA instead of Day 18 of 14
- d6bd75a feat(web): Activity is a collapsed disclosure remembered per session
- 7889651 feat(web): show the console version in the sidebar footer
- d7941ad test(web): screenshots of the claim record
- 30577fc fix(web): a day on the SLA calendar opens the claims due that day
- ac68687 docs(spine): owner approved the under-the-hood sidebar (layout A)
- 6493712 docs(spine): ledger row for nth-hood
- fdf7ecc feat(web): audit search in the shell header
- 746f9bb docs(docs): nth-polish lane status addendum
- 03d7002 Merge branch 'nth-polish': the owner's polish round
- bac8503 feat(web): live API call log for the under-the-hood panel
- e2d6d16 docs(spine): retire the nth-polish ledger row
- 2899045 feat(web): under-the-hood sidebar, minimisable
- 89e15b3 feat(web): under-the-hood notes for the demo pages
- 4161452 test(web): hood screenshots
- e0e5e80 docs(docs): nth-hood lane status
- 80a1fbe Merge branch 'nth-hood': the under-the-hood sidebar
- 29c3c32 docs(spine): context-cap wrap; blank console fix in flight; seed prompt
- 284a191 fix(web): resolve Router before the await in guestGuard
Decisions: D-054, D-055, D-058, D-059, D-060, D-061, D-062, D-057
Follow-ups: Faithful port of the reference shell (backlog #003, reopened); owner's note on trimming Univer and the calendar for memory-capped deploys.

## 2026-11-27 — Closed W-024: Claims follow-ups: counts endpoint, open-first queue, past questions
WS: WS-24
Closed: W-024 (WS-24) Claims follow-ups: counts endpoint, open-first queue, past questions
Outcome: GET /api/v1/claims/counts feeds the KPI strip and tabs in one call, the queue opens on open statuses, and past info requests are listed on the claim record (backlog #006, #007); Bruno requests added.
Commits:
- b875f49 docs(plans): nice-to-haves wave briefs; D-058 to D-060
- e3885f2 feat(api): claim status counts and the past questions list
- afc0dae docs(plans): brief for the claim record and team pages lane
- e925e63 test(api): Bruno requests for counts, questions, users and roles
- 3fde3bf docs(docs): nth-api lane status
- 6e202c4 Merge branch 'nth-api': claim counts, past questions, users and roles
- f0b5409 feat(web): claim record banner and next step card
- 3391e13 feat(web): team and exposure page
- a9e1610 docs(plans): briefs for the review follow-up and Kafka lanes
- 2ff5859 refactor(web): split record and team styles out of claims.css
- f856000 feat(web): a landing page per persona
- 8fedec9 feat(web): route the landing and team pages, retire workload and exposure
- d843a4f feat(web): one Team and exposure nav item
- 292e217 feat(domain): a reserve change is an event
- 3417421 docs(reviews): Codex review of the nth-api lane
- 122beb0 feat(web): end the money track after a rejection and mark the worksheet slot
- 2193122 test(web): screenshots of the record, team page and landings
- 337ac62 docs(docs): nth-claimui lane status
- e070a41 feat(application): exposure counts unreserved claims; workload shows idle officers
- 9fb4a5d feat(api): queue drill-down filters
- 04e489c feat(infra): demo seed subscribes the dev sink to claim events
- d708a8f feat(web): milestone steppers and a stricter next step card
- 08ea1ce test(api): Bruno requests for the drill-down filters and the new report fields
- e596429 feat(web): the queue reads its filters from the URL and opens on Open
- a42d56c docs(docs): nth-api2 lane status
- f24261a feat(web): team and exposure in the order a manager asks
- 8b42690 feat(web): landings answer what to do next
- 69f215b feat(web): claims screens refetch on focus and every 15 s
- 3ea179b Merge branch 'nth-api2': review follow-ups (events, exposure, drill-down)
- 615a003 fix(web): restore the team page styles lost in the style split
- ce006da test(web): refresh screenshots for the milestone steppers and team page
- aae0fde docs(docs): nth-claimui lane status, addendum round
- 94b4921 feat(web): wire the tour hook and show past questions on the record
- 8d298ef docs(docs): nth-claimui lane status, round 3
- 29f6ad8 Merge branch 'nth-claimui': claim record, next step, team page, landings
- 268c1e2 Merge branch 'nth-claimui' round 3: tour targets and past questions
Decisions: D-053, D-061, D-058, D-059, D-060
Follow-ups: none

## 2026-11-27 — Closed W-021: Audit full-text search
WS: WS-21
Closed: W-021 (WS-21) Audit full-text search
Outcome: Audit search shipped on Meilisearch, not Postgres tsvector: the owner reversed the earlier AI-only rejection (D-058). An indexer feeds it from the append-only audit_log; GET /api/v1/audit/search (AUDIT_READ) answers 503 when the service is down; search box on the audit page and in the shell header.
Commits:
- 7b9605b docs(spine): wrap mid-window; close W-018; seed the headless continuation
- b875f49 docs(plans): nice-to-haves wave briefs; D-058 to D-060
- 78a5b57 docs(runbooks): audit search
- 2bc5cd3 feat(infra): Meilisearch audit index and indexer
- addaebe feat(api): audit search endpoint
- 7e2e4c6 docs(docs): nth-search lane status
- 4ed4819 Merge branch 'nth-search': Meilisearch audit search
- c58817b fix(infra): the quickstart fills the Meilisearch master key
- 59e7d77 docs(plans): brief for the wire-up lane
- 8a87a5f feat(web): audit search box
- d551ca3 docs(web): screenshots for the users list and audit search
- 7605975 docs(docs): nth-wire lane status
- 3bca72e Merge branch 'nth-wire': users list, roles matrix, audit search in the console
Decisions: D-058, ADR-010, D-059, D-060
Follow-ups: none

## 2026-11-27 — Closed W-019: Users list endpoint for the Access screen
WS: WS-19
Closed: W-019 (WS-19) Users list endpoint for the Access screen
Outcome: GET /api/v1/users (keyset, search, USERS_READ) and the Access screen's users list with the roles x permissions matrix; contract snapshot and generated client refreshed.
Commits:
- 3a559aa docs(spine): wrap 2026-11-27, decisions D-045..D-048, handoff and seed prompt
- b875f49 docs(plans): nice-to-haves wave briefs; D-058 to D-060
- 9978c1f feat(identity): users list and roles with their permissions
- e925e63 test(api): Bruno requests for counts, questions, users and roles
- 3fde3bf docs(docs): nth-api lane status
- 6e202c4 Merge branch 'nth-api': claim counts, past questions, users and roles
- 3417421 docs(reviews): Codex review of the nth-api lane
- 59e7d77 docs(plans): brief for the wire-up lane
- 90aaaa3 docs(spine): ledger row for nth-wire
- 886384c feat(web): users list on the access screen
- d551ca3 docs(web): screenshots for the users list and audit search
- 7605975 docs(docs): nth-wire lane status
- 3bca72e Merge branch 'nth-wire': users list, roles matrix, audit search in the console
Decisions: ADR-008, ADR-012, D-058, D-059, D-060, D-045, D-046, D-047, D-048
Follow-ups: none

## 2026-11-27 — Closed W-007: S5 REST hardening: idempotency, versioning, rate limits, cache, telemetry
WS: WS-07
Closed: W-007 (WS-07) S5 REST hardening: idempotency, versioning, rate limits, cache, telemetry
Outcome: S5 essentials: idempotency keys with stored-response replay (ADR-013, two Codex reviews; required on claim submit), one-command Docker stack, CI workflow (unrun: no remote), reviewer runbook, Bruno compose env. Readability pass from the owner's review: runtime SQL in 40 embedded .sql files with tested caller headers and fitness rules that read them, a read-only DB role and restricted Postgres MCP config, db:script export (D-054); one file per concept and list filter records (D-055, backlog #005). Behaviour proven unchanged: OpenAPI snapshot byte-identical, suite green.
Commits:
- 451022d docs(plans): idempotency-key plan and ADR-013; keep the Angular console (D-049)
- bd5de50 build(docker): API image with an EF migrations bundle
- 731751d build(web): console image served by nginx with /api proxied
- 38602b0 build(compose): one-command stack (postgres, migrate, api, web)
- 1d3cde3 docs(runbooks): runbook 10 for the one-command stack
- 1ca01f9 fix(infra): compose no longer requires the signing key to start postgres alone
- a2a9746 docs(plans): idempotency plan revision 2 after the Codex review; D-051 audit import
- 793f6f8 feat(web): modules page as a feature-switch table with a toggle and last-changed
- 7c8d082 docs(spine): KIV backlog items for the guided tour and the console shell
- 785124a feat(web): shell with breadcrumbs, collapsible groups and a bottom user card
- f83e5c3 build(compose): Postgres writes file logs for pgAdmin and slow-query tracing
- 5302c46 fix(web): keep the shell user card and footer pinned in the viewport
- bc0eac7 docs(spine): KIV backlog item for a role x permission matrix
- 1d92c88 feat(persistence): idempotency_keys table
- ce0305b feat(api): Idempotency-Key filter with stored-response replay
- 0fe9814 feat(api): document Idempotency-Key in the contract
- 5104945 feat(web): send an Idempotency-Key on create
- dde6eb6 fix(api): idempotency review fixes (no-store, blank key, fitness rule, contract)
- d9b67fb fix(web): fix the Idempotency-Key and body per create submission
- d0eee32 docs(reviews): file the Codex diff review of the idempotency store
- c0e19c4 chore(ci): allow the scopes this repo already uses in commitlint
- 8c6d5eb refactor(identity): name the JWT claim types once and rename the current-user adapter
- a277d4a docs(spine): backlog the list-filter binding cleanup from the owner's review
- 5654760 docs(plans): briefs for the readability pass lanes
- ce403cb docs(spine): ledger rows for the readability lanes
- ef4e2e4 docs(docs): one file per concept in CLAUDE.md; claims personas in runbook 00
- 78f8fa9 refactor(api): list filters bind one query record
- ac4d231 refactor(domain): typed ids and small aggregate types in one file per concept
- f01fadd refactor(application): ports grouped by concern
- a9081c9 refactor(persistence): runtime SQL in embedded .sql files
- 2108c1d docs(docs): readability-files lane status
- 07fd013 test(persistence): every SQL script referenced and present
- 60ea816 Merge branch 'rd-files': one file per concept and list filter records
- a182737 docs(spine): typed-id recipe points at Ids.cs; close backlog #005
- f9deb05 build(infra): idempotent migration script and a read-only database role
- 2e73407 docs(docs): readability-sql lane status
- 89db40d test(arch): fitness rules read the .sql files
- 1658a9f Merge branch 'rd-sql': runtime SQL in embedded .sql files
- e3cec29 docs(docs): journal the readability pass; SQL files in ARCHITECTURE.md
Decisions: D-049, D-050, D-054, D-055, ADR-013, D-051
Follow-ups: API versioning with Deprecation/Sunset, HybridCache on /me permissions, OpenTelemetry traces (new W-023)

## 2026-11-27 — Closed W-020: Claims platform: the timed build window
WS: WS-20
Closed: W-020 (WS-20) Claims platform: the timed build window
Outcome: Claims platform built inside the timed window (tag claims-window-start): Claim aggregate with a guarded state machine, append-only claimant timeline, info requests, claimant-only visibility (404), ten endpoints with If-Match and a required Idempotency-Key on submit, workload and exposure per currency, seed, Bruno claims folder, and the Angular claimant/staff/manager screens. Codex seam review: 3 findings accepted and fixed, 2 declined with reasons. Verified on main: build 0 warnings, ArchTests 37 + Tests 623, web 62, Bruno 82/82 requests and 104/104 tests against the Docker stack.
Commits:
- dd46eca chore(spine): open the claims build window (plan, W-020, D-052, D-053)
- a4ac736 docs(spine): hot-state resume point during the claims window
- c054c63 docs(docs): architecture guide and how-we-work guide for the core doc pack
- 40ee1a3 docs(spine): decisions on SQL files, a read-only DB MCP and file consolidation
- 1fdc33b docs(docs): point architecture guide at D-054 and D-055 for SQL and file layout
- c2154d1 docs(identity): say PBKDF2, which is what the code uses, not Argon2id
- 3b4dcc5 ci: GitHub Actions pipeline and a reviewer start-here runbook
- 8d77574 fix(tests): Bruno compose environment so the webhook sink is reachable from the API container
- 7b9605b docs(spine): wrap mid-window; close W-018; seed the headless continuation
- 4a0ea9e docs(trace): regenerate traceability index
- 6754d1f feat(domain): Claim aggregate with its state machine
- 641f0e8 docs(spine): hot-state: claims backend lane finished, rebase pending
- 9571880 feat(persistence): claims, timeline and info requests
- 4b7186c feat(api): claims contract
- a9d2d1c fix(application): claim handling checks the ETag before the state machine
- b851811 feat(application): claims workload and exposure
- 0d89790 feat(infra): seed claims demo data
- 3142577 test(api): Bruno claims collection
- 9c2a79a feat(api): idempotent claim submit and a flat list filter
- 506fc47 docs(ai): claims-core lane status
- 4f33326 docs(spine): hot-state: claims backend rebased on main (506fc47)
- 8718e07 docs(docs): journal the headless continuation start
- 74522f0 docs(reviews): Codex claims seam review and its disposition
- 885f089 feat(web): claims client and claimant screens
- 6cf5e0f docs(docs): CLAIMS.md and the ADR-013 required-key amendment
- 7fa63d7 fix(persistence): a claim command always runs the version-guarded update
- e3d0cae docs(docs): refresh how-we-use-ai against what this repo now evidences
- 6bcd7ab feat(web): claims queue and staff claim detail
- 7bc4280 fix(api): claim submit requires an Idempotency-Key
- fab8a1c feat(web): workload and exposure
- bbf9b90 test(api): Bruno and runbook send the submit key
- a220919 docs(docs): claims-fix lane status
- abf1558 test(web): claims smoke and screenshots
- 5de17a7 docs(ai): claims-web lane status
- ce78360 docs(docs): README for the claims submission
- 15a4635 Merge branch 'claims-core': the claims platform
- f26fd14 docs(docs): journal the claims merge; failure modes 30 and 31
- 18c98d4 fix(api): claims list query parameters are camelCase like every other list
Decisions: D-052, D-053, D-056, D-057, ADR-013, D-055, D-054
Follow-ups: claims counts endpoint and open-first queue (backlog #006); past info requests list (#007); event-fed exposure projection, Kafka publisher and consumer authorizer, settlement saga, attachments, property fields (CLAIMS.md section 8)

## 2026-11-27 — Closed W-018: Import the owner's earlier audit-logging design and harden audit
WS: WS-18
Closed: W-018 (WS-18) Import the owner's earlier audit-logging design and harden audit
Outcome: Swept the owner's earlier audit package against W-012 and ported four ideas: 401 challenges recorded in access_audit with a fixed-bucket per-IP cap; failure rows carry error_type, an audit-safe error_message (field names or exception type, never handler text) and duration_ms; a Fugue.Audit meter plus a structured fallback log for lost writes; a fitness test that every audited command has a named action. Nine ideas rejected (async writer, sampling, body capture, regex masking, XFF trust, header attribution, DELETE purge, dual writes, search index). Two Codex reviews (port: 2 HIGH, 1 MED, 1 LOW) all fixed and pinned.
Commits:
- 3a559aa docs(spine): wrap 2026-11-27, decisions D-045..D-048, handoff and seed prompt
- b16305e feat(persistence): failure detail columns on audit_log
- 0f42b77 feat(application): failure rows carry error type, message and duration
- 1beae91 feat(observability): audit write-failure meter and structured fallback log
- def832a feat(identity): record 401 challenges in access_audit with a per-IP cap
- d3083b0 test(arch): every audited command has a named action
- a2a9746 docs(plans): idempotency plan revision 2 after the Codex review; D-051 audit import
- 0cb8623 fix(application): audit failure rows never store handler message text
- 9267ef7 fix(api): bound the 401 audit limiter to a fixed bucket array
- 0da2242 fix(api): an audit-side cancellation keeps the 401
- e2bd64b docs(api): refresh the audit error message description in the OpenAPI snapshot
Decisions: D-048, D-051, D-050, ADR-013, D-045, D-046, D-047
Follow-ups: Audit full-text search (Postgres tsvector, q= on GET /audit); success rows could carry elapsed time

## 2026-11-27 — Closed W-011: Project README
WS: WS-11
Closed: W-011 (WS-11) Project README
Outcome: README written by a research agent (Google README guide applied; other sources unreachable), stale status lines corrected by the lead before commit. Status and highlights need a refresh whenever a batch lands.
Commits:
- ee7f62e docs(readme): project README
Decisions: none
Follow-ups: none

## 2026-11-26 — Closed W-017: Fugue Console: Angular admin console for the API (web/)
WS: WS-17
Closed: W-017 (WS-17) Fugue Console: Angular admin console for the API (web/)
Outcome: Fugue Console shipped in web/: Angular 22 (signals, OnPush, lazy routes), SignalStore client state, TanStack Query server state, Vellum tokens. Screens: sign-in, shell, customers (keyset Load more, edit drawer, 412 banner with replay), audit (filters, diff, correlation chips), modules, webhooks (one-time secrets, deliveries, live dev sink), users and access, profile. 18 unit tests, 5 Playwright e2e against the API; review findings fixed.
Commits:
- 2051ac9 docs(spine): open W-017 for the Fugue Console web client
- 7d429aa feat(web): scaffold Fugue Console with Vellum tokens, shell, auth and interceptors
- 0b5bb59 test(web): pin ETag/If-Match, problem mapping, single-flight refresh and hasPermission
- 35580b2 feat(web): customers screen with keyset Load more, edit drawer and the 412 banner
- b71b30b fix(spine): stop npm lockfile package names tripping the secrets scan
- d0dd12c build(web): generate API models from the OpenAPI snapshot and commit the lockfile
- 242b5a8 feat(web): audit log with filters, day timeline and a field-level diff panel
- 2717242 feat(web): modules screen with configuration, switch and effective state per module
- c755fb0 feat(web): webhooks screen with one-time secrets, deliveries and a live dev sink
- dcc968d feat(web): users and access (overrides, groups) and the self-service profile
- 3cead85 test(web): Playwright smoke for the 412 flow against the live API, plus screenshots
- e8ba8e5 docs(web): console architecture README and runbook 09 with the six-minute demo path
- 21ebd0b fix(web): close the console review findings on token scope, refresh races and 412 replay
- e938e2e docs(web): refresh the console screenshots after the review fixes
Decisions: ADR-008, ADR-011, ADR-009, ADR-010
Follow-ups: Runbooks index row for 09-console.md (docs/runbooks/README.md); a users directory endpoint for the Access screen; BFF cookie for the refresh token (web/README.md); regenerate traceability.

## 2026-11-26 — Closed W-016: OpenAPI contract, CORS for the console, GET /me
WS: WS-16
Closed: W-016 (WS-16) OpenAPI contract, CORS for the console, GET /me
Outcome: The API now publishes its contract: a Swashbuckle OpenAPI v1 document pinned by the committed snapshot docs/api/openapi.v1.json (test-gated, refreshed with npm run api:openapi), Swagger UI only in Development, a named CORS policy for the console (exact origins, no credentials, exposed and allowed headers tested including a preflight), and GET /api/v1/me (identity, roles, groups, effective permissions, enabled modules). Permission literals are checked against the seeded catalog. Codex review: no HIGH or MED, one LOW fixed. dotnet test 406 green; Bruno live 49 requests, 67 tests green.
Commits:
- 7daf73b docs(spine): open W-016 for the OpenAPI contract, CORS and /me
- bc1783c feat(api): openapi contract snapshot, cors for the console, /me
- ac9845d fix(api): reject a fragment in a CORS origin and record the Codex W-016 review
Decisions: D-043, ADR-012, D-042, D-044
Follow-ups: W-017 console builds against the snapshot; W-007 adds idempotency keys and the rest of S5 (the contract already allows the Idempotency-Key header)

## 2026-11-26 — Closed W-006: S4 transactional outbox and signed webhooks
WS: WS-06
Closed: W-006 (WS-06) S4 transactional outbox and signed webhooks
Outcome: Transactional outbox and signed webhooks shipped (ADR-011): outbox rows in the same transaction, a two-stage leased dispatcher with the outbox as the single retry authority, SSRF-guarded sender, encrypted subscription secrets, a Development-only sink, runbook 07 and Bruno. The Codex review (1 HIGH, 4 MED) is fixed and pinned. dotnet test 363 green; Bruno 47 requests, 64 tests green.
Commits:
- aa51ca4 feat(persistence): outbox and webhook tables with the AddOutboxWebhooks migration
- 893a345 feat(persistence): capture domain events as outbox rows in the same transaction
- 01e0305 feat(api): webhook subscriptions with encrypted secrets and an SSRF guard
- dfbec7f feat(api): signed webhook dispatcher, guarded sender, deliveries API and dev sink
- ad9df38 docs(api): webhooks runbook 07, Bruno webhooks folder and S4 journal entries
- 596269e fix(api): close the Codex S4 review findings on delivery fencing, leases, capture and sink
- f34d0b4 test(api): list the WEBHOOKS module in the Bruno modules check
Decisions: ADR-011, D-041, D-038, D-037, D-039, D-040
Follow-ups: Outbox retention and archival (backlog #001, deferred); a broker adapter behind the publisher seam if ever needed

## 2026-11-26 — Closed W-015: Codex W-013 findings: no-op override PUT skips the xmin check, email ceiling charged late
WS: WS-15
Closed: W-015 (WS-15) Codex W-013 findings: no-op override PUT skips the xmin check, email ceiling charged late
Outcome: Both Codex W-013 MED findings fixed with regression tests shown RED first: a no-op override PUT now runs the version-guarded UPDATE (412 on a racing change), and the per-email login ceiling reserves a slot before verification and refunds on success. dotnet test 363 green.
Commits:
- 224b5b2 fix(identity): version-guard no-op override PUTs and reserve the email ceiling up front
Decisions: D-035, D-036
Follow-ups: none

## 2026-11-26 — Closed W-013: S3 review follow-ups
WS: WS-13
Closed: W-013 (WS-13) S3 review follow-ups
Outcome: Email uniqueness is per owner (migration CustomerEmailPerOwner) and login is limited per (IP, email) plus a loose per-email ceiling, both owner calls. Override changes need If-Match (migration OverrideConcurrencyToken); group self-target, 23505 races, tenant is_active, fail-closed module config, access_audit clipping and HMAC subkey fixed and pinned; module PATCH audit was already covered by W-012. Recorded not fixed: raw user ids in createdBy, anonymous access_audit growth. Suite 260 green, Bruno 40/56.
Commits:
- 863d4b8 fix(persistence): make email unique per owner, closing the create-path oracle
- 977821f fix(api): key the login limiter on (IP, email) plus a loose per-email ceiling
- feef681 fix(identity): require If-Match to change or delete an existing permission override
- 3766cbc fix(identity): close the remaining S3 review findings on admin and audit edges
Decisions: D-034, D-035, D-036, ADR-008
Follow-ups: Cross-family review of the W-013 auth and migration changes (not yet done).

## 2026-11-26 — Closed W-012: Rich append-only audit log
WS: WS-12
Closed: W-012 (WS-12) Rich append-only audit log
Outcome: Rich append-only audit_log: who, what, field-level old/new, outcome and error code, request and trace ids, in the SAME transaction as the change (EF SaveChanges interceptor plus IAuditTrail for Dapper writes), one failure row per failed command (AuditBehavior), write-time masking and size bound, trigger blocks UPDATE/DELETE/TRUNCATE, GET /api/v1/audit behind AUDIT_READ (admin) with keyset paging. Codex review: 1 HIGH + 3 MED, all fixed and pinned (fixes not re-reviewed). Suite 222 green, Bruno 40 requests / 56 tests.
Commits:
- 67c7d95 feat(persistence): audit_log table, append-only trigger and AUDIT_READ seed
- 8d8f77a feat(persistence): capture audit rows from EF and Dapper writes and failed commands
- beba6dd feat(api): audit log query endpoint with keyset paging behind AUDIT_READ
- f38e3bf docs(audit): ADR-010, decisions, runbook 06, failure modes and deferred items
- c35a022 fix(persistence): audit forced writes, detach failed-save rows, independent failure row
Decisions: D-027, D-028, D-029, D-030, D-031, D-032, D-033, ADR-010
Follow-ups: W-013 gains the missing OverrideAdmin concurrency check; deferred (docs/archive/deferred.md): audit retention and partitioning, non-owner DB role, tenant filter on audit reads, auditing of reads

## 2026-11-25 — Closed W-014: Commit traceability index
WS: WS-14
Closed: W-014 (WS-14) Commit traceability index
Outcome: Refs: now takes several ids per trailer and resolves closed W via log, archive or CHANGELOG. trace-index.mjs generates docs/traceability.md (commit <-> W <-> ADR <-> D) from git log and the committed docs; npm run trace, a pre-push advisory stale check, and wrap-session wiring added. close-workstream now records D/ADR ids from a W's commits. test:scripts 83 green.
Commits:
- 5704fc6 docs(spine): open W-014 commit traceability index on the board
- e0b8f3c feat(spine): multi-id Refs gate and a generated commit traceability index
Decisions: D-013, ADR-007
Follow-ups: none

## 2026-11-25 — Closed W-005: S3 identity: JWT auth, lean UAM v3, module management
WS: WS-05
Closed: W-005 (WS-05) S3 identity: JWT auth, lean UAM v3, module management
Outcome: JWT HS256 login with refresh rotation and family reuse detection, per-request uncached effective permissions (roles, groups, overrides; deny > allow > role), ownership scoping with 404, module management (config kill switch + registry), tenants, groups, grant_audit and access_audit, 204 mutations. Codex rejected the first plan (10 findings, all accepted); Codex and a Sonnet reviewer reviewed the build (1 HIGH fixed and pinned). Suite 171 green, Bruno 32 requests / 45 assertions.
Commits:
- ae7fbb8 docs(spine): close W-009 into the project log and CHANGELOG
- b4b0dea docs(plans): S3 identity plan
- f6363b8 docs(plans): revise S3 per cross-family review
- 580100e feat(identity): UAM v3 schema, seeded catalog and base-entity contract
- e1b54f3 feat(auth): JWT login, per-request permissions, ownership scoping, 204 mutations
- 142158a feat(identity): module management with a config kill switch and a runtime registry
- aae8b27 feat(identity): groups with audited membership changes, and a tenant column
- 31c91c7 feat(identity): refresh tokens with rotation, family reuse detection and logout
- b7ac795 feat(identity): access_audit events for login, refresh, 403 and disabled modules
- 98f1491 feat(identity): permission overrides with expiry, audited in the same transaction
- 8a24bd7 docs(identity): ADR-008 auth model, ADR-009 module management, failure modes
- 4e01024 fix(identity): family revocation lock, smuggled perm claims, override and owner edge cases
Decisions: D-016..D-026, ADR-008, ADR-009
Follow-ups: W-013 S3 review follow-ups (create email oracle, limiter DoS, audit hardening)

## 2026-11-25 — Closed W-009: MapStatus: ErrorType to HTTP status
WS: WS-09
Closed: W-009 (WS-09) MapStatus: ErrorType to HTTP status
Outcome: MapStatus implemented as an exhaustive ErrorType-to-status switch (owner-approved). New ErrorType.Unavailable maps to 503; GlobalExceptionHandler answers 503 + Retry-After for transient DbException/TimeoutException; SendNoContent<T> returns 204 + ETag. The 16 red-by-design tests are green; suite 82/82.
Commits:
- 4e8ee4e feat(api): map ErrorType to HTTP status, 503 for transient db failures
Decisions: D-009
Follow-ups: none

## 2026-11-25 — Closed W-008: H1 port the AI engineering harness
WS: WS-08
Closed: W-008 (WS-08) H1 port the AI engineering harness
Outcome: Ported the AI engineering harness from my two other repos, public-safe: local beads, W-/D-/ADR- ids, refs-trailer and fix-has-test commit gates, a PreToolUse safety guard, an advisory seam-triggered Codex gate, four project skills, fitness functions with shrink-only baselines, doc-lifecycle scripts (close-workstream, commit-link-check, prune-check, adr-check, archive-project-log), failure modes, branch ledger, handoff and the how-we-use-ai narrative. The Codex invocation is confirmed from --help only.
Commits:
- a3a936a feat(spine): beads-local tracking, refs and fix-has-test commit gates
- c4f9dc1 feat(spine): safe-command guard, advisory codex gate, project skills
- ad58e29 test(arch): fitness functions for critical modules with shrink-only baselines
- b26f99c docs(spine): control-plane gaps and how-we-use-ai narrative
Decisions: D-012, D-013, D-014, D-015, ADR-007
Follow-ups: First real Codex seam run (verify invocation); S3 identity (W-005); MapStatus (W-009, owner)

## 2026-11-25 — Closed W-004: S2 persistence: Postgres, EF Core + Dapper, audit, typed ids, Customers slice
WS: WS-04
Closed: W-004 (WS-04) S2 persistence: Postgres, EF Core + Dapper, audit, typed ids, Customers slice
Outcome: Compose Postgres, EF Core + Dapper over one unit of work, audit stamping on both paths, Uuid7 typed ids, Customers vertical slice, dev seeder, Testcontainers tests, Bruno suite and runbooks 03-04. 16 tests stay red until MapStatus exists.
Commits:
- b30ca07 feat(customers): postgres compose, ef core + dapper unit of work, audit stamping, typed ids, customers slice
- 8ed061b feat(api): bruno regression suite, seed fix, runbooks 03-04 and S2 spine docs
Decisions: D-004, ADR-006
Follow-ups: MapStatus is the owner's (W-009); S3 identity next (W-005)

## 2026-11-25 — Closed W-003: S1 validation, exception handler, Serilog, correlation and saga IDs
WS: WS-03
Closed: W-003 (WS-03) S1 validation, exception handler, Serilog, correlation and saga IDs
Outcome: Validation and logging behaviors, global exception handler, Serilog, correlation and saga ids (ADR-005), a ping feature that exercises the real pipeline, runbook 02 and tests; framework 400s fixed to problem+json.
Commits:
- 914e2ed feat(observability): validation and logging behaviors, global exception handler, correlation and saga IDs
- 7430e44 fix(api): problem+json for framework 400s and empty status-code responses
Decisions: D-003, ADR-005
Follow-ups: S4 webhooks must carry the correlation headers (W-006)

## 2026-11-25 — Closed W-002: S0 retarget .NET 8, FluentResults, Application layer, neutral docs
WS: WS-02
Closed: W-002 (WS-02) S0 retarget .NET 8, FluentResults, Application layer, neutral docs
Outcome: Retargeted to .NET 8 (Fugue.sln, SDK pinned 8.0.428); added Fugue.Application; replaced the hand-rolled Result with FluentResults and FugueError; Send* returns ActionResult<T>; neutral docs, ADR-003/004, roadmap, runbook 01 and the AI journal.
Commits:
- 5382163 refactor(arch): retarget net8, adopt FluentResults, add Application layer, neutral docs
Decisions: D-002, D-009, D-010, D-011, ADR-003, ADR-004
Follow-ups: none

## 2026-11-11 — Closed W-001: Bootstrap the doc spine
WS: WS-01
Closed: W-001 (WS-01) Bootstrap the doc spine
Outcome: Router, spine files, git hooks and spine scripts installed; the link checker runs clean.
Commits:
- 400fa57 chore(spine): bootstrap repo spine, solution skeleton and ApiControllerBase
Decisions: D-001, D-005, D-006, D-007, D-008, ADR-001, ADR-002
Follow-ups: none

## 2026-11-25 — S2 — persistence, Customers slice, Bruno

### Added
- `docker-compose.yml` (Postgres 17, named volume, healthcheck), `.env.example`, `dotnet-ef` local tool.
- EF Core 8 + Npgsql + snake_case naming; `FugueDbContext`; migration `InitialCustomers` (catalog row 1).
- `IUnitOfWork`/`EfUnitOfWork`, Dapper `SqlExecutor` on EF's connection and transaction (ADR-006).
- `IAuditable`, `IAuditStamp`, `AuditableSaveChangesInterceptor`; `Uuid7`, `IStronglyTypedId`, `CustomerId` with EF/Dapper/JSON/MVC wiring.
- Customers slice: `/api/v1/customers` (create, get, list with keyset paging, update with `If-Match`, suspend, reactivate, delete); development seeder (`dotnet run -- --seed`).
- Testcontainers integration tests; Bruno collection `bruno/fugue` and `npm run api:test`; runbooks 03 and 04.

### Fixed
- Framework-generated 400s (model binding) and empty 404/405/415 responses are `application/problem+json`; the controller-level `[Produces("application/json")]` is gone.

### Changed
- Route URLs are lower-case.

## 2026-11-25 — S1 — validation, exceptions, logging, correlation and saga IDs

### Added
- `ValidationBehavior` and `LoggingBehavior` (Mediator pipeline), `AddApplication()` validator registration, `ICorrelationContext`.
- `GlobalExceptionHandler` (RFC 9457 500, no detail outside Development, 499 for client aborts); `AddProblemDetails` customization adds `traceId` and `correlationId`.
- `CorrelationIdMiddleware` (`X-Correlation-Id`, `X-Saga-Id`, Activity tags/baggage, Serilog LogContext); Serilog with readable console in Development and compact JSON elsewhere (ADR-005).
- `Diagnostics` ping feature and controller; runbook 02.
- `ApiControllerBase.Send*` overloads for `ICommand` and `IQuery`.

## 2026-11-25 — S0 — retarget .NET 8, FluentResults, Application layer, neutral docs

### Changed
- Target framework .NET 8 (`global.json` SDK 8.0.428); solution is now `Fugue.sln`. `CheckEolTargetFramework=false` is deliberate (ADR-004 context in `Directory.Build.props`).
- Hand-rolled `Result`/`Result<T>`/`Error` replaced by FluentResults + `FugueError`/`FugueErrors`; `ErrorType` kept (ADR-004). `ApiControllerBase.Send*` now return `ActionResult<T>`.
- Docs are domain-neutral. ADR-001 superseded by ADR-003 (single-tenant default, tenancy toggle).

### Added
- `Fugue.Application` project with `Features/` and `Common/Behaviors/`.
- ADR-003, ADR-004, `docs/plans/roadmap.md`, `docs/runbooks/`, `docs/ai-journal.md`.

## 2026-11-11 — Phase 1a — solution skeleton + ApiControllerBase

### Added
- Solution `Fugue.slnx` with `Fugue.Api`, `Fugue.Domain`, `Fugue.Infrastructure`, `Fugue.Tests`, `Fugue.ArchTests`; `global.json`, `Directory.Build.props`, central package management (`Directory.Packages.props`).
- Domain result types (`Result`, `Result<T>`, `Error`, `ErrorType`, `PagedResult<T>`, `IVersioned`) and `ICurrentUser` (ADR-002).
- `ApiControllerBase`: result-to-ProblemDetails (RFC 9457), ETag / `If-Match`, keyset paging headers, `Idempotency-Key` accessor, lazy `Sender` / `CurrentUser`; `ClaimsCurrentUser` implementation.
- Tests over real HTTP using a probe controller and a fake `ISender`; architecture tests for layering and controller rules.

### Notes
- `ApiControllerBase.MapStatus` is deliberately unimplemented (owner decision); `MapStatusTests` and the two error-shape HTTP tests are red until it is written.

## 2026-11-11 — Phase 0 — repo spine

### Added
- Doc spine (`hygiene.md`, `docs/` board, decisions, backlog, log, templates) and the `scripts/spine/` gates.
- Git hooks orchestrated by lefthook; commitlint with Fugue scopes; test-integrity check for C#/xUnit.
- `CLAUDE.md`, `AGENTS.md`, PR template, migration catalog, ADR-001 (shared DB + `tenant_id`), ADR-002 (MVC controllers + `ApiControllerBase`).
