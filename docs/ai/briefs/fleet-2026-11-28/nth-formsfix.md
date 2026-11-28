# Lane nth-formsfix (item 5 follow-up) — Sonnet — worktree .worktrees/nth-formsfix, branch nth-formsfix, base 1e3d63d
Ports: web 4412. Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 4) first.

Objective: the console's visible page filters use the console controls too, and the e2e claims suite is green on main again.

Current state (verify): nth-forms (merged 1e3d63d) added `fg-select` (`web/src/app/ui/select.ts`) and `fg-date`
(`web/src/app/ui/date-picker.ts`), both ControlValueAccessors. Still native: the claims queue Market and Line filters
(`claims-queue-page.ts` ~lines 87 and 96, bound with `[value]`/`(change)` to filter signals) and the SLA calendar day jump
(`claims-calendar-page.ts` ~line 77, `type="date"`). The e2e spec `web/e2e/claims.spec.ts:30` fails in strict mode:
`getByText('We have your claim')` matches 2 elements (the next-step card's instruction and its "why" line, copy from item 7).

Desired state:
1. Queue Market/Line filters use fg-select and the calendar day jump uses fg-date, keeping today's behaviour exactly (URL/filter
   signal updates, "All" option, empty state). If the controls need a non-form binding, prefer `[ngModel]`/`(ngModelChange)`
   or a tiny input/output added to the control (allowed) — no behaviour change for existing form usages (their specs stay green).
2. Fix the e2e locator at claims.spec.ts:30 to target ONE element unambiguously (role/heading or a test id already present —
   do not change product copy). Check 06-claims-flow.spec.ts for the same pattern and fix it the same way.
3. Run `cd web && E2E_BASE_URL=http://localhost:4412 npx playwright test e2e/claims.spec.ts` against your dev server proxied
   to :8080 and paste the result (pace for the rate limiter). Do NOT run 06-claims-flow end to end (it settles a claim on the
   shared demo) unless the integrator asks; type-check it instead.

Acceptance: no native select/date left in the queue filters or calendar day jump (grep); headless as staff: filter by market and
line via keyboard, results change, URL/filters as before; calendar day jump by keyboard; 0 console errors; claims.spec.ts passes;
web gates green. The three `datetime-local` inputs stay native (known limit, recorded by the integrator).
Non-goals: no other pages, no copy changes, no new controls, no backend.
Commits: 1 e2e locator fix (test(web): ...), 2 queue filters + calendar day jump (feat(web): ...); status file + own ledger row.

Recite: queue filters and calendar day on console controls, behaviour unchanged, e2e claims green, no copy change.
