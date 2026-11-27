# Lane nth-forms status (Refs D-064, overnight item 5)

Shared accessible select and date controls, every form swapped, an example filler per form, behaviour unchanged.

## Commits
- `21276e0` feat(web): fg-select, an accessible select-only combobox on the CDK overlay
- `9d9ea50` feat(web): fg-date, a typed date field with a keyboard calendar popover
- `b6e2263` feat(web): console select and date controls in the report, overrides and audit forms
- slice 4: Fill with an example on every form (`web/src/app/ui/examples.ts`, `examples-panel.ts`) + specs

## Gates (copied from output)
- slice 1: `ng test --include src/app/ui/select.spec.ts`: 1 file, 7 tests passed; eslint + prettier clean on both files
- slice 2: `ng test --include` both ui specs: 2 files, 15 tests passed; eslint + prettier clean
- slice 3: `npm run lint`: All files pass linting. `npm test`: 46 files, 281 tests passed. `npm run build`: Initial
  total 460.61 kB / 121.03 kB gzip. Headless smoke (keyboard only) on the report form against :8080: 0 console errors.
- slice 4: `npm run lint`: All files pass linting. `npm test`: 48 files, 290 tests passed. `npm run build`: Initial
  total 460.61 kB / 121.08 kB gzip. Headless proof against :8080: 15 PASS lines, 0 console errors (details in the
  lane report).

## What changed in shared files (exactly)
- `web/e2e/claims.spec.ts`, `web/e2e/06-claims-flow.spec.ts`: `selectOption` on Market / Line of cover becomes
  click the combobox, click the option (the control is no longer a native select).
- `docs/branch-ledger.md`: the nth-forms row (as on main) with its Status cell updated.

## Deviations and gaps
- Examples: the customer drawer shows them for a NEW customer only (filling an existing customer's record with a
  made-up person is not an edit anyone wants). The overrides form and the audit filter get no examples (not in the
  lane's list; the audit filter is a search, not a form to fill).
- The overrides permission list has no CLAIMS_* or USERS_* codes (`web/src/app/api/access.api.ts:12-27`); not changed.
- fg-date also fixed in slice 3: moving focus into the calendar no longer marks the field touched (a required
  error flashed behind the popover); a spec asserts it and fails without the fix (checked).
- Left native on purpose: `datetime-local` in the overrides form and the audit filter (fg-date has no time part),
  the claims queue filter selects and the SLA calendar day jump (page filters, not forms).
- Premise check: native controls exist only in the report form (2 selects, 1 date), the access overrides form
  (2 selects, 1 datetime-local), the audit filter (2 selects), the claims queue filters (2 selects) and the calendar
  day jump (1 date). Request info, reserve, decide/reject, claimant answer, webhooks, customers and add user use none.
- The app has six markets (SG, MY, TH, HK, ID, PH: `web/src/app/features/claims/claims-logic.ts:5-12`), not five.

## Next slice
- none: report to the integrator
