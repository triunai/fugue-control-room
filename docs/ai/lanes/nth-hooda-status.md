# Lane nth-hooda status (Refs D-064, overnight item 11e part A)

## Commits
- `7b6b3ca` feat(web): hood flow steps and a call-path tip attached to its row
- `1df1cb0` feat(web): audit log hood note to the flow standard, and The Score's note
- slice 3: feat(web): hood notes for modules, webhooks, customers and my profile; access gains a flow

## Gates (copied from output)
- slice 1: `ng test --include='src/app/features/hood/*.spec.ts'`: Test Files 2 passed (2), Tests 35 passed (35); `eslint src/app/features/hood/`: clean
- slice 2: same include: Test Files 2 passed (2), Tests 39 passed (39); injecting `LIMIT 501` and a wrong flow path into the
  Score note made 2 tests fail (excerpt + flow file), then restored; eslint hood/audit/app.routes.ts clean
- slice 3: hood include: Tests 56 passed (56); `npm run lint`: All files pass linting.; `npm test`: Test Files 44 passed (44),
  Tests 290 passed (290); `npm run build`: complete, no warnings, Initial total 487.21 kB / 127.47 kB

## What changed in shared files (exactly)
- slice 1: none (only `web/src/app/features/hood/**`).
- slice 2: `web/src/app/app.routes.ts`: one import line (`SCORE_HOOD`) and `hood: SCORE_HOOD` on the `audit/trace/:correlationId` route data.
- slice 3: `web/src/app/app.routes.ts`: four import lines (CUSTOMERS, MODULES, PROFILE, WEBHOOKS) and `hood: X_HOOD,` added to
  the `data` of `customers`, `modules`, `webhooks`, `me`. No other line.

## The flow type (part B reads this after merge)
```ts
export type HoodLayer = 'web' | 'api' | 'handler' | 'db' | 'audit' | 'outbox' | 'webhook' | 'search' | 'job';
export interface HoodStep { readonly layer: HoodLayer; readonly text: string; readonly path?: string }
// HoodNote gains: readonly flow?: readonly HoodStep[];  (optional; rendered as "What happens" before "How the API does it")
```

## Deviations
- Branch ledger: this branch's base (dacfb06) has no `nth-hooda` row (it was added on main later), so there is no own
  Status cell to update here; the status lives in this file and the report. No ledger line is touched.
- The floating tooltip was the native `title` on the call row's path span (the only place a shortened path such as
  `/audit?limit&entityType&entityId` reaches the DOM). Native tooltips cannot be positioned or torn down by the page, so it
  is replaced by a DOM tip that is a child of the row.

- `hood-notes.spec.ts`: the test "pages without a note (customers, modules) show only the live calls" is replaced: the lane
  gives those pages notes, so it asserted the opposite of the new requirement. Replaced by a flow check (every step layer is
  valid, text <= 160 chars, every path exists in the repo).
- The Score has no D-row of its own: the code cites D-064, whose text does not mention The Score. The note links D-064
  labelled "the trace endpoint cites it"; a proposed D-row is in the report.

## Next slice
- headless proof (each page as admin, sidebar open, flow renders, tip attached and gone on leave), report.
