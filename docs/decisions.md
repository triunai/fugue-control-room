# Fugue — decisions (why it is this way)

Latest first. Add an entry when a call settles. To change a decision, add a new entry that supersedes it. Never delete.

Heading form: `## D-NNN — <title> — <YYYY-MM-DD>`. NNN is three digits. Only the orchestrator mints numbers.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)` for decisions older than the rule. Never guess.
Then: Context (one or two lines) · Decision · Why · Consequences · Supersedes (if any), and three closing lines: `Who:` (owner, AI, review) · `Status:` (accepted, superseded by D-NNN, ...) · `Promote:` (`no`, or `yes (ADR-NNN)`).
Capture a decision THE SAME TURN it is made, as a D entry. Promote it to an ADR file (`docs/adr/_template.md`: context, decision, alternatives, consequences, supersedes) when it changes architecture, a public contract, the data model, security posture or a cross-cutting convention; the D entry links the ADR. `node scripts/spine/adr-check.mjs` (pre-push) fails on an unlinked ADR or a `Promote: yes` without its file.

## Index (D-NNN to ADR file)

| D | Title | ADR |
|---|---|---|
| D-001 | Adopt the doc spine | - |
| D-002 | Retarget .NET 8, FluentResults, single-tenant default (covers the .NET 8 and FluentResults owner overrides) | [ADR-003](adr/ADR-003-single-tenant-default-tenancy-toggle.md), [ADR-004](adr/ADR-004-fluentresults-with-errortype.md) |
| D-003 | Three-layer correlation | [ADR-005](adr/ADR-005-trace-correlation-saga-ids.md) |
| D-004 | EF Core reads, Dapper writes | [ADR-006](adr/ADR-006-ef-core-reads-dapper-writes.md) |
| D-005 | Shared database with `tenant_id`, no RLS (superseded by D-002) | [ADR-001](adr/ADR-001-shared-db-tenant-id.md) |
| D-006 | MVC controllers over `ApiControllerBase`; source-generated Mediator | [ADR-002](adr/ADR-002-controllers-with-api-controller-base.md) |
| D-007 | Clean-room rewrite after the template audit | - |
| D-008 | Name: Fugue; theme the brand, keep domain names plain | - |
| D-009 | `MapStatus` is the owner's | [ADR-004](adr/ADR-004-fluentresults-with-errortype.md) |
| D-010 | OrbStack as the container runtime | - |
| D-011 | Fable architecture critique: accepted, with two challenges | - |
| D-012 | Pin every catch | - |
| D-013 | Beads is a local queue; commits cite only committed ids | [ADR-007](adr/ADR-007-ai-harness-gates.md) |
| D-014 | Codex review gate is advisory, seam-triggered, capped | [ADR-007](adr/ADR-007-ai-harness-gates.md) |
| D-015 | Commit gates and the safe-command guard | [ADR-007](adr/ADR-007-ai-harness-gates.md) |
| D-016 | S3: JWT bearer (HS256), local token endpoint, refresh in S3 (D-024) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-017 | S3: lean UAM v3 port and precedence deny > allow > role (owner overrode "defer overrides") | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-018 | S3: permissions via uncached DB lookup in `OnTokenValidated`, not token claims | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-019 | S3: ownership by repository scoping, 404 on cross-owner | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-020 | S3: uniform login failure, dummy hash, no lockout | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-021 | S3: deferrals and exclusions from UAM v3 | - |
| D-022 | S3: abuse controls, create ownership, canonical email, key hygiene | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-023 | S3: module management (config kill switch + runtime registry) | [ADR-009](adr/ADR-009-module-management.md) |
| D-024 | S3: tenants, refresh tokens, groups, grant/access audit built basic | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-025 | Customers PUT/suspend/reactivate answer 204 + ETag | - |
| D-026 | Base entity contract: IAuditable everywhere, append-only allowlist | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-027 | W-012: `audit_log` table shape, append-only trigger, keyset indexes (no BRIN) | [ADR-010](adr/ADR-010-audit-log.md) |
| D-028 | W-012: capture in the same transaction (EF interceptor in the same SaveChanges; Dapper through `IAuditTrail`) | [ADR-010](adr/ADR-010-audit-log.md) |
| D-029 | W-012: failures recorded by `AuditBehavior` as one failure row; auth commands exempt | [ADR-010](adr/ADR-010-audit-log.md) |
| D-030 | W-012: safety knobs (masking at write time, partial masks, size bound, never sampled, not disable-able outside Development) | [ADR-010](adr/ADR-010-audit-log.md) |
| D-031 | W-012: read API behind `AUDIT_READ`, admin only, keyset paged | [ADR-010](adr/ADR-010-audit-log.md) |
| D-032 | W-012: `grant_audit` and `access_audit` stay specialised; relationship to `audit_log` | [ADR-010](adr/ADR-010-audit-log.md) |
| D-033 | W-012: retention and archival deferred | - |
| D-034 | W-013: customer email is unique per owner (owner call) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-035 | W-013: login limiter keyed on (IP, email) plus a loose per-email ceiling (owner call) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-036 | W-013: S3 review hardening (override If-Match, group self-target, fail-closed module config, tenant is_active, audit key hygiene) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-037 | W-006: transactional outbox (same transaction, domain events, frozen payload, thin masked payload) | [ADR-011](adr/ADR-011-outbox-and-signed-webhooks.md) |
| D-038 | W-006: subscription secrets in Data Protection (key ring in the database), SSRF guard at subscribe and connect time | [ADR-011](adr/ADR-011-outbox-and-signed-webhooks.md) |
| D-039 | W-006: two-stage dispatcher (relay, leased delivery), SKIP LOCKED, outbox is the retry authority, signature v1, at-least-once | [ADR-011](adr/ADR-011-outbox-and-signed-webhooks.md) |
| D-040 | W-006: `IEventPublisher` is the broker seam (Kafka adapter documented, not built), Development-only sink | [ADR-011](adr/ADR-011-outbox-and-signed-webhooks.md) |
| D-041 | W-006: Codex S4 review fixes (per-claim token fence, spent-lease dead letter, lease re-check before send, cancelled-save cleanup, sink envelope check) | [ADR-011](adr/ADR-011-outbox-and-signed-webhooks.md) |
| D-042 | W-016: the backend owns the API contract; the OpenAPI snapshot is a test-gated file | [ADR-012](adr/ADR-012-openapi-contract-and-console-boundary.md) |
| D-043 | W-016: console CORS policy (exact origins, no credentials, named headers) | [ADR-012](adr/ADR-012-openapi-contract-and-console-boundary.md) |
| D-044 | W-016: `GET /api/v1/me` is the console's source for navigation and permission checks | [ADR-012](adr/ADR-012-openapi-contract-and-console-boundary.md) |
| D-045 | Console palette: Vellum light | - |
| D-046 | Console stack: NgRx SignalStore + TanStack Query, in `web/`, domain-neutral screens | - |
| D-047 | Live permissions SSE stream cancelled | - |
| D-048 | Owner's earlier design repo may be copied directly, names scrubbed on import | - |
| D-049 | Keep the W-017 Angular console; no template import | - |
| D-050 | Idempotency keys with stored-response replay on the two create POSTs | [ADR-013](adr/ADR-013-idempotency-keys.md) |
| D-051 | W-018 audit import: four ideas ported, nine rejected | [ADR-010](adr/ADR-010-audit-log.md) (amended) |
| D-052 | The claims window runs in this repo, opened by a tag | - |
| D-053 | Claims platform shape for the window | - |
| D-054 | Raw SQL lives in .sql files; a read-only Postgres MCP for agents | - |
| D-055 | One file per concept, not per type | - |
| D-056 | Codex claims seam review: 3 accepted (reserve race, required submit key, event names), 2 declined | [review](reviews/codex/2026-11-27-claims-seam.md) |
| D-057 | Console money: ISO code plus two decimals for every market currency | - |
| D-058 | Audit search on Meilisearch, fed by an indexer from audit_log (owner reversed the earlier AI-only rejection) | - |
| D-059 | Nice-to-have UI on Univer (reserve sheet) and FullCalendar (SLA calendar), lazy-loaded | - |
| D-060 | Console shell adapted (not ported) from the owner's reference app; faithful port parked (correction in the entry) | - |
| D-061 | Fable interview-readiness review: all 15 changes accepted, Kafka adapter included | [review](reviews/fable/2026-11-27-interview-readiness.md) |
| D-062 | Feature freeze for v0.0.2; Kafka kept but ignored (off by default, out of the demo and README highlights) | - |
| D-063 | Owner lifts the freeze for one feature: Meilisearch claims search, claimant scope enforced server-side | - |

The ADR files stay the home of architecture reasoning; a D entry below is the short why plus the link. Trailer ids a commit may cite: `W-NNN` (`docs/workstreams.md`), `D-NNN` (this file), `ADR-NNN` (`docs/adr/`).

## D-078 — One folder layout per slice: Commands, Queries, Models, Repos, Helpers — 2026-11-28
WS: WS-39
Context: backlog #010 item 1 (owner, 02:45): every vertical slice should read the same way.
Decision: `Features/<Feature>/` holds `Commands/`, `Queries/`, `Models/` (DTOs, outcomes, errors), `Repos/` (the slice's ports with their records) and `Helpers/` (pure rules such as `ClaimFlow`, `ClaimScope`); namespaces follow folders; ports used by several slices stay in `Application/Abstractions/` (`IAuthPorts`); no per-slice DI extension, because none was needed (Infrastructure wires the ports, Mediator handlers are source-generated). Merged in 8e0993b (W-039, lane nth-refactor): 58 renames, OpenAPI snapshot byte-identical, the full suite count unchanged (849 + 37).
Why: a reviewer finds any file by its role, in every slice the same way.
Consequences: items 2-3 of #010 stay open (D-075, D-076). Dated history docs keep their old paths on purpose.
Who: owner (asked), AI (lane nth-refactor)
Status: accepted
Promote: no

## D-077 — Legacy sync stays a design; the built path is file import — 2026-11-28
WS: WS-31
Context: the owner asked (11:30) for a way to bring existing legacy data in so the platform is backward compatible, with the import module as the central adapter; lane nth-legacydoc wrote `docs/design/legacy-sync.md` (merged 615f0d1).
Decision: keep legacy sync as a design (backlog #012, KIV). The built path is the file import (D-074): spreadsheets, inbox and chat exports reviewed into drafts, idempotent by item. The design doc marks everything beyond section 1 as PROPOSAL and adds a section on scaling to many users and offline sync grounded in ADR-001/003/006/011/013.
Why: no legacy source or owner exists to build against on submission day; the design is an interview talking point, not a promise.
Who: owner (asked, KIV), integrator (recorded)
Status: accepted
Promote: no

## D-076 — Items 15 and 11g deferred; item 13 only if the refactor merges by 11:00 — 2026-11-28
WS: WS-39
Context: from about 07:00 the Mac slept on battery (failure mode 36), so suites ran only in dark-wake windows and the morning lost about three hours; at 10:14 it is still on battery.
Decision: defer item 15 (audit "Sign-ins" tab over `access_audit`, the `[NoAudit]` comment reword) and 11g (permission-chip tooltips: no DTO exposes `permissions.description`, so it needs an src/ change). Item 13 (IOptions) is dispatched only if item 12 merges by 11:00, else deferred too. The README refresh (11) runs at 12:00 regardless; the 14:00 wrap is unchanged. A red suite counts only once `pmset -g batt` says AC Power.
Why: finished and verified beats new on submission day; an unreliable machine cannot prove a new change.
Consequences: 15 and 11g stay in the plan as deferred; interview answers: `access_audit` already records every sign-in (the tab is a read view), and the catalog already has descriptions (one DTO field away).
Who: dispatcher (decided 09:58), integrator (recorded)
Status: accepted
Promote: no

## D-075 — Item 14 deferred: the Worker host and building-blocks split stay in the backlog — 2026-11-28
WS: WS-39
Context: overnight item 14 (a `Fugue.Worker` host for the four BackgroundServices, a building-blocks project for the pipeline behaviours) was gated at 10:00 (05:15 plan). About two hours were lost to the OrbStack daemon hang (failure mode 36, about 07:00-09:03), and item 13 (IOptions) after the refactor runs to about 10:40, so 14 could not start before its gate.
Decision: defer item 14; it stays as backlog #010 part 3. Interview answer: the four BackgroundServices (`OutboxDispatcherService`, `ClaimIndexer`, `AuditIndexer`, `IdempotencyPurgeService`) are already isolated behind their own options and DI registration, so lifting them into a host is mechanical.
Why: a new host project and a compose service on submission day is high risk for no demo value.
Who: dispatcher (decided 09:15), integrator (recorded)
Status: accepted
Promote: no

## D-074 — Intake import is file imports reviewed into drafts, not live connectors — 2026-11-28
WS: WS-31
Context: the brief says claimants submit by phone or email and staff work from shared inboxes and spreadsheets (overnight item 11a).
Decision: staff (CLAIMS_HANDLE) upload a spreadsheet (.csv/.xlsx), a shared-inbox export (.eml/.mbox) or a WhatsApp chat export (.txt); the server reads it into drafts, a person reviews each draft, and only then is a claim created, owned by the claimant's existing account, through the submit path's own validator, factory and repository (timeline, audit, outbox). The uploads stand in for Microsoft Graph / Gmail and WhatsApp Business connectors and the page says so; no external service is called. POST /claims is not reused because staff do not hold CLAIMS_SUBMIT and it makes the caller the claimant. Idempotency is a ledger (`claim_imports`, migration 20261127194749_AddClaimImports) keyed by the server's SHA-256 of each source item, inserted in the claim's transaction (`ON CONFLICT DO NOTHING`), so the same row, email or chat never becomes two claims. For that reason the import create is NOT `[Idempotent]`: the ledger makes a retry a 409, and the pinned list of idempotent creates stays at three (the lane's first full run failed only that pin; it was fixed in code, cc90cc1, not by editing the test). Sender domain to market is configuration (`Import:MarketByDomain`, IOptions, longest suffix). No package: the xlsx and MIME readers are small hand-written readers with tests. Merged in f967355 (W-031, lane nth-import).
Why: live connectors need tenant consent and secrets we do not have; files prove the same pipeline honestly.
Consequences: rolling the migration back keeps imported claims but loses the ledger, so a re-import after a rollback would duplicate (catalog row 13).
Who: owner (asked, item 11a), AI (lane nth-import)
Status: accepted
Promote: no

## D-073 — Header search stays claims and audit; users and customers are not indexed — 2026-11-28
WS: WS-32
Context: item 10 asked whether admins should find users and whether customers belong in the header search.
Decision: neither goes into Meilisearch now. Users are a few dozen accounts, already searchable by `GET /api/v1/users?q=` (email or display name, USERS_VIEW); indexing them would copy emails into a second store with its own scope rules for no gain at this size. If wanted later: a "People" group in the header shown only with USERS_VIEW, calling the existing endpoint (no new index). Customers are the starter example the brief never asks for (D-063 already chose claims over customers), and the customers list already filters with `search`.
Why: no new store of personal data without a need.
Consequences: revisit when users number in the thousands or customers join the claims domain.
Who: AI (lane nth-searchx), integrator (recorded)
Status: accepted
Promote: no

## D-072 — Claims search answers like an officer expects — 2026-11-28
WS: WS-32
Context: "SG", "Singapore" and "motor" found nothing on market or line; a mistyped claim number returned its neighbours; index settings were re-sent (re-indexing) on every start.
Decision: market code, market name and line are searchable with two-way synonyms (six market codes and names, car/motor, home/property); every word must match (`matchingStrategy: all`); identifier tokens (a digit and 5+ characters) are sent as phrases so claim and policy numbers match exactly; hits carry highlights (why they matched) and facet counts; settings are compared at startup and sent only when they differ; a document shape version re-indexes once when the shape changes. `typoTolerance.disableOnAttributes` is NOT used: on the pinned Meilisearch v1.12.8 it hides words of documents added after the setting (reproduced; fixed in v1.13.3). Claimant scope (D-063) unchanged, with a new negative test. Merged in aa401bc (W-032, lane nth-searchx).
Why: lookups by number must be exact, and a restart must not re-index.
Consequences: a query with a word no claim carries returns nothing; a policy number matches only once complete. Follow-up candidate: Meilisearch v1.13.3+ in compose and tests, then `disableOnAttributes` as a second guard.
Who: owner (asked, item 10), AI (lane nth-searchx)
Status: accepted
Promote: no

## D-071 — The global ErrorHandler drops exactly one benign browser notice, ResizeObserver loop — 2026-11-28
WS: WS-30
Context: /claims/calendar logged "ResizeObserver loop completed with undelivered notifications." as a console error with the hood sidebar open (the default), which fails the zero-console-errors gate (17b). The load case was fixed at the source (the calendar is built once its CSS has loaded and the claims query has settled); the window-resize case comes from FullCalendar 7.1.5's internal global ResizeObserver on window resize, and no option exists to defer its updateSize.
Decision: `FugueErrorHandler` (`web/src/app/core/error-handler.ts`, provided in `app.config.ts`) drops that one message by exact match (or the same text on `cause` in dev builds) and passes every other error, including other ResizeObserver texts, to Angular's default handler. A spec proves both sides; a mutation check turned it red. Merged in 3de8f49 (W-030, lane nth-calfix).
Why: the notice is the browser reporting a deferred layout, not a failure; a broad filter would hide real errors.
Consequences: revisit if FullCalendar exposes its resize observer.
Who: AI (lane nth-calfix), integrator (recorded)
Status: accepted
Promote: no

## D-070 — Sample claim letters come from a hand-written PDF writer, no PDF dependency — 2026-11-28
WS: WS-34
Context: overnight item 11b asks for a fixed, branded settlement letter (approved or settled claims) and a decision letter (rejections), downloadable by staff and by the claimant on their own claim, with the library choice recorded.
Decision: `Features/Letters` writes PDF 1.4 by hand with the standard Type 1 fonts (Times-Bold, Helvetica; nothing embedded), served by `GET /api/v1/claims/{id}/letter`. QuestPDF rejected (Community licence v3.0 limits free commercial use to organisations under USD 1M revenue, so a reviewing insurer would need a paid licence); PDFsharp rejected (AGPL-3.0-only from 6.5.0, licence drift); client-side jsPDF/pdfmake rejected (a new npm package and bundle weight). A letter is composed only from what the caller may read (the claim and its timeline under `ClaimScope`, 404 outside scope), made per request and never stored; 409 `claim.letter_not_available` before a decision. Every page says "Sample document, not a legal letter". Merged in 59e1ad6 (W-034, lane nth-pdf).
Why: licence-clean with zero dependencies; the scope rules the rest of the claims API already proves apply unchanged.
Consequences: characters outside Windows-1252 (e.g. Thai script) print as "?" (pinned by a test). Follow-up candidate: `ApiControllerBase.SendFile(...)` so file actions are one-liners.
Who: owner (asked), AI (writer over a library, licence check)
Status: accepted
Promote: no

## D-069 — Backlog #009 split: paid figure built, outside notification cut, the rest answered — 2026-11-28
WS: WS-37
Context: panel-review leftovers (#009): no paid or settled figure on Team and exposure; no notification outside the app; claimants see the SLA calendar; "Send question" during an API restart closed its dialog with no POST (unverified).
Decision: build the paid/settled figure (W-037, lane nth-paid). CUT an outside-notification consumer: signed webhooks are the integration point, so email or SMS is a subscriber's job, and building one on submission day adds risk with no demo value; the interview answer is the webhook story plus The Score. Claimants seeing the SLA calendar stays by design (only their own claims). The "Send question during a restart" case is recorded as a known limit in hot-state, not fixed today.
Why: submission-day risk; each answer is backed by code that already exists.
Who: owner (asked), dispatcher (split), integrator (recorded)
Status: accepted
Promote: no

## D-068 — The compose Postgres creates the read-only role; the MCP server is pinned — 2026-11-28
WS: WS-29
Context: owner hard gate 17a: drive the demo with the read-only Postgres MCP and the CLIs. The role did not exist on the demo stack, and the unpinned `uvx postgres-mcp` in `.mcp.json` resolved 0.1.0 with mcp 2.x and crashed.
Decision: a fresh compose volume runs `scripts/db/docker-initdb-readonly.sh` from `/docker-entrypoint-initdb.d`, creating `fugue_readonly` before the migrations; its default privileges make every later table readable and nothing writable. `.mcp.json` pins postgres-mcp 0.3.0 with uv overrides (`scripts/db/postgres-mcp-overrides.txt`: pglast 7.25, mcp below 2) and runs `--access-mode=restricted` as the second layer. Runbook `docs/runbooks/agent-tooling.md`. Merged in 66604db (W-029, lane nth-tooling).
Why: the role survives `down -v`; the write refusal is the database's, not only the tool's.
Consequences: revisit the pin when upstream fixes it; uv needs PyPI on the first run, so warm the uvx cache before the demo.
Who: owner (asked), AI (role via initdb, pin)
Status: accepted
Promote: no

## D-067 — The decision timeline page is generated from the docs, never hand-copied — 2026-11-28
WS: WS-22
Context: the owner asked (03:15) for the decision trail itself in the console: every ADR and D-row, dated, linked, grouped by the release it landed in (overnight item 11d, lane nth-decisions).
Decision: `scripts/gen-decisions.mjs` (run as `npm run gen:decisions` in `web/`) parses `docs/adr/ADR-*.md`, the `## D-` sections of `docs/decisions.md` and the CHANGELOG version headings into `web/src/app/features/decisions/decisions.data.json`, deterministically; the read-only `/decisions` page (staff, managers, admins) renders it. No API.
Why: hand-copied lists drift; a generated file with tests (both heading styles, version ranges, determinism) stays honest.
Consequences: re-run `npm run gen:decisions` and commit the JSON after editing decisions.md, an ADR or the CHANGELOG; there is no CI check yet, so the integrator regenerates at each spine commit and at the wrap.
Who: owner (asked), AI (generator over hand-copy)
Status: accepted
Promote: no

