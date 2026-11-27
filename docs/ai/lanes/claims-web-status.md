# Lane claims-web: status

Builder: Sonnet. Worktree `.worktrees/claims`, branch `claims-core`. Finished 2026-11-27 ~17:55 +08 (commits 1-4 in; hard stop was 20:15). Nothing pushed.

## Commits (web paths only, by pathspec)

| Sha | Subject |
|---|---|
| 885f089 | feat(web): claims client and claimant screens |
| 6bcd7ab | feat(web): claims queue and staff claim detail |
| fab8a1c | feat(web): workload and exposure |
| abf1558 | test(web): claims smoke and screenshots |

Contract used: `docs/api/openapi.v1.json` last changed at 7bc4280 (Idempotency-Key now required on submit). `api:gen` was re-run after it: no diff in the generated client.

## Validation (read from output, not exit codes)

- `npm test`: Test Files 11 passed (11), Tests 62 passed (62). New specs: claims logic (money per currency incl. IDR, parseMoney, status x role action map, the claimant "what happens next" map), the ETag interceptor for every claim command, the claims API (filter params, per-status counts), permission-driven Claims nav.
- `npm run lint`: "All files pass linting."
- `npm run build`: "Application bundle generation complete."
- Playwright against the live API (throwaway DB `fugue-claims-pg` :5441, API :5091): `claims.spec.ts` (smoke: 2 tests), `05-claims-screenshots.spec.ts` (3), `06-claims-flow.spec.ts` (1): all passed, in two runs (5 passed, then 1 passed). The flow spec walks report -> pick up -> reserve -> ask -> answer -> over-reserve approve refused -> approve -> settle, and the 412 banner.
- Screenshots: `web/docs/screenshots/claims-{my-claims,claimant-detail,queue,staff-detail,workload,exposure}.png`.

## False premises in the brief

1. "Claim commands are handled by the existing interceptors": false. The ETag interceptor only sent `If-Match` on PUT/PATCH/DELETE and the POSTs `suspend|reactivate|rotate-secret`, and `resourceKey` only stripped those suffixes. `pickup`, `decision`, `settle`, `info-requests[/id/response]` (POST) and `reserve` (PUT, tag keyed under `/reserve`) would all have gone out without `If-Match` (428), and the new ETag from each 200 would have been stored under the action URL. Fixed in `core/http/interceptors.ts` and `etag.store.ts`, pinned in `interceptors.spec.ts`.
2. "KPI counts from the list API or workload endpoint": the workload DTO has only `underReview`/`infoRequested` per officer and `unassignedCount`; there is no per-status or "approved awaiting settlement" count for a non-manager (exposure needs `CLAIMS_EXPOSURE`). Counts are read from the list endpoint, one page of 100 per status, shown as "100+" when a cursor remains.
3. The runbook says the decision body is `approve|reject`; the contract enum is `Approve|Reject`. The client sends the contract's.

Premises that held: permission codes, endpoint shapes, `SubmissionKey` reuse, `X-Next-Cursor` paging, `openInfoRequest` on the claim, claimant never receives the reserve (asserted in the flow spec).

## Deviations from the plan

- Money: all six currencies are formatted with two minor digits and the currency CODE ("IDR 2,500.00"), not `Intl`'s own digits (Intl gives IDR 0, which would drop the decimals the API stores) and not "$" (SGD and HKD would read the same). Documented in `claims-logic.ts` and the web README. A no-break space sits between code and number (Intl default); tests compare with a plain space.
- Routes are `/claims/mine`, `/claims/new`, `/claims/queue`, `/claims/workload`, `/claims/exposure`, `/claims/:id` (one detail page for both audiences, decided by `CLAIMS_ALL`). The Claims nav group is first, so a claimant or officer lands on their own screen.
- Extracted `visibleGroups` in `features/shell/nav.ts` (the shell now uses it) so the nav rule is unit-testable.
- A defect found by the smoke and fixed in commit 4: the claim timeline was cached, so a claimant saw a new status over an old timeline; it now re-reads on open.
- Added `06-claims-flow.spec.ts` beyond the one smoke the brief asked for.
- Not cut: nothing from the cut list was cut.

## Contract gaps (not patched)

- No count endpoint: the queue's KPI strip and tab counts cost six list calls and cap at "100+".
- Workload has no Submitted/Approved columns and no per-officer "open" total; the console sums under review + info requested.
- The detail has only the OPEN info request; past questions and answers are visible only as timeline notes (the timeline note carries the question text; I did not check how the answer is recorded there).
- List filter `assigned=me` and the claim `assignedOfficerName` are display names only on the staff DTO; the claimant DTO omits them (as designed).

## What I could NOT verify, and what would settle it

- A 409 guard toast in the browser (a non-assigned officer acting on someone else's claim, an invalid transition): the UI hides those actions, so reaching it needs a stale page; the generic problem toast path is the shared one. Settle by two officers racing a pickup in two tabs.
- Dark mode, small-screen layout beyond the CSS breakpoints, and screen-reader behaviour of the new dialogs and tabs (they reuse CDK dialog and the existing tab styles; not audited).
- Behaviour against a database with thousands of claims (the counts approach is the weak point).
- The rate limit: the sign-in limiter is 20/min/IP; repeated e2e runs inside a minute fail at sign-in (seen once). The API I started used `RateLimiting__Auth__PerEmailPermits=60`, which does not lift the per-IP limit; wait a minute between runs.

## Process notes

- A `git commit --amend --no-edit` run by mistake after a failed commit (commitlint line length) folded my staged files into 506fc47 (the backend status commit). I undid it at once with `git reset --soft 506fc47` (verified: HEAD back at 506fc47, working tree untouched, my files re-staged) and committed again. No other commit was touched.
- `pkill -f Fugue.Api` once, to restart my API: it matches any host `Fugue.Api` process, so another lane's host API could have been stopped. Later stops used the listening port.
- Left running: `fugue-claims-pg` (:5441). Stopped: my API (:5091) and `ng serve` (:4200). Nothing of mine touches the compose stack or `tpms-local-pg`.
