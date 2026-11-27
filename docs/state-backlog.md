# Fugue — backlog (open items)

Append new items at the end. Settled items leave: record the call in `docs/decisions.md`, then remove the item here. All findings go in this ONE file. Do not start parallel finding files.

Heading form (the only one allowed): `### #NNN — <title>`. NNN is three digits. Only the orchestrator mints numbers, after listing all worktrees. Build lanes use a placeholder such as `#NEW-A1`; the orchestrator rewrites it at merge.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)`.
Then: what is wrong or missing · where (path) · size (S/M/L) · what would close it.

Code smells that do not block the work in hand go here, not into the current change.

### #001 — Outbox, delivery and subscription-less event retention
WS: WS-06
`outbox_messages` rows (dispatched or dead) and their `webhook_deliveries` are never pruned, and events with no subscriber still accumulate. Size M. Close with a retention job that deletes dispatched messages older than N days (deliveries cascade) and a setting; the trigger already allows DELETE. Found by the Codex S4 review (LOW), deliberately out of S4 (plan non-goals).

### #002 — KIV: guided product tour (driver.js) on selected console screens
WS: WS-07
The owner wants a driver.js walkthrough as the LAST console touch, only on certain screens (candidates: Customers 412 flow, Audit diff, Modules switch, Webhooks secret-once). Not before submission. Where: `web/` (a tour service + per-screen step lists, opt-in, dismissible, remembered per user in localStorage). Size M. Close when the owner picks the screens and the steps.

### #003 — KIV: console shell closer to the reference design (collapsible nav groups, user card, breadcrumbs)
WS: WS-07
Owner likes a reference shell: grouped sidebar with collapsible sections and count badges, the signed-in user card at the bottom with sign-out, a version footer, breadcrumbs in the top bar, a command search. Where: `web/src/app/features/shell/`. Size M (search palette L). Close with a scoped pass: breadcrumbs + bottom user card + collapsible groups first; search later.

### #004 — KIV: role × permission matrix on the Access screen
WS: WS-07
The owner likes a reference "who can do what" grid: one column per role (with member counts and an Edit action), one row per permission grouped by area, ticks where the role grants it, system roles adjustable but not deletable. Fugue has the data (roles, role_permissions, permissions with modules) but no list endpoint for roles with their grants. Where: a `GET /api/v1/roles` read + `web/src/app/features/access/`. Size M. Close after the claims window.

### #005 — Controllers stack `[FromQuery]` on every list filter parameter (DONE 2026-11-27, merge of `rd-files`)
WS: WS-07
`AuditController.List` (9 filters), `CustomersController.List` and the webhook deliveries list repeat `[FromQuery]` per parameter; with `[ApiController]` they are redundant and the actions stop reading as one-liners. Owner review 2026-11-27. Fix: one `[FromQuery] <X>Filter filter` record per list in `src/Fugue.Api/Contracts/` with `ToQuery()`; proof of safety = the OpenAPI snapshot is byte-identical. Size S. After the claims merge (the claims controller already follows the new style).

### #006 — Claims queue counts cost one list call per status
WS: WS-20
The KPI strip and tab counts read one 100-row page per status (six calls, "100+" when a cursor remains), because the API has no count endpoint and workload is per officer. Fix: `GET /api/v1/claims/counts` (one grouped query under the caller's scope and filters), then one call in the console. Also: the queue's default "All" tab sorts settled and rejected claims among open ones; default to open statuses. Size S. Found by the claims-web lane (2026-11-27).

### #007 — Past info requests only appear as timeline notes
WS: WS-20
The claim DTO carries only the OPEN info request; earlier questions and answers are visible only as timeline notes. Fix: `GET /claims/{id}/info-requests` (scoped like the timeline) and a Q&A list on the detail page. Size S.

### #008 — "Under the hood" panel: technical decisions and live API calls per page (owner idea, not approved to build)
WS: WS-22
A header toggle (`</>` or the `.` key) opens a right slide-over per page with four parts: why this page (2 lines), API calls recorded live by the HTTP interceptors (method, path, status, ms, ETag/If-Match; 409/412 appear as they happen), decisions linking to D-rows and CLAIMS.md sections on GitHub, and the guards that apply on that screen. Plus at most two inline "why" markers per page (dotted underline + hover card), e.g. "No grand total" -> D-057, "Not yours" -> assigned-officer guard. First pages: claim record, queue, team and exposure, claimant landing, report an incident, audit log. Off by default so the demo stays clean. Size M (one lane: a shared component, an interceptor log, per-page note objects co-located with the pages). Waiting for the owner's go.
