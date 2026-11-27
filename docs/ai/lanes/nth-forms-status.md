# Lane nth-forms status (Refs D-064, overnight item 5)

Shared accessible select and date controls, every form swapped, an example filler per form, behaviour unchanged.

## Commits
- `21276e0` feat(web): fg-select, an accessible select-only combobox on the CDK overlay
- `9d9ea50` feat(web): fg-date, a typed date field with a keyboard calendar popover
- slice 3: native selects and the date input swapped in the report form, the access overrides form and the audit filter

## Gates (copied from output)
- slice 1: `ng test --include src/app/ui/select.spec.ts`: 1 file, 7 tests passed; eslint + prettier clean on both files
- slice 2: `ng test --include` both ui specs: 2 files, 15 tests passed; eslint + prettier clean
- slice 3: `npm run lint`: All files pass linting. `npm test`: 46 files, 281 tests passed. `npm run build`: Initial
  total 460.61 kB / 121.03 kB gzip. Headless smoke (keyboard only) on the report form against :8080: 0 console errors.

## What changed in shared files (exactly)
- `web/e2e/claims.spec.ts`, `web/e2e/06-claims-flow.spec.ts`: `selectOption` on Market / Line of cover becomes
  click the combobox, click the option (the control is no longer a native select).
- `docs/branch-ledger.md`: the nth-forms row (as on main) with its Status cell updated.

## Deviations and gaps
- fg-date also fixed in slice 3: moving focus into the calendar no longer marks the field touched (a required
  error flashed behind the popover); a spec asserts it and fails without the fix (checked).
- Left native on purpose: `datetime-local` in the overrides form and the audit filter (fg-date has no time part),
  the claims queue filter selects and the SLA calendar day jump (page filters, not forms).
- Premise check: native controls exist only in the report form (2 selects, 1 date), the access overrides form
  (2 selects, 1 datetime-local), the audit filter (2 selects), the claims queue filters (2 selects) and the calendar
  day jump (1 date). Request info, reserve, decide/reject, claimant answer, webhooks, customers and add user use none.
- The app has six markets (SG, MY, TH, HK, ID, PH: `web/src/app/features/claims/claims-logic.ts:5-12`), not five.

## Next slice
- slice 4: Fill with an example on every form
