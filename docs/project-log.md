# Fugue — project log (journal)

Latest first. Append-only: never edit an old entry. One entry per session or per notable event. Older `(previous)` RESUME blocks from `docs/hot-state.md` are copied into the entry for their day.

Heading form: `## <YYYY-MM-DD> — <title>`. Entry key = `<date>#<n>`, where n is the order among that date's headings, top to bottom (so a second entry the same day is `<date>#2`).
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)`.
Then: what was done · what was decided (cite D-NNN) · what is open (cite #NNN).

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

## 2026-11-25 — S2: Postgres, EF Core + Dapper, audit, typed ids, Customers
WS: WS-04
Fixed framework 400s to `application/problem+json`; added docker-compose Postgres, `FugueDbContext` and the first migration, `IUnitOfWork`/`SqlExecutor`, audit stamping on both write paths, `Uuid7` and `CustomerId`, the Customers vertical slice, a dev seeder, Testcontainers integration tests, a Bruno suite, runbooks 03 and 04 and ADR-006.
Decided: D-004.
Next: S3 identity. Owner action pending: implement `MapStatus`.
Superseded RESUME block (2026-11-25, S0 and S1 done): S2 persistence was next.

## 2026-11-25 — S1: validation, exceptions, logging, correlation
WS: WS-03
Added the validation and logging behaviors, global exception handler, Serilog, correlation and saga ids (ADR-005), a ping feature that exercises the real pipeline, runbook 02 and tests.
Decided: D-003.
Next: S2 persistence per the roadmap. Owner action pending: implement `MapStatus`.
Superseded RESUME block (2026-11-25, S0 done): net8, FluentResults, Application layer, neutral docs finished; S1 was next.

## 2026-11-25 — S0: net8, FluentResults, Application layer, neutral docs
WS: WS-02
Retargeted to .NET 8 (`Fugue.sln`, SDK pinned 8.0.428), added `Fugue.Application`, replaced the hand-rolled Result with FluentResults + `FugueError`, made `Send*` return `ActionResult<T>`, neutralised the docs, wrote ADR-003/004, the roadmap, runbook 01 and the AI journal.
Decided: D-002.
Next: WS-03 (S1: validation, exception handler, Serilog, correlation and saga IDs).
Superseded RESUME block (2026-11-11): spine just installed, nothing in flight.

## 2026-11-11 — Doc spine installed
WS: WS-01
Installed the router, the spine files, the git hooks and the spine scripts. The link checker runs clean.
Decided: D-001.
Next: add the first real thread to `docs/workstreams.md`.
