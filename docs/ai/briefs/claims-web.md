# Lane claims-web (Sonnet builder): the claims screens in the Angular console, inside the timed window

## Objective
One Angular app serving two audiences (the brief's core frontend question): claimants report and track their claims and answer questions; claims staff work a queue, act on a claim, and see team workload; managers see exposure. Built on the existing console patterns and the generated client, against the real API.

## Lane facts
- Worktree: `~/Repos/personal-work/fugue/.worktrees/claims` (branch `claims-core`). The backend builder has FINISHED the contract there (sha given by the lead below) and may still be committing backend slices 4-7 in the SAME worktree: you own ONLY `web/**` (except `web/src/app/api/generated/**`, which you regenerate, never hand-edit). Commit only `web/` paths by explicit pathspec; never `git add -A`. If a commit races with the backend builder's (index.lock), wait a few seconds and retry.
- Contract: `docs/api/openapi.v1.json` in that worktree. Regenerate: `cd web && npm run api:gen`. Re-run `api:gen` whenever the backend commits a snapshot change (check `git log -1 -- docs/api/openapi.v1.json`).
- Read first: `web/README.md` (architecture: TanStack Query for server state, NgRx SignalStore for client state, interceptors for correlation/auth/ETag/problems, permission codes from `/me`, `*hasPermission`, `permissionGuard`), `docs/plans/claims-window.md` decision K, `app.routes.ts`, the customers feature (`features/customers/*` is the reference for list + drawer + 412 banner + filters store + keyset "Load more"), the modules feature (table + `ui/switch.ts`), `ui/states.ts` (empty/loading/error), `ui/ref-chip.ts`, `api/customers.api.ts` + `api/http-page.ts` (thin wrappers), `core/forms` (server errors -> controls), `styles/tokens.css` (the only place colours live).
- Idempotency: creates send an `Idempotency-Key` via the existing `SubmissionKey` helper (see the customer drawer) - reuse it for "Report an incident".
- Live API for eyeballing: build the API from THIS worktree on a free port, e.g. `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5091 Seed__Enabled=true dotnet run --project src/Fugue.Api` with the env from `docs/runbooks/09-console.md` and a DB: use a THROWAWAY database, never the compose `fugue` DB: `docker run -d --name fugue-claims-pg -e POSTGRES_USER=fugue -e POSTGRES_PASSWORD=fugue -e POSTGRES_DB=fugue -p 5441:5432 postgres:17-alpine`, apply migrations with `dotnet ef database update --project src/Fugue.Infrastructure --connection "Host=localhost;Port=5441;Database=fugue;Username=fugue;Password=fugue"`, then run the API with that connection string and a generated signing key (`openssl rand -base64 32`, never print it). Point `ng serve` at it with a temporary proxy config file you do NOT commit, or run `ng serve --proxy-config <tmpfile>`. Accounts: dev password in `docs/runbooks/05-auth.md`; claimant `self@fugue.test`, officer `staff@fugue.test`, manager `manager@fugue.test`, admin `admin@fugue.test`.

## Screens (decision K)
Navigation: a new "Claims" nav group, items shown by permission (codes from the generated contract / plan decision G):
1. **Claimant** (`CLAIMS_SUBMIT` / `CLAIMS_READ` without `CLAIMS_ALL`):
   - **My claims**: list (number, line, market, status pill, submitted, last update), "Report an incident" button, empty state that explains what to do.
   - **Report an incident**: form (market -> currency shown read-only, line Motor/Property, policy number, incident date (not future), what happened, where); server validation errors on fields; Idempotency-Key per submission; on success go to the claim.
   - **Claim detail (claimant view)**: status + a plain-language "what happens next" line per status, the timeline (claimant-safe notes), and when InfoRequested: the open question with an answer box (If-Match handled by the interceptor; 412 -> reload banner like customers).
2. **Staff** (`CLAIMS_ALL` + `CLAIMS_HANDLE`):
   - **Claims queue**: KPI strip at the top (counts by status from the list API or workload endpoint: Submitted/unassigned, Under review, Info requested, Approved awaiting settlement), status tabs WITH counts, filter "Assigned to me / Unassigned / Everyone", market and line filters, rich table (number + policy line, claimant, market, line, status pill, reserve in its currency, age in days, assigned officer), keyset "Load more".
   - **Claim detail (staff view)**: header with status + actions available for THIS status and role only: Pick up (Submitted), Set reserve, Request info (question dialog), Approve (amount <= reserve) / Reject (reason) dialog, Settle (manager/CLAIMS_SETTLE, Approved). Errors: 409 shows the guard message, 412 the "changed since you opened it" banner with reload, 428 never happens (interceptor). Timeline + info requests + audit link (`/audit?entityId=<claim id>` if AUDIT_READ).
   - **Workload** (`CLAIMS_WORKLOAD`): per-officer table (open by status, oldest open age, decided last 7/30 days), unassigned count, ageing buckets as a small bar row.
3. **Manager** (`CLAIMS_EXPOSURE`): **Exposure**: table by market, line, currency (open count, outstanding reserve, approved not settled) and totals PER CURRENCY (never a grand total across currencies; say so in the page summary line).
Money: format minor units with the currency's minor digits via `Intl.NumberFormat` (IDR has 0 decimals in practice? check `Intl` defaults; be consistent and document it). Dates local.

## Acceptance
1. Claimant flow works end to end in the browser against the live API: report -> appears in My claims -> (staff picks up, requests info) -> claimant answers -> sees decision.
2. Staff flow: queue tabs/counts correct, pick up, request info, reserve, approve/reject, settle (manager), workload and exposure render real numbers.
3. A claimant cannot see staff nav or reach staff routes (guards); a staff user without CLAIMS_EXPOSURE cannot reach Exposure.
4. `cd web && npm test` green with new unit tests (permission-driven nav, money formatting per currency, the status -> available-actions map, the claimant "what happens next" map), `npm run lint` clean on touched files, `npm run build` green.
5. One Playwright smoke spec `e2e/claims.spec.ts` for the claimant report + staff pickup path (run it against your live API if you can; if rate limits bite, say so).
6. Screenshots of the four main screens into `web/docs/screenshots/claims-*.png`.

## Non-goals
Backend changes (report contract gaps to the lead instead of patching), attachments, notifications UI, charts libraries (CSS bars only), dark mode, the product tour.

## Commits (pathspec `web/...` only; body per AGENTS.md; trailers `Refs: W-020, D-053` + `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`)
1. `feat(web): claims client and claimant screens`
2. `feat(web): claims queue and staff claim detail`
3. `feat(web): workload and exposure`
4. `test(web): claims smoke and screenshots`

## Time
Window hard stop 21:56 +08. Cut order if late: screenshots, e2e smoke, workload ageing bars, market/line filters. Never cut: claimant report/track/answer, staff pickup + decide, exposure per currency.

## Verify the premises
Verify every claim here against the code and the contract first; report false ones.

## Report
Commits, counts, deviations, contract gaps found, what you could not verify. Short.

## Recite
Claims UI in one Angular app: claimant (My claims, Report, detail + answer), staff (queue with KPI + tabs, detail with role/status actions, workload), manager (exposure per currency); permission-driven nav and guards; generated client; tested; web-only commits in the shared worktree.
