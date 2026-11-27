# Lane nth-formsfix status (Refs D-064, item 5 follow-up)

## Commits
- f3efa50 test(web): make the next-step card locators exact
- 6db3049 test(web): claims e2e uses the next-step test id and waits for the search index
- slice 2: feat(web): queue Market/Line filters on fg-select, calendar day jump on fg-date (this commit)

## Gates
- lint: All files pass linting; npm test: 54 files, 335 tests passed; build: complete
- `E2E_BASE_URL=http://localhost:4412 npx playwright test e2e/claims.spec.ts`: 2 passed (proxied to demo API)
- headless as staff: 0 native select/date in the queue and calendar; keyboard market then line filter changes rows and URL; reset restores; day jump opens the day panel; 1 console error (FullCalendar ResizeObserver, fires on calendar load before any day jump)

## Shared files changed
- `web/src/app/features/claims/claims-queue-page.ts`, `claims-calendar-page.ts`: ngModel bindings on fg-select/fg-date; no change to ui/ controls.
- `web/e2e/claims.spec.ts`, `web/e2e/06-claims-flow.spec.ts`: two locators made exact (`getByText('...', { exact: true })`).

## Deviations
- `docs/branch-ledger.md` had no row for this lane; one was added.

## Next
- none; report written.
