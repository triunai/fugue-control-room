# Migration catalog

One row per EF Core migration (and any hand-written SQL applied outside EF).

**Same-commit rule.** Any commit that adds, edits or removes a migration updates this catalog IN THE SAME COMMIT. A migration without a catalog row (or the reverse) is a defect. Numbers are execution order, not labels.

| # | Migration | Date | Tables touched | Tenant-owned? | Rollback | Notes |
|---|---|---|---|---|---|---|
| | _(none yet - Phase 1 creates the first)_ | | | | | |
