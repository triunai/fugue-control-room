# Lane nth-calfix (14:00 gate) — Sonnet — worktree .worktrees/nth-calfix, branch nth-calfix, base db0eb20
Ports: web 4414. Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 6) first.

Objective: /claims/calendar logs ZERO console errors for every persona, so the 14:00 zero-console-errors smoke passes.

Current state (integrator reproduced twice on :8080; verify): with the "Under the hood" sidebar OPEN (the default on a first
visit), /claims/calendar logs "ERROR Error: ResizeObserver loop completed with undelivered notifications" through Angular's
ErrorHandler. With ?hood=off it does not. Cause is likely FullCalendar (v7) reacting to its container resizing when the shell's
third grid column (the hood sidebar) changes width. The calendar page is `web/src/app/features/claims/claims-calendar-page.ts`
(+ its css); the day jump there now uses fg-date (do not change that).

Desired state, in this order of preference:
1. Fix at the source: stop the resize loop — e.g. FullCalendar `handleWindowResize: false` plus an own ResizeObserver on the
   container that coalesces into one `requestAnimationFrame(() => calendar.updateSize())`; or a fixed/explicit `height` /
   `contentHeight` so FullCalendar does not re-measure in the same frame; or render the calendar after the layout settles
   (`afterNextRender`). Pick what works; explain why in the report.
2. Fallback ONLY if the source cannot be fixed: in the app's global ErrorHandler ignore exactly this message (exact string
   match), with a comment and a spec proving any other error (including other ResizeObserver texts) still reaches the handler.

Acceptance: headless on your dev server proxied to :8080, as staff AND self@fugue.test, at 1280x800 and 1500x950: load
/claims/calendar with the sidebar open, toggle the sidebar closed and open twice, resize the window once, click a day — 0
console errors and 0 pageerrors (collect both console 'error' and 'pageerror'). Prove the bug first on the unmodified code
(same script → ≥1 error), then 0 after. Web gates green. fix(web) commit includes a spec.
Non-goals: no visual changes to the calendar, no other pages, no changes to the hood sidebar.
Commits: 1 fix(web) + spec; status file + own ledger row.

Recite: reproduce with the sidebar open, fix at the source, 0 console errors and pageerrors for staff and claimant at two sizes.
