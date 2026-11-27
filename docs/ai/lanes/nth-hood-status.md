# Lane nth-hood status (Refs W-022, backlog #008)

## Commits
- `bac8503` feat(web): live API call log for the under-the-hood panel
- `2899045` feat(web): under-the-hood sidebar, minimisable
- `89e15b3` feat(web): under-the-hood notes for the demo pages
- test(web): hood screenshots (`web/docs/screenshots/hood-open.png`, `hood-minimised.png`: claim record as `staff@fugue.test` after a pick-up)
- this status file

## Gates (copied from output)
- `npm test`: 31 files, 180 tests passed (was 29 files, 159 tests at ac68687, per the brief)
- `npm run lint`: All files pass linting
- `npm run build`: Initial total 426.48 kB raw / 112.15 kB gzip at ac68687 -> 433.74 kB / 114.38 kB (+7.26 kB / +2.23 kB).
  Measured on a clean checkout of ac68687 and on this branch. The sidebar sits in the lazy shell chunk; the delta in the
  initial bundle is the call-log interceptor plus the eight note objects imported by `app.routes.ts` (route `data` is synchronous).

## What changed in shared files (exactly)
- `web/src/app/features/shell/shell.html`: one line, `<fg-hood />` as the last child of `.shell`.
- `web/src/app/features/shell/shell.ts`: import and `imports: [..., HoodSidebar]`.
- `web/src/app/features/shell/shell.css`: the two grid-template-columns gain a third `auto` column (hood width: 21rem open, 14px minimised; `display: none` under 860px).
- `web/src/app/app.config.ts`: `provideAppInitializer(hoodOffFromUrl)` (same pattern as `suppressTourFromUrl`).
- `web/src/app/app.routes.ts`: `data.hood` on eight routes (one commit with the notes).
- `web/src/app/core/http/interceptors.ts`: `callLogInterceptor` added last in the chain, below the problem interceptor
  (so it sees the raw error body for the `code` and the If-Match the ETag interceptor attached).

## Behaviour and design choices
- Call log records only: method, path with ids as `{id}`, query parameter NAMES, status, ms, three yes/no markers, and the problem code on 409/412/428. Tested not to contain query values, bodies, tokens or header values.
- Log clears when the URL PATH changes (a filter change in the query does not). Background calls such as `/diagnostics/ping` appear because they are real API calls.
- Open by default; choice remembered in `localStorage` (`fugue.hood.open`); `?hood=off` starts minimised for the browser session via `sessionStorage` and is not remembered. `.` toggles unless typing or a modifier is held.
- Notes sit next to pages as `*.hood.ts`: claim record, queue, team and exposure, landing (one note for all personas), report, my claims, audit, access. Customers, modules and webhooks show only live calls.
- Doc links go to `https://github.com/triunai/fugue-control-room/blob/main/...`; decision links point at `docs/decisions.md` (the file has no per-row anchors).

## Deviations and gaps
- The "Customers" page was not in the brief's list, so it has no note.
- Note claims were checked against `docs/CLAIMS.md`, `docs/decisions.md` and the page code, not by an automated test; the spec only checks shape and that decision paths look like `docs/...`.
- The Playwright tool could only write screenshots under the main repo root, so they were moved into this worktree.

## Not verified
- Mobile (<860px) hides the sidebar; not eyeballed. A narrow-desktop check at 900-1100px was not done either.
- Keyboard `.` was exercised in unit tests and in the browser (toggled closed and open), not under a screen reader.
