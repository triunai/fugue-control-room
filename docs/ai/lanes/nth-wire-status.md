# Lane nth-wire: status

Branch `nth-wire`, base 59e7d77. Refs W-019, W-021, W-022.

## Commits (web-only pathspecs, no amend, no push)
1. `886384c` feat(web): users list on the access screen
2. `3adfa43` feat(web): roles matrix reads the API
3. `8a87a5f` feat(web): audit search box
4. `5a2eff0` fix(web): manager tour points at the team page
5. screenshots commit (`access-users.png`, `audit-search.png`), then this status file

## Validation (literal output)
- `cd web && npm test`: 20 files, 91 passed (base 83).
- `npm run lint`: All files pass linting.
- `npm run build`: complete, no errors.
- Live check against a throwaway Postgres (:5458), API (:5108, seeded) and Meilisearch v1.12.8 (:7708): a temporary
  Playwright script (deleted) listed 6 users, searched "admin" (1 row), opened overrides on row click, saw the
  roles matrix from `GET /roles`, and got ranked hits for the typo "custmer". All containers and processes removed.

## Deviations
- The "seeded API on :5108" was not running; I started my own.
- Matrix drops roles with `isActive: false` (the API returns the flag).
- The audit list API has no `outcome`/`q`; they are search-only filters (outcome select is labelled "(search)").
  Entity id, actor and correlation filters are ignored in search mode.
- Search results have no detail beyond the hit fields (no diff/trace); the panel offers "History of this entity".
- The `recentActors` chips (audit-derived) were removed from the access page; the users list replaces them.
- Tour: both manager steps now use `/claims/team`; their selectors (`exposure-totals`, `workload-kpis`) are
  unchanged and must exist on the merged page.

## Could not verify
- The 503 fallback against a real engine-off API (covered by a unit test with a 503 ApiProblem); settle by
  running the API without `Search__Meilisearch__Url`.
- The manager tour against the merged `/claims/team` page (nth-claimui not merged here).
- Sidebar still lists Workload/Exposure (nav.ts is claimui's).
