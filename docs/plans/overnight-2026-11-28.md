# Overnight task list — 2026-11-28 (submission 15:00 +08)

The lead works this list top to bottom under `/loop` while the owner sleeps (ADR-014, D-065). Each item: build in a lane worktree
(`.worktrees/nth-<topic>`, ledger row first), lane reports gates + a headless screenshot, the lead re-runs the gates, looks at the
screenshot, merges, rebuilds the `fugue-demo` stack, re-checks headless, pushes with the SSH keepalive and `git ls-remote`.
Tick `[x]` with the merge sha. Never: force push, `--no-verify`, drop a non-throwaway database, pkill by name, headed browsers.

Order inside P1: demo-visible first (5 to 9), then 11a and 11b, then 10, 11c. Feature cut-off 13:00. 13:00-14:00 only fixes. 14:00: recreate the clean demo stack, final push, hot-state.

## P0 — demo breakers
- [x] 1. (03:08, on rebuilt :8080: welcome centred 465,251 510x397; outside click closed it; nav spam then /audit navigated; 0 errors) Browser re-check of 2ba4e19 (welcome can never freeze the page): fresh context, sign in WITHOUT `?tour=off`, welcome
      centred; click outside closes it; then spam the nav (scratch `check/spam2.mjs` pattern) and every link navigates. Note:
      dev-server sign-in timed out at 03:03 (proxy ECONNREFUSED to :5081, then unexplained); check on the rebuilt :8080 instead.
- [ ] 2. Push main (keepalive; origin was fb10684 at 03:02).

## P1 — owner asks (D-064)
- [ ] 3. nth-uam (admin adds users, USERS_MANAGE migration + catalog row): wait for its report, verify, merge.
- [ ] 4. nth-score ("The Score" per-correlation inspector from real audit/outbox/delivery rows): wait, verify, merge.
- [ ] 5. Forms: no native OS popups. Console-styled select (CDK overlay listbox, keyboard + a11y) and date picker for every
      form; "Fill with an example" on every form, like the sign-in demo accounts (Report an incident: 3-4 realistic templates per
      market/line; request info; set reserve; decide/reject reason; claimant answer; webhook subscription; customers; add user).
- [x] 6. (0439df5, verified on :8080 03:35: workbook footer+tab, Export CSV, Save to Excel icons, 0 errors) Claim page assessment worksheet: full workbook feel (footer, sheet tab, spare columns), an Export button;
      rename "Open in Excel" to "Save to Excel" everywhere; icons on Save to Excel and Use as reserve (and Reserve sheet buttons).
- [x] 7. (0439df5: next-step copy, step popovers on hover/focus, How a claim moves modal closes on Esc and outside click) Claim page copy: rewrite the "Next step" block (blocked state reads badly); explain the Handling and Payout tracks
      (hover popover per step and a "How a claim moves" modal).
- [x] 8. (00e5c76: rounded, token cells, today and events in palette) SLA calendar: rounded corners, warmer cells (tokens, not white), events and today styled in the console palette.
- [x] 9. (00e5c76: Stack dialog; FOUND PostgreSQL 17 (postgres:17-alpine in docker-compose.yml), ids UUID v7 from the API (Uuid7.NewGuid): owner's 'Postgres 18' is wrong, 'timestamped GUIDs' is right) Env pill `dev · net8 · v1` opens a "Stack" modal: Angular + key libraries, .NET 8 + key packages, PostgreSQL version and
      ID scheme. VERIFY first (compose image tag; owner believes Postgres 18 and timestamped GUIDs; state what the code really
      uses, fix the owner's belief in the handoff if it differs).
- [ ] 10. Search hardening from `docs/research/2026-11-28-meilisearch.md`: market name and line searchable, synonyms (SG ->
      Singapore ...), typo tolerance off for claim/policy numbers, highlights showing why a hit matched, facets; settings applied
      idempotently at startup; tests. Decide other global-search candidates (users for admins, customers) with a D-row.
- [ ] 11. README counts and screenshots refreshed after the merges (verified numbers only).

- [ ] 11a. Intake import module (brief: "claimants submit by phone or email", staff work "from shared inboxes and
      spreadsheets"). Staff-only page "Import" with three sources, all producing DRAFT claims a person reviews before they
      are created (never auto-submitted): (a) spreadsheet importer, .xlsx/.csv upload, column mapping, per-row validation
      preview using the same rules as Report an incident, import the valid rows; (b) shared inbox, upload .eml/.mbox
      exports, route by sender domain to market (a domain -> market table in config, IOptions), parse subject/body into a
      draft; (c) WhatsApp, upload a chat export .txt, parse messages into a draft with the transcript attached as a note.
      Honest labels: these are FILE IMPORTS that stand in for live connectors (no Graph/Gmail/WhatsApp Business API calls);
      say so on the page and in a D-row. Audited, idempotent (same file twice does not duplicate), tests per parser.
- [ ] 11b. PDF export with a fixed template: a "Settlement letter" (approved/settled claims: claimant, policy, incident,
      approved amount in the claim currency, decision reason, officer, dates, claim number, correlation id) and a decision
      letter for rejections. One fixed, branded template (QuestPDF on the API or a client-side generator; pick the one that
      keeps the bundle and licences clean, record it). Download from the claim page for staff and for the claimant on their
      own claim (scope rules apply: 404 outside scope). Label it a sample document, not a legal letter.
- [ ] 11c. Backlog items still open in `docs/state-backlog.md`: #003 (faithful shell port: superseded by the shell work,
      close with a note), #006 (queue counts in one call), #007 (past info requests list), #009 (panel leftovers: a paid or
      settled figure on Team and exposure, a notification consumer for events, the Send question during an API restart).

## P2 — after the lanes merge (backlog #010)
- [ ] 12. Per-slice folders `Commands/ Queries/ Models/ Repos/ Helpers/` + DI extension; namespaces follow folders (IDE fix or
      `dotnet format analyzers --diagnostics IDE0130`); full suite.
- [ ] 13. Remaining hardcoded values to IOptions (page sizes kept in step with validators, webhook replay window, SLA days).
- [ ] 14. `Fugue.Worker` host for the four BackgroundServices + a building-blocks project for pipeline behaviours; ArchTests.
- [ ] 15. Optional: audit page "Sign-ins" tab over `access_audit`; reword the `[NoAudit]` comment on LoginCommand.

## P3 — wrap (by 14:00)
- [ ] 16. CHANGELOG v0.0.3 + tag, journal, hot-state, failure modes, push with tags.
- [ ] 17. Recreate the clean demo stack (`docker compose -p fugue-demo down -v && docker compose -p fugue-demo up -d --build`),
      headless smoke as all four personas, zero console errors.
