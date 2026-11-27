# Lane nth-claimui status

Base afc0dae, branch `nth-claimui`, 9 commits before this file (web only; no backend change).

## Commits
- f0b5409 `feat(web): claim record banner and next step card` (DEVIATION: the brief's commits 1 and 2 share `claims-logic.ts`, `claims.css` and the detail page, so they are one commit)
- 3391e13 `feat(web): team and exposure page`
- 2ff5859 `refactor(web): split record and team styles out of claims.css` (claims.css went over the 6 kB component style budget; build warned)
- f856000 `feat(web): a landing page per persona`
- 8fedec9 `feat(web): route the landing and team pages, retire workload and exposure` (the one `app.routes.ts` commit; also deletes the old two pages and their two screenshots, and edits `e2e/support.ts` and `e2e/05-claims-screenshots.spec.ts`)
- d843a4f `feat(web): one Team and exposure nav item` (the one `nav.ts` commit; also `shell/nav.spec.ts`, two assertions renamed)
- 122beb0 `feat(web): end the money track after a rejection and mark the worksheet slot`
- 2193122 `test(web): screenshots of the record, team page and landings`

## Gates (this worktree, copied from output)
- `npm test`: Test Files 14 passed, Tests 92 passed (main was 62; +30: record 17, team logic 8, home logic 5)
- `npm run lint`: All files pass linting.
- `npm run build`: Initial total 398.23 kB, complete, no warnings.
- Live: API :5105 seeded (Postgres :5455), `ng serve` :4205 with a proxy config kept outside the repo. Screenshots taken per persona.

## What was built
- Record banner (`claim-record-banner.ts`): Claim and Money tracks, n of N, checked / bold current / "Next"; InfoRequested is a dot on Under review; Rejected ends both tracks (Claim at an error-tone step, Money with no Next). Claimant money track is Assessing, Approved amount, Settled (no reserve). Pure functions `claimTrack`, `moneyTrack` in `claims-logic.ts`.
- Next step card (`claim-next-step.ts`, pure `nextStep(claim, actor)`): chip Ready / Waiting / Waiting on claimant / Blocked / Done, instruction, why, checklist, one primary action wired to the existing dialogs (claimant "Answer now" focuses the answer box). Existing action buttons stay below it. Old timeline is now "Activity", newest first. `<!-- assessment worksheet slot -->` is in `claim-detail-page.ts` after the card.
- Help button calls `startTour('claim-detail')` in `tour-hook.ts`: it looks up `globalThis.fugueTour.start(key)` at call time, a no-op until nth-shell's service registers there (or the lead swaps the body for a real import).
- `/claims/team` (`team-page.ts`, `team-logic.ts`): KPIs (unassigned, open past SLA, oldest open with link, decided this week), per-officer load bars with each officer's oldest claim linked, clickable ageing bands, exposure matrix market x Motor/Property with bars scaled within one currency, per-currency totals, no grand total. `CLAIMS_WORKLOAD` shows the team part, `CLAIMS_EXPOSURE` the money part. Old routes redirect.
- Landing at `/` (`features/home/`): `landingKind` from permission codes (MODULES_MANAGE admin, WORKLOAD+EXPOSURE manager, HANDLE officer, SUBMIT without ALL claimant, else the old first-nav-item redirect).

## Deviations and decisions
- `SLA_DECISION_DAYS = 14` lives in `claims-logic.ts` for "open past SLA"; nth-grid defines its own SLA constants: the lead should make one the source.
- `e2e/support.ts` `signIn` now goes to each persona's old working screen after sign-in (since `/` is a landing now); not in the lane's allowed list, but the existing e2e would otherwise break. Not run here.
- `shell/nav.spec.ts` (nth-shell's area) edited by two assertions because the nav entry changed; expect a trivial conflict.
- Ageing band click opens the queue unfiltered (queue has no age filter); KPI "unassigned" opens it filtered to Unassigned.

## Contract gaps
- The claims list omits `openInfoRequest` (null for an InfoRequested row), so the claimant landing hero fetches that one claim's detail to show the question.
- Workload per officer gives `oldestOpenAgeDays` but not which claim; the page derives it from the list (first 100 per open status): beyond 100 open claims per status the oldest could be missed. A `oldestOpenClaimId` on the workload DTO, and a queue filter by officer, would settle it.
- No `assigned=<officerId>` filter, so the officer row cannot link to "the queue filtered to that officer"; it links to the claim.

## Not verified
- Playwright e2e specs were not run (they target the compose stack); `signIn` change unproven there. Run `cd web && npm run e2e` against a stack.
- Tour hook is untested against nth-shell's real service.
- Keyboard and screen reader pass on the steppers was not done (current step has `aria-current="step"`, checklist marks have hidden text).
- Cut order not needed: landing for admin and ageing drill-down shipped (drill-down is queue-level only).
