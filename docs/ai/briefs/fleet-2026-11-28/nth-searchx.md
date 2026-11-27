# Lane nth-searchx (item 10) — Opus — worktree .worktrees/nth-searchx, branch nth-searchx, base dacfb06

Objective: the header claims search answers the way an insurance officer expects — market names, lines, synonyms, exact
claim/policy numbers, and it shows why each hit matched.

Read: `docs/research/2026-11-28-meilisearch.md` (note: sections 2-5 may be truncated — fill from Meilisearch docs for the
server version actually in compose, verify the version), item 10 in `docs/plans/overnight-2026-11-28.md`, D-063 and D-058
in `docs/decisions.md` (read only), the existing claims indexer and search query code and their tests.

Desired state: market name and line searchable (e.g. "Singapore", "motor"); synonyms (SG↔Singapore, MY↔Malaysia, TH↔Thailand,
ID↔Indonesia, PH↔Philippines, car↔motor, home↔property — only for markets/lines the app has); typo tolerance OFF for claim
and policy number attributes; highlights returned and rendered in the header results (why a hit matched); facets by
status/market/line if the UI can show them simply; index settings applied idempotently at startup (compare then update, no
needless reindex); claimant scope enforcement (D-063) unchanged and still tested. Propose a D-row deciding other global-search
candidates (users for admins, customers) — decision only, do not build them.

Acceptance: integration tests for synonyms, exact-number no-typo, highlights present, settings idempotent (second startup does
not issue a settings update), claimant still only sees own claims; full suite under the lock; headless: typing "SG motor"
and a claim number shows highlighted results; 0 console errors.
Non-goals: audit search (only reuse patterns), new indexes for users/customers, UI outside the header search.
Stop if: the Meilisearch server version in compose lacks a needed setting — report and do what it supports.
Commits: 1 settings + startup apply + tests, 2 query/highlight API + tests, 3 web results rendering.

Recite: synonyms, market/line, exact numbers, highlights, idempotent settings, scope unchanged, tested.