## D-066 — Dispatcher, integrator and contract-bound builder lanes replace the single overnight lead — 2026-11-28
WS: WS-22
Context: at 03:09 one Opus lead ran a `/loop` over about 15 items, building and merging serially with two to four lanes (D-065). Asked to "split it" at 03:22, the AI's answer was to tell that same lead to go wider. The owner rejected that at 03:23 ("this is dumb and waiting to fail"): one agent that builds, merges and verifies is a single point of failure and a throughput bottleneck, and its long context is expensive.
Decision: the session with the smallest context becomes the DISPATCHER and writes agent task contracts with hard file boundaries (`.worktrees/_briefs/OWNERS.md`); one INTEGRATOR owns `main` (serial `--no-ff` merges, gates re-run on the merged tree, headless check on the rebuilt stack, push, ticks, spine); nine builder lanes work in parallel worktrees. Each contract has ten parts: objective, current state, desired state, allowed files, architecture rules, binary acceptance, literal validation commands, non-goals, stop conditions, commit slices; lanes verify the premises they are handed and report what they could not verify; a file lock serializes the 10-minute .NET suite; ports are assigned per lane; lanes never touch the spine (proposed D-rows and doc text go in their reports).
Why: parallelism without merge chaos; machine gates, not an agent's say-so, decide a merge; one integration point; the human sets the topology.
Consequences: refines ADR-014 / D-065 (roles split three ways). The contracts are kept verbatim in `docs/ai/briefs/fleet-2026-11-28/`. The outcome (merge count, times, reds caught at integration) is recorded at the wrap.
Who: owner (overrode the AI's "go wider" proposal; set the topology), AI (contracts, gates)
Status: accepted
Promote: no

## D-065 — Owner-steered parallel lanes, verified before merge, run overnight under a loop — 2026-11-28
WS: WS-22
Context: from 02:41 the owner sent screen-by-screen asks faster than one agent can build them, then went to sleep with the submission at 15:00.
Decision: ADR-014. One lead owns main and the spine; each ask is a lane in its own worktree with a written brief and a model sized to the work; no lane pushes or merges; the lead merges only after re-running gates, reading the lane's screenshot and a headless check of the merged result; the pre-push hook runs the full .NET suite. Overnight the lead works `docs/plans/overnight-2026-11-28.md` under `/loop`, resuming from hot-state after each compaction; feature cut-off 13:00, clean demo stack by 14:00.
Why: speed without losing the "every done is verified" promise; the record (ADR, D-rows, journal, failure modes) is part of what the panel sees.
Who: owner (the way of working, overnight run), AI lead (gates, lane rules, cut-off)
Status: accepted
Promote: ADR-014

## D-064 — Submission-morning asks past the freeze: shell polish, USD view, add users, onboarding — 2026-11-28
WS: WS-22
Context: at 02:41 on submission day (15:00) the owner asked, with screenshots: no box inside the page card; the SLA calendar day as a side drawer; the reserve workbook with sheet tabs like the Wishes2Vows one; "where the money is" with USD amounts, bar and pie charts; a first-visit welcome that points at the README and autoplays the tour; adding users from the console ("UAM is incomplete"); a better My profile.
Decision: lift the freeze for exactly these. The lead does the shell polish, calendar drawer and workbook on main. Three lanes in separate worktrees: nth-money (an INDICATIVE USD view at fixed, dated, labelled rates, shown beside the per-currency truth, never replacing it: the per-currency figures stay the only exact numbers, and the USD total is marked indicative), nth-uam (admin creates a user with roles: new permission `USERS_MANAGE` granted to ADMIN by migration with a catalog row, validation, audit, tests), nth-onboard (first-visit welcome with the README steps and Help, autoplay the driver.js tour once per user, My profile redesign). Each lane merges only on green gates and a headless browser check.
Why: the asks are visible on the first screens of the walkthrough; the USD view answers "how big is this in one number" without breaking the never-add-across-currencies rule, because it is labelled as a conversion at stated rates.
Who: owner (asked, lifted the freeze), AI lead (indicative-rate rule, lane split, merge gate)
Status: accepted
Promote: no

## D-063 — Claims search on Meilisearch (one feature past the freeze) — 2026-11-27
WS: WS-22
Context: at 20:53, with v0.0.2 tagged, the owner asked what Meilisearch should index next ("claims? customers?") and to add it. Customers are the starter example the brief never asks for; claims are what officers look up by number, policy, claimant or place.
Decision: index claims, not customers. Same pattern as audit search (D-058): a background indexer copies claim rows into a Meilisearch index; `GET /api/v1/claims/search` applies the claimant scope SERVER-SIDE as a Meilisearch filter (a claimant only ever gets their own claims; the browser never talks to Meilisearch), returns 503 `search.unavailable` when the engine is down, and the console falls back to the existing list. Built in lane nth-claimsearch and merged only if every gate and a browser check pass; otherwise parked as a prepared answer. The freeze (D-062) otherwise stands.
Why: it answers the brief's "what does an officer need to manage their workload" with a real lookup, and it reuses a reviewed pattern instead of inventing one the night before.
Who: owner (lifted the freeze, asked for it), AI lead (claims over customers, scope rule, merge gate)
Status: accepted
Promote: no

## D-062 — Feature freeze for v0.0.2; Kafka kept but ignored — 2026-11-27
WS: WS-22
Context: the evening before submission, with every brief ask built and nine lanes merged, the console went blank for a returning signed-in user (NG0203, failure mode 33) although every gate was green. The optional Kafka adapter (lane nth-kafka) finished but had never reported.
Decision: freeze features: fixes and polish only until the walkthrough (the owner's "Under the hood" guard snippets count as polish on a shipped panel). Merge the Kafka adapter as built: `Events:Publisher` defaults to `Webhooks`, so the app behaves exactly as before; the broker runs only under the compose profile `kafka`. Keep Kafka out of `docs/DEMO.md` and out of the README highlights (one "optional" row under "Beyond the brief"), and answer "where is Kafka?" by pointing at the port, the adapter and runbook 12. Release what is on main as v0.0.2 (CHANGELOG entry and git tag; the sidebar already says v0.0.2).
Why: the risk the night before is a regression, not a missing feature; an adapter that is off by default costs nothing at runtime and turns a verbal answer into code.
Who: owner (freeze, keep-but-ignore Kafka, v0.0.2), AI lead (merge after verification)
Status: accepted
Promote: no

## D-061 — Fable interview-readiness review: all 15 changes accepted — 2026-11-27
WS: WS-22
Context: the owner found the app hard to act on; a read-only Fable review mapped the brief to the build and ranked 15 changes. The lead verified its factual claims against the code before acting (claim page never refetched; ARCHITECTURE said "no business domain"; unreserved claims summed as zero exposure; idle officers missing from workload; the seed asked for photos the UI cannot take; a reserve change raised no event): all true.
Decision: accept all 15. Docs now (service-boundaries diagram and split order, entity diagram, sync/async table, async across both tiers, contract ownership, ARCHITECTURE intro, `docs/DEMO.md`); the stepper, next-step card, team page, landings, polling and queue defaults folded into lane nth-claimui; backend follow-ups (reserve event, unreserved count, idle officers, drill-down filters, seeded claim.* subscription, answerable seed question) in lane nth-api2; the optional Kafka adapter built in lane nth-kafka rather than left verbal. The review's advice to keep audit search, the sheet and the calendar off the presentation is adopted in the demo script; they stay in the product because the owner asked for them.
Why: the review answered the brief's named questions where the build was weakest, and every one of its claims survived a check.
Who: AI (Fable reviewer, lead disposition), owner (asked for the review)
Status: accepted
Promote: no

## D-060 — Console shell ported from the owner's reference app and admin-console design — 2026-11-27
WS: WS-22
Context: the owner could not see the shell they asked for: the sidebar's pinned account card hid the last nav group, and the reference shell (collapsible groups, rail mode, progress bar, account menu) was never ported (backlog #003).
Decision: lane nth-shell ports the reference shell into Angular, colours through tokens only, light theme; the nav scroll defect is fixed first as its own commit. The reference apps are not named in this public repo.
Why: the owner's design reference is the bar; one shell for every persona.
Who: owner (asked), AI (scope)
Status: accepted; delivered as an ADAPTATION, not a port (correction 2026-11-27 19:56)
Correction: the owner, looking at the result, said the shell was never ported (only the breadcrumbs). Lane nth-shell's own report agrees: it kept the console's item style instead of the reference's tree line and sliding card and left out the theme switch, the inbox bell and click-to-expand rail (the header search came later from nth-polish). What landed: the nav scroll fix, collapsible remembered groups, rail with tooltips, progress bar, account menu, eyebrow header and breadcrumbs. A faithful port is parked until after submission (D-062 freeze).
Promote: no

## D-059 — Nice-to-have UI on Univer and FullCalendar, lazy-loaded — 2026-11-27
WS: WS-22
Context: the owner asked to see the nice-to-haves: a spreadsheet surface and a calendar. The owner's own note on trimming them for memory-capped deploys is still owed.
Decision: a staff reserve-review sheet on Univer (`@univerjs/presets`; only the reserve column editable; save sends one If-Match reserve command per changed row and shows per-row results) and an SLA calendar on FullCalendar (MIT core; one event per open claim on its computed due date). Both are lazy routes so the initial bundle does not grow; the lane records the measured chunk sizes.
Why: both earn their weight on screens where a grid or a calendar is the natural view; lazy loading keeps the rest of the console light.
Consequences: the sheet saves row by row through the existing guarded command (no bulk endpoint); a bulk reserve command with partial-failure semantics is a "more time" item.
Who: owner (asked), AI (libraries and shape)
Status: accepted
Promote: no

## D-058 — Audit search on Meilisearch, fed by an indexer — 2026-11-27
WS: WS-21
Context: W-021 had planned Postgres full-text search and marked Meilisearch rejected; that rejection was an AI call never put to the owner. The owner asked where Meilisearch is.
Decision: Meilisearch as an optional compose service; an `IAuditSearch` port with a Meilisearch adapter; a background indexer copies `audit_log` rows (already masked) in keyset batches, idempotent by id, checkpoint kept in the index (no migration); `GET /api/v1/audit/search` behind `AUDIT_READ`. With no URL configured search answers 503 `search.unavailable`, so the host-run API and tests need no search engine.
Why: typo-tolerant, ranked, highlighted search over the audit trail is what operators expect; the append-only table stays the source of truth and the index is rebuildable from it.
Consequences: search is eventually consistent (indexer lag); one more container in the stack.
Who: owner (asked), AI (shape)
Status: accepted
Promote: no

## D-057 — Console money: ISO code plus two decimals for every market currency — 2026-11-27
WS: WS-20
Context: the API stores every amount as bigint minor units with two minor digits; `Intl.NumberFormat` gives IDR zero fraction digits and renders SGD and HKD both as "$".
Decision: the console shows every amount as `<ISO code> <amount with two decimals>` (`IDR 2,500.00`, `SGD 2,000.00`), formatted in one helper (`web/src/app/features/claims/claims-logic.ts`) and documented in the web README. Exposure totals are per currency only.
Why: a reader must never confuse two currencies or lose the decimals the API stores; for claims staff across six markets the code is clearer than a symbol.
Consequences: IDR shows ".00" that a local reader may find unusual; a per-currency minor-digit table (ISO 4217) is the "more time" refinement, with the API storing minor units to match.
Who: AI (web lane proposed, lead accepted)
Status: accepted
Promote: no

## D-056 — Codex claims seam review: three accepted, two declined — 2026-11-27
WS: WS-20
Context: Codex (read-only, gpt family) reviewed `main..claims-core` at 506fc47 for the state machine, visibility, permissions, concurrency, atomicity and money. No illegal transition found; five findings.
Decision: accept #1 (a same-value reserve skips the xmin check: every claim command now forces the guarded UPDATE) and #5 (claim submit requires `Idempotency-Key`, 400 `idempotency.key_required`; other creates stay optional per ADR-013); accept #3 as a doc fix (plan H amended to the dotted event names the fitness rule forces). Decline #2 (handler-level permission checks: authorization is enforced at the HTTP edge and pinned by an arch test; handlers scope data only) and #4 (a 403 for lacking the permission is identical for every id, so it reveals nothing).
Why: fix what a real race or a real duplicate can break; keep one authorization model rather than two that can disagree.
Consequences: a second transport (Kafka consumer) needs its own authorizer before it dispatches commands: listed under "with more time". Submit clients must send a key (the console already does).
Who: AI (review and disposition), Codex (findings)
Status: accepted
Promote: no

## D-055 — One file per concept, not per type — 2026-11-27
WS: WS-20
Context: owner review: 250 source files, 53 under 20 lines, 24 one-struct files in `Domain/Ids`; hard to read for people and agents.
Decision: group by concept: all typed ids in one `Ids.cs`; an aggregate keeps its small enums and value types in its own file; ports grouped by concern (identity, audit, messaging); one slice stays one file; list filters bind one `[FromQuery]` record (backlog #005). CLAUDE.md's "one type per file" rule is replaced. Done as one readability pass after the claims backend merges; proof = build 0 warnings, full suite green, OpenAPI snapshot byte-identical.
Why: fewer, fuller files show the shape of the system; tiny files cost opens and context without adding meaning.
Consequences: a large mechanical diff in one commit series; `git log --follow` still traces moved code.
Who: owner (asked), AI (rules)
Status: accepted
Promote: no

## D-054 — Raw SQL lives in .sql files; a read-only Postgres MCP for agents — 2026-11-27
WS: WS-20
Context: owner review: 28 raw SQL statements are inline string constants across 11 Infrastructure files.
Decision: runtime SQL moves to embedded `.sql` files under `src/Fugue.Infrastructure/Persistence/Sql/<area>/`, loaded once by `SqlScripts.Get("area/name")`, with a test that every referenced script exists. Schema migrations stay EF Core; `npm run db:script` exports them as one idempotent `.sql` script any Postgres runner or psql can apply. Agents inspect the database through a Postgres MCP server in restricted (read-only) mode using a SELECT-only `fugue_readonly` role, configured in the project `.mcp.json`.
Why: SQL that is reviewable as SQL (highlighting, paste into pgAdmin, DBA review); migrations runnable without .NET; agents can read live data and plans but cannot write.
Consequences: a script name can drift from its caller: the existence test plus the integration tests that execute every statement catch it.
Who: owner (format and the MCP), AI (loader, role, export)
Status: accepted
Promote: no

## D-053 — Claims platform shape for the window — 2026-11-27
WS: WS-20
Context: the take-home brief (motor and property claims, six markets, claimants, staff, managers, one Angular app) is built inside a 5-hour window.
Decision: one CLAIMS module in the monolith; a Claim aggregate with its state machine; an append-only claimant timeline; info requests; claimant-only visibility (404 otherwise); seven permissions and a new MANAGER role; REST for every command and query, an outbox event per transition (webhooks now, Kafka adapter keyed by ClaimId later); workload and exposure as plain SQL over live rows, never summed across currencies. Details: `docs/plans/claims-window.md`.
Why: claim state, timeline and event must commit together; the scaffold already provides concurrency, audit, outbox and the console, so the window goes to the domain.
Consequences: cut lines are listed in the plan; projections, saga and market-scoped toggles are "more time" items.
Who: AI (lead) proposed, owner approved the window
Status: accepted
Promote: no

## D-052 — The claims window runs in this repo, opened by a tag — 2026-11-27
WS: WS-20
Context: the brief evaluates how much is built in 2-3 h (5 h cap) with AI; there is no proctored session and the panel confirmed a disclosed starter is acceptable.
Decision: build the claims platform in the Fugue repo after an annotated tag `claims-window-start`; everything before it is the disclosed scaffold, everything after it is the window. Two scaffold lanes started before the tag (idempotency fix round, console shell pass) land after it and keep `Refs: W-007`.
Why: one repo keeps the full AI-orchestration history as evidence, and `git diff claims-window-start..HEAD` gives the panel the exact window.
Consequences: README states the disclosure and the tag on its first lines; the journal logs the window as it happens.
Who: owner (window now, disclosure confirmed) and AI (tag mechanism)
Status: accepted
Promote: no

## D-051 — W-018 audit import: four ideas ported, nine rejected — 2026-11-27
WS: WS-18
Context: a read-only sweep compared the owner's earlier audit package with W-012's `audit_log` (findings kept outside the repo; the source is not named here, D-048).
Decision: port (1) 401 challenges recorded in `access_audit` (`unauthenticated`, reason `missing_token`/`expired`/`invalid_token`) with a per-IP rows-per-minute cap; (2) failure rows gain `error_type`, `error_message` (the authored `FugueError` message only, capped; an unhandled exception stores its type name, never its message) and `duration_ms`; (3) a `Fugue.Audit` meter counting lost audit writes plus a structured fallback log carrying the lost row's identifying fields; (4) a fitness test that no audited command falls back to an `app.*` action. Rejected: async buffered writer, sampling, request/response body capture, regex PII masking over JSON, first-hop X-Forwarded-For trust, header-based caller attribution, DELETE-based purge, dual master/tenant writes, an external search index.
Why: each rejected item breaks a W-012 guarantee (same transaction, append-only, never sampled, write-time structural masking, identity from the verified token) or needs infrastructure Fugue does not run. The ported items close real gaps: a bad token left no trace, a failure row said only its code, and a lost failure row was invisible.
Consequences: one additive migration on `audit_log`; the audit API and the OpenAPI snapshot gain three nullable fields; anonymous 401 floods are bounded by the cap and counted.
Who: AI (headless lead), from the sweep
Status: accepted
Promote: no (amends ADR-010 in place: consequences section)

## D-050 — Idempotency keys with stored-response replay on the two create POSTs — 2026-11-27
WS: WS-07
Context: creates are the only writes that are unsafe to repeat; PUT/DELETE/transitions already carry If-Match.
Decision: opt-in `[Idempotent]` resource filter on `POST /customers` and `POST /webhooks/subscriptions`; Structured Field String key scoped to (user, endpoint, key), body fingerprint, atomic claim with a claim token before execute, 2xx responses stored encrypted (Data Protection) and replayed for 24 h; 422 on reuse with a different body, 409 while in progress, a business 4xx frees the key, anything ambiguous stays pending and answers `outcome_unknown`; a claimed key is never reclaimed. Plan: `docs/plans/w007-idempotency.md` (revision 2 after the Codex plan review, REVISE: 5 HIGH / 8 MEDIUM / 1 LOW, all addressed or accepted with reasons in decision J).
Why: the IETF draft shape, minimal surface, keeps HTTP out of Application; encryption because the webhook create response carries a one-time secret.
Consequences: a crash between commit and completion leaves a stuck key (never a duplicate); the key ring is in the same database, so encryption protects against table leaks only; snapshot + console client change.
Who: AI (headless lead)
Status: accepted
Promote: yes (ADR-013)

## D-049 — Keep the W-017 Angular console; no template import — 2026-11-27
WS: WS-07
Context: the owner asked whether an Angular template should be cloned, and offered a Lovable prototype of the same seven-screen brief.
Decision: no template. `web/` already is the Angular 22 console (W-017, eight screens, tests and e2e). The prototype is React (TanStack Router, shadcn) with a dark crimson palette; nothing is copied from it. Its users list layout is the reference for the W-019 list only.
Why: porting React components to Angular costs more than building the one missing list; the Vellum palette (D-045) is already the console's scheme.
Consequences: no new dependency; W-019 builds the list in the existing design system.
Who: AI (headless lead), on the owner's question
Status: accepted
Promote: no

## D-048 — The owner's earlier design repo may be copied directly; names are scrubbed on import — 2026-11-27
WS: WS-18
Context: the owner's earlier work (audit logging and related patterns) now sits in a repo the owner controls, cloned read-only at `~/Repos/career-work/` (outside this repo).
Decision: agents may copy code from it directly instead of re-deriving the pattern, because the owner states full authority over it. Every import is renamed to Fugue conventions and must pass the scrub gate (no employer, client or product names) and the existing fitness rules.
Why: one day to submission; re-deriving proven code costs time the owner does not have.
Consequences: imported code gets the same tests, review and gates as new code; the source repo is never referenced by name in Fugue.
Who: owner
Status: accepted
Promote: no

## D-047 — Live permissions stream (SSE) cancelled — 2026-11-27
WS: WS-17
Context: a server-sent events stream at `/api/v1/me/events` was designed so the nav updates the moment a module is toggled or grants change.
Decision: not built. The console refetches `GET /api/v1/me` after login, on token refresh and on window focus.
Why: owner call to protect the submission timeline; the refetch covers the demo.
Consequences: a toggle shows up on the next focus or navigation, not instantly. Revive trigger recorded in `docs/archive/deferred.md` terms: when a second instance or a live-ops screen needs push.
Who: owner
Status: accepted
Promote: no

## D-046 — Console stack: NgRx SignalStore for client state, TanStack Query for server state — 2026-11-26
WS: WS-17
Context: the brief requires Angular; the owner first proposed Zustand.
Decision: Angular (latest stable) in `web/`; NgRx SignalStore for session and UI state, TanStack Query for Angular for server state (fetched data never lives in the store); Tailwind + Angular CDK; TypeScript client generated from `docs/api/openapi.v1.json`; screens stay domain-neutral.
Why: SignalStore keeps the Zustand feel without a React library in an Angular codebase; splitting client and server state avoids a hand-rolled cache.
Consequences: contract drift breaks the console build instead of the demo.
Who: owner (AI proposed Zustand + TanStack first; owner overrode to SignalStore)
Status: accepted
Promote: no

## D-045 — Console palette: Vellum light — 2026-11-26
WS: WS-17
Context: five palettes were compared on one rendered screen; a crimson brand accent was rejected because red must mean error.
Decision: Vellum (aged paper, Prussian-blue accent), light mode only; red, amber and green are status colours only; tints derived with `color-mix()` from one token file.
Why: projector and screen-share legibility for the demo; status colours never collide with the brand.
Consequences: a dark theme is a token swap later.
Who: owner
Status: accepted
Promote: no

## D-044 — W-016 GET /api/v1/me is the console's source for navigation and permission checks — 2026-11-26
WS: WS-16
Context: the console must hide what a user cannot do and show what is switched on, without learning it from 403s or duplicating role rules.
Decision: `GET /api/v1/me` (any signed-in user, module SYSTEM, `no-store`) returns the caller's own identity, active roles and groups, the effective permission codes from the same query the authorizer runs, and the effectively enabled modules (code, name). The subject is the token's user only; no header or body can name another.
Why: one server-side definition of "effective" (deny > allow > role, expiry, disabled modules) feeds both enforcement and the UI; the cost is one more indexed query per call.
Consequences: a tenant id and email are returned to their owner only; the console treats the permission list as a hint (the API still enforces); the signed-in-only action set is closed by a fitness test.
Who: AI, owner brief
Status: accepted
Promote: no (in ADR-012)

## D-043 — W-016 console CORS policy: exact origins, no credentials, named headers — 2026-11-26
WS: WS-16
Context: the Angular console runs on its own origin and sends `Authorization`, `If-Match` and correlation headers, and must read `ETag`, `Link`, `X-Next-Cursor` and friends.
Decision: one named policy `Console` from `Cors:Origins` plus `http://localhost:4200` in Development only; each origin must be exact (a wildcard, path or trailing slash stops the host); no credentials; methods GET POST PUT PATCH DELETE; the request and exposed header lists in ADR-012. The middleware sits before authentication.
Why: a bearer token in a header needs no credentialed CORS, so the narrowest policy that works is the safe one; failing at start beats a silent over- or under-match.
Consequences: a new console origin is a config change, not code; production with no configured origin allows no cross-origin browser caller.
Who: AI, owner brief
Status: accepted
Promote: no (in ADR-012)

## D-042 — W-016 the backend owns the API contract; the OpenAPI snapshot is a test-gated file — 2026-11-26
WS: WS-16
Context: the console generates its TypeScript client from the API description; an informal description drifts.
Decision: Swashbuckle generates the `v1` document; `docs/api/openapi.v1.json` is its deterministic rendering and is committed. A test regenerates it in memory and fails on any difference with a message naming the refresh command (`npm run api:openapi`). Swagger UI and the live JSON are Development only. Action attributes declare success and the problems an action decides itself; the pipeline-derived statuses and headers come from one operation filter.
Why: the diff of the snapshot is the review of the contract; the console cannot be broken by an unreviewed change.
Consequences: every API change touches the snapshot; new actions need summaries and `[ProducesResponseType]` (fitness); XML doc files are generated for Api and Application.
Who: AI, owner brief
Status: accepted
Promote: yes (ADR-012)

## D-041 — W-006 Codex review of the S4 diff: fence, budget, lease, cancel, envelope — 2026-11-26
WS: WS-06
Context: the cross-family review (`docs/reviews/codex/2026-11-26-s4-webhooks.md`) found one HIGH and four MED, each reproduced by a test before the fix.
Decision: (HIGH) the result write is fenced by a random `claim_token` written at every claim, not by the attempt number, because redeliver restarts the number (migration `AddDeliveryClaimToken`); (MED) an expired lease on the final attempt dead-letters instead of being re-sent; (MED) a queued delivery whose lease cannot cover one more attempt is released unsent and uncounted; (MED) the outbox interceptor also cleans up on a cancelled save; (MED) the Development sink rejects a request whose event id or type header differs from the signed body. Outbox retention (LOW) stays deferred (backlog #001).
Why: each bug let a late or replayed actor change an outcome or send beyond the budget; a token that never repeats is the only fence that survives a counter reset.
Consequences: one nullable column; no change to the signature scheme; receivers are unaffected.
Who: AI, reviewed by Codex
Status: accepted
Promote: no (amends ADR-011 in place)

## D-040 — W-006 the publisher seam, a possible Kafka adapter, and a Development-only sink — 2026-11-26
WS: WS-06
Context: the owner asked "REST or Kafka?". There is no broker in scope; the question is where a broker would plug in.
Decision: `IEventPublisher` (Application) is the seam: the relay hands each claimed outbox message to every registered publisher. The first and only adapter is the webhook fan-out. A Kafka adapter would be one more `IEventPublisher` producing to a topic with the aggregate id as the message key (Kafka orders per key), with the outbox row id as an idempotency header; it is documented in ADR-011 and not built. A Development-only `POST /api/v1/dev/webhook-sink` (anonymous, authenticated by the signature, 404 outside Development) verifies, dedupes by event id and lists the last N, so one API process demonstrates the whole loop.
Why: the outbox already gives atomic capture and at-least-once relay, which is the hard part of "REST or Kafka"; the transport is an adapter. Dual-writing (commit, then publish) loses events when the process dies in between, which is why the outbox exists at all.
Consequences: a broker later costs one adapter and a topic design, not a redesign; ordering is per aggregate only if the adapter keys by aggregate id (webhooks give no ordering guarantee under retries, and receivers must tolerate that).
Who: owner (REST vs Kafka framing), AI (seam shape)
Status: accepted
Promote: yes (ADR-011)

## D-039 — W-006 the dispatcher: two stages, leases, SKIP LOCKED, outbox as the retry authority — 2026-11-26
WS: WS-06
Context: several API instances may run; a receiver can be slow, down or hostile; a crash can happen anywhere.
Decision: stage 1 (relay) claims pending messages with `SELECT ... FOR UPDATE SKIP LOCKED` in one transaction, publishes through `IEventPublisher` and marks them dispatched; the webhook adapter inserts one delivery per matching active subscription with `ON CONFLICT DO NOTHING`; a savepoint per message isolates a poison message. Stage 2 (delivery) claims due deliveries with `UPDATE ... WHERE id IN (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING`, setting `in_flight` and a lease and counting the attempt at claim time, sends over HTTP with NO transaction open, and writes the result fenced by the attempt number; an expired lease is reclaimed, so a crash costs one counted attempt. Backoff is exponential with jitter; `MaxAttempts` then dead letter; `POST /webhooks/deliveries/{id}/redeliver` revives a dead one with a fresh budget. The standard resilience handler provides timeouts, a circuit breaker per authority and a rate limiter; its own retry is one attempt and disabled for POST, because the outbox is the single retry authority (durable, counted, minutes of backoff, visible in `webhook_deliveries`). Signature `X-Fugue-Signature: v1=hex(HMAC-SHA256(secret, timestamp + "." + rawBody))` with `X-Fugue-Event-Id`, `-Event-Type`, `-Timestamp`, `X-Correlation-Id`, `X-Saga-Id`, `traceparent`; delivery is at-least-once, so receivers verify the signature, reject timestamps older than 5 minutes and dedupe on the event id.
Why: holding row locks across HTTP calls would pin connections and block redelivery; leases make the claim short and crash safe; one retry authority keeps attempts honest.
Consequences: a receiver that is down for the whole backoff schedule dead-letters (visible, redeliverable); two instances never deliver the same row concurrently, but a lease expiry during a very slow send can double-send, which dedupe on the event id absorbs; no per-aggregate ordering.
Who: AI, owner brief
Status: accepted
Promote: yes (ADR-011)

## D-038 — W-006 subscription secrets and the SSRF guard — 2026-11-26
WS: WS-06
Context: signing needs the raw secret, so it cannot be hashed; a subscription URL is attacker-influenced input that the server will call.
Decision: the secret (`whsec_` plus 32 random bytes) is encrypted with ASP.NET Core Data Protection (purpose `Fugue.Webhooks.Secret.v1`), the key ring is stored in the database (`data_protection_keys`, `[NoAudit]`) so every instance and test host decrypts the same data, and the secret is returned once on create and on rotate, never on read; rotation replaces it immediately. The SSRF guard accepts https only (no userinfo), resolves the host and rejects loopback, private, link-local (including the cloud metadata address), carrier-grade NAT, multicast and reserved ranges, IPv4-mapped IPv6 included. It runs at subscribe time AND at connect time (the `ConnectCallback` resolves, checks and dials the verified address, which defeats DNS rebinding); redirects are never followed. `Webhooks:AllowInsecureLocal=true` permits http, loopback and private ranges for local demos, never link-local, and the host refuses to start with it outside Development.
Why: ciphertext in the table is useless without the key ring; a subscribe-time check alone is bypassed by a name that resolves to a public address first and a private one later.
Consequences: the key ring and the ciphertext sit in the same database, so a database dump exposes both; production should protect the key ring at rest (certificate or a key service) through Data Protection's `ProtectKeysWith*`, which is a deployment step recorded in the runbook, not built. Losing the key ring makes every stored secret unreadable (rotate to recover).
Who: AI, owner brief
Status: accepted
Promote: yes (ADR-011)

## D-037 — W-006 the transactional outbox — 2026-11-26
WS: WS-06
Context: S4 needs events that exist if and only if the business change committed, with no broker in scope.
Decision: `outbox_messages` (UUIDv7 id, versioned event type such as `customer.created.v1`, aggregate type and id, jsonb payload, correlation id, saga id defaulting to the aggregate id, traceparent, status pending/dispatched/dead, attempts, next attempt, last error, dispatched at) is written in the SAME transaction as the change. Customer raises domain events (created, updated, suspended, reactivated, deleted); an EF SaveChanges interceptor turns them into outbox rows in the same SaveChanges, and the Dapper insert path calls `IOutbox` inside the unit of work (the audit pattern). The payload is thin and masked with the audit redaction rules (email and phone partially masked, never secrets); receivers fetch the resource by id. The row is not `IAuditable` (it is on the append-only allowlist with `occurred_at`); status columns may change, a trigger rejects any change to the event columns, DELETE stays legal for retention. The entity is `[NoAudit]`.
Why: a commit followed by a publish loses the event when the process dies between the two; the outbox turns that into one atomic write plus a retryable relay. A frozen payload means what was committed is what is delivered.
Consequences: every event type needs a versioned name constant (fitness function); a new Dapper write that must publish calls `IOutbox`; the outbox grows until a retention job exists (deferred).
Who: AI, owner brief
Status: accepted
Promote: yes (ADR-011)

## D-036 — W-013 S3 review hardening of the admin surface — 2026-11-26
WS: WS-13
Context: the remaining MED and LOW findings of the S3 build review (rv-s3-build.md) after the two owner calls.
Decision: (1) override PUT/DELETE on an existing row require `If-Match` (xmin; 428 without, 412 stale, 412 for a concurrent create or a version sent for a vanished row), creating sends none; the list carries `version`. (2) group membership cannot target oneself (409 `group.self_target`), as overrides already could not. (3) unique-violation and lost-update races in group add/remove and override set are answered (add: success, remove: 404, override: 412), not 500. (4) module configuration fails closed: only true/1/yes/on, blank or absent keep a module enabled; `SYSTEM` can never be config-disabled. (5) `tenants.is_active = false` blocks login, refresh and live tokens, with the same uniform 401. (6) `access_audit` clips reason, ip, user agent and correlation id to their columns instead of losing the row; the email hash uses an HMAC subkey derived from the signing key, and with no usable key stores no hash (the old constant-key fallback was dictionary-reversible). (7) the refresh port takes a `RotationRequest`, not a placeholder token.
Why: each is a way for a stale client, a typo or a race to produce a wrong state or a lost audit row; fail-closed is the safe direction for a kill switch.
Consequences: override API clients must send `If-Match` when changing a row (Bruno and runbook 05 updated); email hashes written before this change cannot be correlated with new ones (the key changed); `audit_log` already records module PATCH (the interceptor), now pinned. Recorded without a fix: raw user GUIDs in `createdBy` (actor ids are the stamp format by design), the anonymous `access_audit` growth (deferred.md, with the audit retention item).
Who: AI (owner calls D-034 and D-035 set the scope)
Status: accepted
Promote: yes (ADR-008)

## D-035 — W-013 login limiter: strict (IP, email) partition plus a loose per-email ceiling — 2026-11-26
WS: WS-13
Context: S-M3. The per-email limiter charged every attempt, so an attacker could hold a victim at 429 from any IP (a lockout lever, the thing D-020 refused to build).
Decision: three limiters, all evaluated before password verification and chained so a request must pass each: per IP (unchanged), strict per (client IP, canonical email), and a loose per-email ceiling (default 50 per 15 minutes, configurable) that only unsuccessful attempts charge. A throttled request is a 429 problem+json with Retry-After. Refresh has no email (the token is the credential), so it keeps only the per-IP policy; there is nothing to partition.
Why: one IP hammering a victim exhausts only its own (IP, email) bucket, so the victim from another IP still signs in; the loose ceiling still bounds a distributed guesser while leaving real headroom.
Consequences: 50+ failed attempts from any mix of IPs push a victim's logins to 429 for the window (accepted trade-off; one IP at the strict rate needs about ten minutes of sustained abuse, and the victim's own good logins never charge the ceiling). Partition state is in memory per process.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-034 — W-013 customer email is unique per owner — 2026-11-26
WS: WS-13
Context: C3 / S-M1. A global unique index on customers.email made create a cross-owner existence oracle (409 vs 201) for restricted callers.
Decision: unique on (user_id, canonical email) for owned rows, plus a partial unique index on email `WHERE user_id IS NULL` for unowned (staff-created) rows. Customers are hard-deleted, so every row counts. The 409 `customer.email_taken` stays but can only fire inside the target owner's scope; a CUSTOMERS_ALL caller creating for another owner can still hit it (they see everything anyway).
Why: per-owner uniqueness removes the oracle without a generic-response trick; two users can legitimately hold the same contact.
Consequences: migration `CustomerEmailPerOwner` swaps the index (Down restores the global index and fails if per-owner duplicates exist by then). Deleting a user sets their customers' owner to NULL (FK SET NULL) and could collide with an unowned row; users are not hard-deleted today.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-033 — W-012 retention and archival of the audit log are deferred — 2026-11-26
WS: WS-12
Context: an append-only table grows forever; the owner asked for the log, not for its lifecycle.
Decision: no retention, archival or partitioning in W-012; the revive trigger is in `docs/archive/deferred.md`.
Why: partitioning by month plus a detach policy is a separate migration and an operational decision (how long, where to).
Consequences: the table is unbounded until that thread opens; the append-only trigger must be reconsidered then (a retention job needs a controlled bypass).
Who: AI (owner scoped it out in the brief)
Status: accepted
Promote: no

## D-032 — W-012 grant_audit and access_audit stay specialised — 2026-11-26
WS: WS-12
Context: three audit tables risk overlap.
Decision: `audit_log` = data changes plus command outcomes; `access_audit` = authN/authZ events; `grant_audit` = privilege changes. They stay separate and `audit_log` excludes them.
Why: different shapes (hashed email, subject and target) and different write guarantees (best effort for anonymous callers); folding would churn S3's tests and let a login flood into the data log.
Consequences: a grant leaves one `grant_audit` row and one `audit_log` row per changed entity (two views of one change).
Who: AI
Status: accepted
Promote: yes (ADR-010)

## D-031 — W-012 read API behind AUDIT_READ, admin only, keyset paged — 2026-11-26
WS: WS-12
Context: the log must be queryable by who, what, which row, when and which request.
Decision: `GET /api/v1/audit` (`[Module("AUDIT")]`, `[HasPermission("AUDIT_READ")]`), exact-match filters, newest first, cursor over `(occurred_at, id)`. `AUDIT_READ` is granted to `ADMIN` only; staff do not get it (an override can grant it).
Why: a row shows other users' data; least privilege is the safe default.
Consequences: no write verb exists; the response never un-masks (masking is at write time); tenant filtering waits for the ADR-003 toggle.
Who: AI (owner asked for the permission; "staff read?" was left open)
Status: accepted
Promote: yes (ADR-010)

## D-030 — W-012 safety knobs for the audit log — 2026-11-26
WS: WS-12
Context: an audit table is a PII and secret magnet.
Decision: mask at write time (built-in `PasswordHash`, `TokenHash`, `SigningKey`, a configurable list, `[AuditMask]`, `[NoAudit]`), partial-mask email and phone by default, bound `changes` with `MaxChangesBytes` and a `_truncated` marker, optional client IP, per-entity exclusions, never sample, refuse `Audit:Enabled=false` outside Development.
Why: nothing sensitive is stored, so no read path can leak it; the size bound keeps one bad row from bloating the table.
Consequences: a masked-field change shows as `*** -> ***` (it changed, the value is not recorded); partially masked emails cannot be searched by value.
Who: owner (requirements) and AI (defaults)
Status: accepted
Promote: yes (ADR-010)

## D-029 — W-012 failed commands are recorded by AuditBehavior — 2026-11-26
WS: WS-12
Context: a command that fails with a business error changes nothing, so the interceptor never sees it.
Decision: `AuditBehavior` (order Logging, Audit, Validation) writes ONE failure row with the error code in one insert on its own connection and transaction (it survives a surrounding rollback), and opens the ambient scope the interceptor reads; successes stay with the interceptor so there are no double rows. Authentication commands carry `[NoAudit]` (their record is `access_audit`).
Why: a tracked context may be dirty after a failed save and a surrounding transaction may be aborted; an independent plain insert depends on neither. Validation failures are audited because the behavior wraps validation.
Consequences: a failing audit write is logged, never surfaced; failure rows are best effort, success rows are transactional.
Who: owner (requirement) and AI (placement)
Status: accepted
Promote: yes (ADR-010)

## D-028 — W-012 audit rows are written in the same transaction as the change — 2026-11-26
WS: WS-12
Context: an audit row that can outlive a rolled-back change, or be missing from a committed one, is worse than none.
Decision: an EF `SaveChangesInterceptor` adds the rows to the same SaveChanges; Dapper writes call `IAuditTrail` through `SqlExecutor` inside the same `IUnitOfWork` transaction (or a short one). A fitness rule makes every raw SQL write call `IAuditTrail` or sit on a baseline.
Why: atomicity without a second transaction; the change tracker already holds old and new values.
Consequences: a new Dapper write path fails the build until it records its audit row.
Who: owner (requirement) and AI
Status: accepted
Promote: yes (ADR-010)

## D-027 — W-012 audit_log table shape, append-only trigger, indexes — 2026-11-26
WS: WS-12
Context: one general, rich audit table on the S3 seam.
Decision: columns for who, what, change (jsonb), context and metadata; CHECKs on `actor_type` and `outcome`; a trigger rejects UPDATE, DELETE and TRUNCATE; a btree per filter ending in `occurred_at DESC, id DESC`; no BRIN.
Why: the trigger stops the app and accidents (not the table owner; a non-owner role is deferred); the index shape serves the keyset order directly.
Consequences: rows are never edited, so a wrong masking rule cannot be fixed in place (fix forward).
Who: owner (requirements) and AI
Status: accepted
Promote: yes (ADR-010)

## D-026 — S3 base entity contract: IAuditable everywhere, append-only allowlist — 2026-11-25
WS: WS-05
Context: the owner wants one base contract for every entity.
Decision: every mutable entity (UAM tables, Customer, tenants, groups, refresh tokens) implements `IAuditable`; append-only audit rows use `occurred_at` plus an actor and sit on a shrink-only allowlist; a fitness function enforces it. A rich general `audit_log` is the next batch; the audit tables share the `IUnitOfWork` transaction and read `ICorrelationContext` as the seam.
Why: one stamping path, no entity forgotten.
Consequences: `tests/Fugue.ArchTests/Baselines/append-only-entities.txt`.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-025 — Customers PUT, suspend, reactivate answer 204 + ETag — 2026-11-25
WS: WS-05
Context: mutations returning a full body invite stale reads; `SendNoContent<T>` exists (4e8ee4e).
Decision: Customers `PUT`, `suspend`, `reactivate` return 204 with the new ETag; DELETE already 204. Clients GET to read the result.
Why: owner preference; the ETag header still chains If-Match.
Consequences: integration tests, Bruno scripts and runbook 04 updated.
Who: owner
Status: accepted
Promote: no

## D-024 — S3 scope: tenants, refresh tokens, groups, grant_audit, access_audit built basic — 2026-11-25
WS: WS-05
Context: the owner moved the previously deferred UAM v3 pieces into S3 as minimal versions, sliced after the core security slice.
Decision: tenants (table, nullable `users.tenant_id`, NO query filter); refresh tokens (`refresh_tokens`, SHA-256 hash only, family_id, rotation, reuse revokes the family, `/auth/refresh`, `/auth/logout`, rate limited); groups, group_members, group_roles (effective = user roles + group roles, then overrides); grant_audit (same transaction as the grant) and access_audit (login, refresh, reuse, 403, module-disabled).
Why: owner call; each is small once the schema exists.
Consequences: removed from `docs/archive/deferred.md`; if time runs out the build stops after groups.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-023 — Module management: config kill switch plus runtime registry — 2026-11-25
WS: WS-05
Context: the owner wants toggleable modules.
Decision: `Modules:<CODE>:Enabled` (config, default true) is the hard switch; the `modules` table `is_active` is the runtime switch; effective = both. `[Module("CODE")]` on feature controllers + an action filter -> 404 `module.disabled`; `GET /modules`, `PATCH /modules/{code}` (`MODULES_MANAGE`, If-Match); disabled modules' permissions are not effective; `SYSTEM` cannot be disabled.
Why: a kill switch operators control without a deploy of data, plus a runtime toggle with audit.
Consequences: fitness: every feature controller has exactly one `[Module]`.
Who: owner
Status: accepted
Promote: yes (ADR-009)

## D-022 — S3 abuse controls and key hygiene (Codex 2, 10) — 2026-11-25
WS: WS-05
Context: Codex REJECT findings 1, 2, 5, 6, 10.
Decision: create binds the owner to the caller without effective `CUSTOMERS_ALL`; built-in rate limiter on token and refresh; email change staff-only; one canonical email key with an active-row unique index; signing key = base64 of at least 32 random bytes, placeholders refused at startup.
Why: each closes a reviewed hole with a test.
Consequences: see `docs/plans/s3-identity.md` review resolutions.
Who: review (Codex), owner accepted
Status: accepted
Promote: yes (ADR-008)

## D-21 — S3 deferrals and exclusions from UAM v3 — 2026-11-25
WS: WS-05
Context: the owner wants a lean port.
Decision: Not built in S3: SSO columns, tenant query filter (ADR-003 toggle stays off), domain tables, user and role CRUD, password reset, lockout, RS256, permission cache. Refresh tokens, tenants, groups, grant_audit and access_audit moved INTO S3 by D-024. Rest parked in `docs/archive/deferred.md`.
Why: each is outside the S3 acceptance and the ~3 day horizon.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: no

## D-20 — S3 login failure is uniform; no lockout — 2026-11-25
WS: WS-05
Context: login must not reveal whether an email exists.
Decision: One `auth.invalid_credentials` for every failure and one hash verify on every attempt (dummy hash when no user); no lockout; the built-in rate limiter (by IP and by canonical email, before verification, 429 + Retry-After) is IN S3 (Codex finding 2).
Why: removes the enumeration and timing oracle without new state or a lock-out-a-victim vector.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-19 — S3 ownership by repository scoping, 404 on cross-owner — 2026-11-25
WS: WS-05
Context: self-service users see only their own record while Application has no ASP.NET dependency.
Decision: `CUSTOMERS_ALL` widens scope; handlers build an `OwnerScope` from `ICurrentUser`; repositories filter; a miss is NotFound.
Why: filters lists, leaks nothing, keeps layering; `IAuthorizationService` resource handlers fail all three.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-18 — S3 permissions by uncached lookup in OnTokenValidated — 2026-11-25
WS: WS-05
Context: permissions must be revocable faster than the token TTL without bloating the token.
Decision: Effective permissions are one indexed DB query per authenticated request in `OnTokenValidated` (once per request, can fail the ticket), added as in-memory claims. NO cache in S3; HybridCache is S5. Supersedes the first draft (60 s cache), rejected by Codex findings 3-4.
Why: immediate revocation, no invalidation races, expiry boundaries exact.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-17 — S3 lean UAM v3 port; precedence deny > allow > role — 2026-11-25
WS: WS-05
Context: the owner's UAM v3 design is the model; the owner overrode the earlier recommendation to defer per-user overrides.
Decision: Port users, modules, resources, actions, permissions, roles, role_permissions, user_roles, user_permission_overrides (with `expires_at`); precedence deny override > allow override > role grant; overrides are their own, last-cut slice.
Why: precedence was unwritten in v3; the written rule and expiry close the audit gaps.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-16 — S3 JWT bearer, HS256, local token endpoint, refresh in S3 (D-024) — 2026-11-25
WS: WS-05
Context: Fugue needs authentication that Bruno and Testcontainers can use.
Decision: JwtBearer 8.x, HS256 with a 32-byte-minimum key from env, 15 min TTL, 30 s skew, `POST /api/v1/auth/token`, `PasswordHasher<T>`; refresh tokens deferred. CLAUDE.md says Argon2id but `PasswordHasher<T>` is PBKDF2: owner to amend the wording.
Why: one process is issuer and verifier, so an asymmetric key buys nothing yet.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-015 — Commit gates and the safe-command guard — 2026-11-25
WS: WS-08
Context: the owner's two sibling repos learned the hard way that hooks and prompts do not hold a rule; machine checks do.
Decision: `commit-msg` runs `check-refs-trailer` (agent commits need a resolvable `Refs:`) and `check-fix-has-test` (a `fix` touching `src/` touches `tests/` or says `Tests: none (reason)`); a project-level PreToolUse hook (`scripts/safe-command-guard.mjs`) blocks force push, `--no-verify`, hard reset on main, stray `rm -rf`, `curl | sh`, secret printing and an undry-run `ef database drop`.
Why: each rule matches a failure already seen elsewhere; the guard is narrow because a false positive trains people to ignore it.
Consequences: human commits are exempt from the refs gate; hooks stay client-side, so CI remains the real backstop when added.
Who: owner+AI
Status: accepted
Promote: yes (ADR-007)

## D-014 — Codex review gate is advisory, seam-triggered, capped — 2026-11-25
WS: WS-08
Context: cross-family review is valuable on critical seams but quota is finite and an LLM deciding "should I review" gets skipped.
Decision: `scripts/codex-gate.mjs` runs at pre-push, decides from the diff alone (seam globs), dedupes by diff hash, caps at 8 launches per day, runs detached, and never blocks or delays the push. Findings land in `docs/reviews/codex/`. It reads Codex output, never its exit code.
Why: the trigger is a pure function of the diff; non-blocking keeps the owner's override cheap.
Consequences: an override on quota is recorded in the commit or handoff; the gate is advisory, so the review gate matrix in `AGENTS.md` still requires an independent final-diff review.
Who: owner
Status: accepted
Promote: yes (ADR-007)

## D-013 — Beads is a local queue; commits cite only committed ids — 2026-11-25
WS: WS-08
Context: `bd init` rewrites `core.hooksPath`, can add a Dolt remote, and committed its own config on this repo.
Decision: `.beads/` is gitignored; nothing beads-related is committed. A `Refs:` trailer may cite only `W-NNN`, `D-NNN` or `ADR-NNN`. `core.hooksPath` stays unset.
Why: a public repo must not depend on a local database, and an id a reader cannot resolve is noise.
Consequences: beads ids appear only in local notes. See `docs/failure-modes.md`.
Who: owner
Status: accepted
Promote: yes (ADR-007)

## D-012 — Pin every catch — 2026-11-25
WS: WS-08
Context: review catches that are only fixed come back.
Decision: no High or Medium finding closes without a regression test, an architecture test or a type that makes it unrepresentable. Recorded in `AGENTS.md`; the review skill enforces the loop.
Why: a catch you do not pin is a bug you rent.
Consequences: reviewer rounds repeat until no High or Medium remains and each has a pin.
Who: owner
Status: accepted
Promote: no

## D-011 — Fable architecture critique: accepted, with two challenges — 2026-11-25
WS: WS-02
Context: an external review of the S0 plan (see `docs/ai-journal.md`).
Decision: accepted nearly all of it (Dapper deferral was later overridden, D-004); kept `SagaId` alongside W3C trace context; kept runbooks.
Why: the critique was mostly right; the exceptions concern what the owner needs to demonstrate.
Consequences: ADR-005 keeps three correlation layers.
Who: review, owner accepted with two challenges
Status: accepted
Promote: no

## D-010 — OrbStack as the container runtime — 2026-11-25
WS: WS-02
Context: Docker on macOS for Postgres and Testcontainers.
Decision: OrbStack.
Why: least setup friction; fastest in the comparison.
Consequences: Testcontainers needs `DOCKER_HOST` pointed at the OrbStack socket (runbook 03).
Who: AI proposed, owner accepted
Status: accepted
Promote: no

## D-009 — `MapStatus` is the owner's — 2026-11-11
WS: WS-02, WS-09
Context: the ErrorType-to-HTTP-status mapping is the one policy call worth the owner's own hands.
Decision: the method stayed the owner's call. Resolved 2026-11-25: the owner asked for it and approved the AI-written exhaustive switch (commit 4e8ee4e); `ErrorType.Unavailable` -> 503 added.
Why: ownership of the decision is the point.
Consequences: no known-red tests remain; every named ErrorType must be mapped (build breaks otherwise).
Who: owner
Status: accepted
Promote: no

## D-008 — Name: Fugue — 2026-11-11
WS: WS-01
Context: a project name.
Decision: "Fugue" (independent voices over one subject).
Why: brand-only theming; domain types keep plain names.
Consequences: the name appears in namespaces and docs, not in domain types.
Who: AI proposed, owner accepted
Status: accepted
Promote: no

## D-007 — Clean-room rewrite after the template audit — 2026-11-11
WS: WS-01
Context: six parallel Haiku agents audited a previous template; three claims were spot-checked by hand and all were confirmed.
Decision: copy patterns, not files; the old code had committed secrets and a foreign domain.
Why: agent claims are cheap; trust is earned by checking.
Consequences: the spot-check habit is recorded in `docs/ai-journal.md`.
Who: AI audit, owner spot-checked
Status: accepted
Promote: no

## D-006 — MVC controllers over `ApiControllerBase`; Mediator not MediatR — 2026-11-11
WS: WS-01
Context: controller style and mediator library.
Decision: MVC controllers with an abstract `ApiControllerBase` (no generic CRUD controller, no FastEndpoints); source-generated `Mediator` (MIT) because MediatR v13+ is commercial. See ADR-002.
Why: the project exists to practise the plumbing, so keep it visible; avoid licence risk.
Consequences: actions are one-liners; logic lives in handlers.
Who: AI proposed, owner accepted
Status: accepted
Promote: yes (ADR-002)

## D-005 — Shared database with `tenant_id`, no RLS — 2026-11-11
WS: WS-01
Context: the owner overrode the AI's row-level-security proposal. Superseded by D-002 (tenancy becomes a toggle, ADR-003).
Decision: shared database with a `tenant_id` column only (ADR-001).
Why: RLS adds operational weight the project does not need.
Consequences: ADR-001 is superseded, not deleted.
Supersedes: none. Superseded by: D-002.
Who: owner (overrode AI's RLS proposal)
Status: superseded by D-002
Promote: yes (ADR-001)

## D-004 — EF Core reads, Dapper writes, one shared transaction — 2026-11-25
WS: WS-04
Context: an external review suggested deferring Dapper; the owner wants the full toolbox practised.
Decision: EF for reads and xmin-checked updates; Dapper for inserts, upserts and set-based writes; Dapper always on EF's connection and current transaction via `IUnitOfWork`; audit stamped on both paths (ADR-006).
Why: each tool where it is strongest, without losing optimistic concurrency or atomicity.
Consequences: the S4 outbox writes through the unit of work; Dapper never reads aggregates.
Who: owner (overrode a Fable review)
Status: accepted
Promote: yes (ADR-006)

## D-003 — Three-layer correlation: trace, correlation id, saga id — 2026-11-25
WS: WS-03
Context: need to debug across logs, responses and later outbound webhooks; a review suggested dropping the saga id.
Decision: W3C `Activity` trace per hop, `X-Correlation-Id` per request chain, `X-Saga-Id` per business process (ADR-005); validated inbound, echoed outbound, in logs and Activity tags. Behaviors require FluentResults responses (`ResultBase, new()`).
Why: each id answers a different question; validation blocks header and log injection.
Consequences: S4 outbox and webhooks must carry the ids as headers.
Who: owner, from a review suggestion challenged
Status: accepted
Promote: yes (ADR-005)

## D-002 — Retarget .NET 8, adopt FluentResults, single-tenant default — 2026-11-25
WS: WS-02
Context: scaffold targeted .NET 10 with a hand-rolled Result type and multi-tenant e-commerce framing; an external review and the owner's interview context changed the goals.
Decision: target .NET 8 (EOL check disabled on purpose, NuGet audit kept); FluentResults with our `ErrorType` kept (ADR-004); single-tenant by default with a `Tenancy:Enabled` toggle (ADR-003); add an Application layer; `MapStatus` stays the owner's.
Why: CV/interview alignment, ecosystem familiarity, domain-neutral template; reasons in the ADRs and `docs/ai-journal.md`.
Consequences: ADR-001 is superseded; Application must not reference ASP.NET Core (arch test).
Supersedes: ADR-001 (not a D-entry).
Who: owner (overrode AI on .NET version and Result type)
Status: accepted
Promote: yes (ADR-003)

## D-001 — Adopt the doc spine — 2026-11-11
WS: WS-01
Context: AI sessions lose context between runs; hand-written notes drift.
Decision: keep project memory in split files by lifecycle (now, board, why, open, journal, handoff), joined by a thread key and checked by a script.
Why: each file has one job and every link can be machine-checked.
Consequences: every wrap updates the spine in the order in `hygiene.md`; pushes run `scripts/spine/ledger-check.sh`.
Who: owner+AI
Status: accepted
Promote: no
