# Lane nth-searchui status

## Commits
- `feat(web): obvious header search with a first-use Meilisearch explainer` (single slice; web only, one component)

## Gates (copied from output)
- `npm run lint`: All files pass linting.
- `npm test`: 58 files, 371 tests passed (3 new in `global-search.spec.ts`, 1 placeholder assertion updated)
- `npm run build`: Initial total 513.22 kB raw / 132.01 kB gzip

## Shared files changed
None. Only `web/src/app/features/shell/global-search.{ts,css,spec.ts}`.

## Behaviour
- Bar: search icon, stronger border/background, `/` hint, `?` button, Meilisearch badge, placeholder "Try: SG motor, CLM-SG-…".
- `/` (not while typing elsewhere) and Ctrl/Cmd+K focus the box.
- First focus per browser (`localStorage` `fugue.search.intro`, try/catch) opens a popover: keyword search with typo tolerance,
  synonyms, exact numbers, highlights, facets, role-scoped; "not AI or semantic"; links to meilisearch.com and the .NET SDK docs.
  "Got it" dismisses, typing closes it, `?` reopens it. It is marked seen when first shown.

## Deviations
- No row for this lane exists in `docs/branch-ledger.md`, so no ledger edit (the contract allows only my own row's Status cell).

## Not verified
- Mobile (<700px): the search is hidden by existing CSS, unchanged.
