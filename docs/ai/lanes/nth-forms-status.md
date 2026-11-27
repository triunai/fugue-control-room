# Lane nth-forms status (Refs D-064, overnight item 5)

Shared accessible select and date controls, every form swapped, an example filler per form, behaviour unchanged.

## Commits
- `21276e0` feat(web): fg-select, an accessible select-only combobox on the CDK overlay
- slice 2: `fg-date` (`web/src/app/ui/date-picker.ts`) + spec

## Gates (copied from output)
- slice 1: `ng test --include src/app/ui/select.spec.ts`: 1 file, 7 tests passed; eslint + prettier clean on both files
- slice 2: `ng test --include` both ui specs: 2 files, 15 tests passed; eslint + prettier clean

## What changed in shared files (exactly)
- `docs/branch-ledger.md`: the nth-forms row (as on main) with its Status cell updated.

## Deviations and gaps
- Premise check: native controls exist only in the report form (2 selects, 1 date), the access overrides form
  (2 selects, 1 datetime-local), the audit filter (2 selects), the claims queue filters (2 selects) and the calendar
  day jump (1 date). Request info, reserve, decide/reject, claimant answer, webhooks, customers and add user use none.
- The app has six markets (SG, MY, TH, HK, ID, PH: `web/src/app/features/claims/claims-logic.ts:5-12`), not five.

## Next slice
- slice 3: swap the native controls in the forms
