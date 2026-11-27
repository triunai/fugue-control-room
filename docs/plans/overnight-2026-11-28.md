# Overnight task list — 2026-11-28 (submission 15:00 +08)

The lead works this list top to bottom under `/loop` while the owner sleeps (ADR-014, D-065). Each item: build in a lane worktree
(`.worktrees/nth-<topic>`, ledger row first), lane reports gates + a headless screenshot, the lead re-runs the gates, looks at the
screenshot, merges, rebuilds the `fugue-demo` stack, re-checks headless, pushes with the SSH keepalive and `git ls-remote`.
Tick `[x]` with the merge sha. Never: force push, `--no-verify`, drop a non-throwaway database, pkill by name, headed browsers.

Feature cut-off 13:00. 13:00-14:00 only fixes. 14:00: recreate the clean demo stack, final push, hot-state.

## P0 — demo breakers
- [ ] 1. Browser re-check of 2ba4e19 (welcome can never freeze the page): fresh context, sign in WITHOUT `?tour=off`, welcome
      centred; click outside closes it; then spam the nav (scratch `check/spam2.mjs` pattern) and every link navigates. Note:
      dev-server sign-in timed out at 03:03 (proxy ECONNREFUSED to :5081, then unexplained); check on the rebuilt :8080 instead.
- [ ] 2. Push main (keepalive; origin was fb10684 at 03:02).

## P1 — owner asks (D-064)
- [ ] 3. nth-uam (admin adds users, USERS_MANAGE migration + catalog row): wait for its report, verify, merge.
- [ ] 4. nth-score ("The Score" per-correlation inspector from real audit/outbox/delivery rows): wait, verify, merge.
- [ ] 5. Forms: no native OS popups. Console-styled select (CDK overlay listbox, keyboard + a11y) and date picker for every
      form; "Fill with an example" on every form, like the sign-in demo accounts (Report an incident: 3-4 realistic templates per
      market/line; request info; set reserve; decide/reject reason; claimant answer; webhook subscription; customers; add user).
- [ ] 6. Claim page assessment worksheet: full workbook feel (footer, sheet tab, spare columns), an Export button;
      rename "Open in Excel" to "Save to Excel" everywhere; icons on Save to Excel and Use as reserve (and Reserve sheet buttons).
- [ ] 7. Claim page copy: rewrite the "Next step" block (blocked state reads badly); explain the Handling and Payout tracks
      (hover popover per step and a "How a claim moves" modal).
- [ ] 8. SLA calendar: rounded corners, warmer cells (tokens, not white), events and today styled in the console palette.
- [ ] 9. Env pill `dev · net8 · v1` opens a "Stack" modal: Angular + key libraries, .NET 8 + key packages, PostgreSQL version and
      ID scheme. VERIFY first (compose image tag; owner believes Postgres 18 and timestamped GUIDs; state what the code really
      uses, fix the owner's belief in the handoff if it differs).
- [ ] 10. Search hardening from `docs/research/2026-11-28-meilisearch.md`: market name and line searchable, synonyms (SG ->
      Singapore ...), typo tolerance off for claim/policy numbers, highlights showing why a hit matched, facets; settings applied
      idempotently at startup; tests. Decide other global-search candidates (users for admins, customers) with a D-row.
- [ ] 11. README counts and screenshots refreshed after the merges (verified numbers only).

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
