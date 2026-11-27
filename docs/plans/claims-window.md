# Claims platform: the timed build window (W-020)

The take-home brief (motor and property claims, six APAC markets, claimants + claims staff + managers, one Angular app, 2-3 h target, 5 h hard cap) is built here, on top of the disclosed scaffold. The window starts at git tag `claims-window-start`; `git diff claims-window-start..HEAD` is everything built in it. Started 2026-11-27 ~17:00 +08, hard stop 22:00 +08.

## What the scaffold already gives (not rebuilt)
Layers and slices, typed errors (RFC 9457), ETag/If-Match concurrency, keyset paging, JWT + roles/groups/overrides + module toggles, append-only audit with field diffs, outbox + signed webhooks, idempotency keys, OpenAPI snapshot + generated Angular client, the Angular console shell, one-command Docker stack.

## Decisions for the window
| Id | Decision |
|---|---|
| A | **One deployable, a CLAIMS module.** Modular monolith: claim state, its timeline and its outbox event must commit in one transaction. Service split is a "more time" item along module lines. |
| B | **Claim aggregate** (`claims`): `ClaimId` (UUIDv7), `number` (`CLM-<market>-<yyyy>-<seq>`), `market` (SG, MY, TH, HK, ID, PH), `currency` (fixed per market: SGD, MYR, THB, HKD, IDR, PHP), `line` (Motor, Property), `status`, `claimant_user_id` (owner), `assigned_officer_id?`, `assigned_at?`, `policy_number`, `incident_date`, `incident_description`, `incident_location?`, `reserve_minor?` (bigint minor units, set by the officer), `approved_amount_minor?`, `decision_reason?`, `decided_at?`, `settled_at?`, `xmin` version, `IAuditable`. Money is never float; amounts are never summed across currencies. |
| C | **State machine in the aggregate** (`Claim.Pickup/RequestInfo/ProvideInfo/SetReserve/Approve/Reject/Settle`, each returns `Result`; an illegal move is 409 `claim.invalid_transition`): `Submitted -> UnderReview` (pickup assigns the caller) `-> InfoRequested -> UnderReview` (claimant answers) `-> Approved | Rejected`; `Approved -> Settled`. Terminal: Rejected, Settled. Approve needs a reserve and `0 < amount <= reserve`. Every command takes `If-Match` (412 stale, 428 missing) except submit (Idempotency-Key). |
| D | **Timeline** (`claim_timeline`, append-only trigger like `audit_log`): one row per transition and per info request/response, written in the same transaction: from/to status, actor display, a claimant-safe note. It is what the claimant sees; `audit_log` keeps the internal field diff for staff. |
| E | **Info requests** (`claim_info_requests`): question, requested by/at, response, responded at. At most one open per claim. |
| F | **Visibility**: a claimant sees only their own claims (anything else 404, never 403); staff with `CLAIMS_ALL` see all. Internal reserve is hidden from the claimant DTO. |
| G | **Permissions** (module CLAIMS): `CLAIMS_SUBMIT`, `CLAIMS_READ`, `CLAIMS_RESPOND` (claimant); `CLAIMS_ALL`, `CLAIMS_HANDLE` (pickup, request info, reserve, decide), `CLAIMS_SETTLE`, `CLAIMS_WORKLOAD` (staff); `CLAIMS_EXPOSURE` (manager). Roles: SELF_SERVICE = claimant set; STAFF = READ, ALL, HANDLE, WORKLOAD; new MANAGER role = STAFF + SETTLE + EXPOSURE (seeded `manager@fugue.test`); ADMIN = all. |
| H | **REST vs events**: every command and query is REST (the caller needs the guard result now). Every transition also emits an outbox event (`claim.submitted.v1`, `claim.assigned.v1`, `claim.info_requested.v1`, `claim.info_provided.v1`, `claim.decided.v1`, `claim.settled.v1`) published today as signed webhooks; a Kafka adapter is one `IEventPublisher` keyed by ClaimId (per-claim order). Notifications, exposure feeds and payment are consumers, not part of the request. |
| I | **Workload** `GET /claims/workload` (`CLAIMS_WORKLOAD`): per officer open by status, oldest open age, decided in the last 7 and 30 days, median days submit-to-decision; unassigned count; ageing buckets 0-2, 3-7, 8-14, 15+ days. Plain SQL over live rows (as fresh as the transaction). |
| J | **Exposure** `GET /claims/exposure` (`CLAIMS_EXPOSURE`): rows by market, line, currency with open count, outstanding reserve (open claims) and approved-not-settled amount; totals per currency only. Plain SQL; a projection fed by events is the "more time" item. |
| K | **Angular, one app, two audiences**: routes and nav driven by `/me` permissions. Claimant: My claims, Report an incident, claim detail with timeline and "answer the question". Staff: Claims queue (status tabs + KPI strip), claim detail with actions, Workload. Manager: Exposure. Same interceptors (ETag, correlation, problems) and the generated client. |
| L | **Seed** (Development only): ~20 claims across markets, lines and statuses; `self@fugue.test` owns three. |

## Lanes
1. **Backend core** (Sonnet, worktree `.worktrees/claims`): B-H + L, then I-J. Commits per slice. Contract (controllers + OpenAPI snapshot) lands as early as possible so the frontend can start.
2. **Frontend** (Sonnet, after the contract commit): K, against the regenerated client.
3. **Review**: Codex on the state machine + visibility + permissions seam; fix round.
4. **Lead**: journal entries as decisions happen, verification after every lane, README + runbook + demo script, wrap.

## Cut lines (in order)
Settlement saga with payment failure (keep `Settle`); median decision time; property-specific fields; claimant attachments; manager drill-down; market-scoped module toggles.
