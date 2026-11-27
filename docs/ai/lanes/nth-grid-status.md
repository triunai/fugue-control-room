# Lane nth-grid status (Univer reserve sheet, FullCalendar SLA calendar)

Refs: W-022, D-059. Branch `nth-grid`, base b875f49.

## Commits
- 960238c feat(web): reserve review sheet
- 0824bf8 feat(web): claims SLA calendar
- f6cac4b feat(web): nav entries and routes for the reserve sheet and the SLA calendar
- a1feeac docs(web): nice-to-haves and their bundle cost
- 4be6dc0 docs(web): final bundle numbers from the last build
- (this file) docs(docs): nth-grid lane status

## Gates (copied from output)
- `cd web && npm test`: 14 files, 85 tests passed (base: 62).
- `npm run lint`: All files pass linting.
- `npm run build`: complete. main 372.58 kB raw / 100.92 kB est. transfer; initial total 406.99 kB / 107.43 kB.
  Base: main 363.02 / 97.94, initial total 397.43 / 104.46.
- e2e (`web/e2e/07-grid.spec.ts` 3 passed and `08-assessment.spec.ts` 1 passed, live API :5101, dev server :4201) (sheet: invalid and stale-row results,
  then a saved row; calendar: events render, click opens the claim). Screenshots `web/docs/screenshots/claims-sheet.png`,
  `claims-calendar.png`.

## Deviations
1. The initial bundle DID grow: +9.56 kB raw (+2.98 kB est. transfer). Neither library's code is in it; shared rxjs
   modules and tslib helpers Univer imports are hoisted into `main` by the bundler. Not avoidable without serving Univer
   outside the Angular build (UMD from assets). Documented with the numbers in the web README, "Nice to haves".
2. FullCalendar is used through its framework-free `Calendar` class (`fullcalendar` 7.1.5), not `@fullcalendar/angular`:
   the wrapper added about 10 kB more to `main`. daygrid and timegrid come from `fullcalendar/daygrid`, `fullcalendar/timegrid`
   (FullCalendar 7 merged the plugin packages into `fullcalendar`; the brief's separate daygrid/timegrid packages are v6).
   `temporal-polyfill` is a peer dependency of v7.
3. Library stylesheets (Univer 211 kB, FullCalendar 24 kB) are vendor assets (`angular.json` assets, `vendor/`), linked on
   first use by `lazy-css.ts`, because the `anyComponentStyle` budget (10 kB error) rejects them as component styles and
   `styles` entries would be initial.
4. `nav.spec.ts` (a shell file) was edited in the nav commit: the claimant list now includes the SLA calendar, and the staff
   case asserts the sheet. Nothing else under `features/shell/` was touched.
5. Brief said "Univer `@univerjs/presets`"; the preset also needs `@univerjs/preset-sheets-core` (both pinned 1.6.2, plus
   `react`/`react-dom` pulled in by npm as peers).
6. The sheet shows Submitted, UnderReview, InfoRequested and Approved claims (open = not Rejected or Settled); the list takes
   one status at a time, so it reads each status up to 5 pages of 100 (500 per status).
7. Settlement (Approved) has an SLA of 7 days after `decidedAt` in addition to the three the brief named; one constant, one line.

## Addendum (owner, via lead)
- 3e35ef1 feat(web): open the reserve sheet in Excel (ExcelJS 4.4.0, lazy chunk `exceljs-min` 944.59 kB / 218.34 kB; downloaded
  file unzipped and checked: a 16x8 worksheet). data-tour attributes on sheet and calendar controls.
- 71dbac4 feat(web): claim assessment worksheet: `features/claims/claim-assessment-sheet.ts` (standalone, input = claim, output
  `reserved`), formulas for Gross and Net, Use as reserve (If-Match, only when allowed and net > 0), Open in Excel, ephemeral
  caption, data-tour hooks. Host route `claims/:id/assessment` added to `app.routes.ts` (a second touch of that file beyond the
  nav commit; the lead can drop it when nth-claimui embeds the component). Screenshot `claim-assessment.png`.
- Not done: the calendar month-only cut was not needed (month and week both ship).

## Contract gaps
- No count or "SLA" field on the API, and the list takes a single `status`: the sheet and calendar make four list calls per load.
  nth-api's `/claims/counts` does not help here; a multi-status filter would.
- The reserve command has no bulk form: rows save one after another (D-059 consequence).

## Not verified, and what would settle it
- A 409 on a real row in the browser: the local rule (not editable) catches the common case first; a reassign between load
  and save would produce it. Covered by the `errorResult` unit test only. Settle: reassign a claim via API between load and save.
- Sheet behaviour on touch devices and phone widths; the calendar in a non-local timezone.
- More than 500 claims in one status (list is capped).
- The 60-minute Univer timebox was not hit: Univer mounted inside Angular on the first attempt (about 25 minutes of the lane).

## Cleanup
Throwaway Postgres `nth-grid-pg` removed; API :5101 and `ng serve` :4201 stopped by port. The temporary proxy config lived
outside the repo and was not committed.
