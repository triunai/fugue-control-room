# Lane nth-shell: status

Branch `nth-shell`, base b875f49. Refs W-022, D-060 (backlog #002, #003, #004).

## Commits (web-only pathspecs, no amend, no push)
1. `3ff8ba7` fix(web): the nav scrolls above the pinned account card
2. `c58eb38` feat(web): console shell from the reference design
3. `fa98e36` feat(web): reviewer tour
4. `714ebdd` feat(web): roles and permissions matrix
5. `3ac0a3e` feat(web): matrix step in the admin tour, shell e2e and screenshots

## Validation (literal output)
- `cd web && npm test`: 79 passed (main was 62).
- `npm run lint`: All files pass linting.
- `npm run build`: no budget warnings; initial main 360.26 kB raw (97.34 kB transfer), styles 42.18 kB
  (driver.js stylesheet added); driver.js is its own lazy chunk, 47.68 kB raw (14.17 kB transfer).
- `E2E_BASE_URL=http://localhost:4202 npx playwright test e2e/07-shell-screenshots.spec.ts`: 8 passed against
  a throwaway Postgres (:5452) and the API (:5102), seeded; both removed afterwards.
- Screenshots: `web/docs/screenshots/shell-{expanded,account-menu,rail,short-viewport}.png`,
  `tour-{claimant,officer,manager,admin}.png`, `access-matrix.png`.

## Deviations and findings
- Premise partly false: on main the nav already sat in a scroll container (`overflow-y: auto`). The real defect
  was a 218px footer (profile card plus three action rows) leaving about 346px of nav at a 640px viewport, with
  no scroll cue, so Platform was below an unmarked fold. Fixed by shrinking the footer to one account row with an
  upward menu (about 100px) and an edge-shadow scroll cue. Measured at 1280x640: last item ends where the footer
  starts; the e2e asserts it.
- Port is an adaptation, not a clone: kept the console's existing item style (accent bar) rather than the
  reference's tree line and sliding card. Done: animated collapsible remembered groups (group holding the open
  route opens itself), active indicator, rail with tooltips and the `[` key, navigation progress bar, route
  entrance (skipped for the first page and under reduced motion), account card and menu, header with the
  section as eyebrow and the page in the display face. Not done: theme switch (dark is a non-goal), search box
  and inbox bell from the reference header (no backing feature), click-anywhere-to-expand on the rail.
- Shell split into `sidebar`, `account-menu`, `nav-progress`, `shell` (+ `active-nav.ts`, `tour-*.ts`).
- Tour: `TourService` starts it on first sign-in per persona (localStorage `fugue.tour.seen.<persona>`) and from
  the account menu. Persona comes from permission codes: MODULES_VIEW admin, CLAIMS_EXPOSURE manager,
  CLAIMS_HANDLE officer, CLAIMS_SUBMIT claimant, else member (no auto tour). The "412 banner" cannot be shown on
  demand, so the officer step highlights the actions and explains the 412 case in its copy.
- e2e: `signIn` now marks the tour seen (otherwise the overlay would cover every other spec);
  `playwright.config.ts` reads `E2E_BASE_URL`.
- The shared Playwright MCP browser is used by several lanes (it jumped to another lane's port mid-run); I
  switched to `npx playwright test` for everything that mattered.

## For the lead
- Nav entries: unchanged model. A new entry is one more item object in a group's `items` in
  `web/src/app/features/shell/nav.ts` (`label`, `path`, `permission`, `icon`). Nothing else in the shell needs
  touching; the sidebar, active highlight, group auto-open and the landing redirect all read `NAV`.
- Roles matrix swap: `web/src/app/api/roles.api.ts`, `RolesApi.list()`. Replace its body with the GET
  `/api/v1/roles` call and map to `RoleDto[]` (`code`, `name`, `permissions: {code, module}[]`). The component is
  gated by `GROUPS_VIEW` in `access-page.html`; switch to `USERS_VIEW` once the web knows that code.
  The placeholder data was read from the seeded database and matches it.
- Tour step copy is in `tour-steps.ts` (pure, unit-tested); selectors use existing `data-testid`s.

## Could not verify
- The tour's "answer" step for a claimant needs a claim in Info requested; if none exists the step shows centred.
  Settled by seeding one (the dev seed has one, the e2e walked the claimant tour to "The timeline" only).
- Reduced-motion behaviour of the bar and entrance was reasoned from CSS and the matchMedia guard, not run.
- Mobile layout (under 860px) was kept as it was and not re-screenshotted.

## Addendum (owner follow-ups)
- Tour hook symbol: `TourService.start(key: string)` in `web/src/app/features/shell/tour.service.ts`
  (injectable, root). Also exposed as `window.startTour(key)` once the shell is up, for dynamic lookup.
  Keys: `landing`, `queue`, `claim-detail`, `team-exposure`; no key (or `'persona'`) runs the persona tour.
  Steps live in `PAGE_TOURS` in `tour-steps.ts`; targets are `[data-tour="queue-kpis"|"queue-tabs"|
  "claim-actions"|"claim-timeline"|"claim-answer"|"exposure-totals"|"exposure-note"|"team-workload"]` with
  today's test ids / classes as fallbacks. A missing target is skipped, never an error.
- Header "Help" button starts the tour for the URL on screen (`tourKeyFor`); `/claims/team` maps to
  `team-exposure` already.
- Persona tour still navigates to `/claims/workload` and `/claims/exposure` for the manager; when nth-claimui
  merges them into `/claims/team`, change those two `page.path` values in `tour-steps.ts`.
- Nav: Customers is now in the last group `Starter example`, folded by default (PrefsStore default). Renaming
  Workload and Exposure to one "Team and exposure" item is one edit in `nav.ts`.
- Auto-start: shown once per persona (the first start marks it seen, so one dismiss is forever);
  `?tour=off` on the entry URL suppresses it for the browser session.
- Not re-run: the e2e suite and screenshots after this addendum (API and Postgres were already torn down).
  Unit 83 passed, lint clean, build clean.
