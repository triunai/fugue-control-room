# Lane nth-calfix status (14:00 gate: /claims/calendar console error, Refs D-064)

## Commits
- fix(web): no ResizeObserver console error on the SLA calendar (sha in the lane report; one commit, one slice)

## Gates (copied from output)
- `npm run lint`: All files pass linting.
- `npm test`: Test Files 56 passed (56), Tests 354 passed (354). New spec fails (1 failed | 353 passed) with the filter removed.
- `npm run build`: Application bundle generation complete.
- Headless (dev server :4414 proxied to the demo API, hood open, 2 sidebar toggles, one window resize, day click):
  before: staff 1280x800 = 3 errors, 1500x950 = 1; after: staff and self@fugue.test at both sizes = 0 console errors, 0 pageerrors.

## Shared files changed (exactly)
- `web/src/app/app.config.ts`: one import and one provider line (`provideFugueErrorHandler()`).

## Deviations
- Source fix removes the load-time error only; the window-resize error comes from inside FullCalendar 7's own ResizeObserver
  (no `handleWindowResize`/`updateSize` options exist in v7), so that one is ignored by exact message in the ErrorHandler.

## Next
- None. Lane complete.
