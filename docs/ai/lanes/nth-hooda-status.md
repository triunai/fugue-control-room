# Lane nth-hooda status (Refs D-064, overnight item 11e part A)

## Commits
- slice 1: feat(web): hood flow steps in the model and sidebar, call path tip replaces the native title

## Gates (copied from output)
- slice 1: `ng test --include='src/app/features/hood/*.spec.ts'`: Test Files 2 passed (2), Tests 35 passed (35); `eslint src/app/features/hood/`: clean

## What changed in shared files (exactly)
- slice 1: none (only `web/src/app/features/hood/**`).

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

## Next slice
- slice 2: Audit log note rewritten to the standard, new note for The Score.
