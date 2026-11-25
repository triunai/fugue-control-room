# Migration catalog

One row per EF Core migration (and any hand-written SQL applied outside EF).

**Same-commit rule.** Any commit that adds, edits or removes a migration updates this catalog IN THE SAME COMMIT. A migration without a catalog row (or the reverse) is a defect. Numbers are execution order, not labels.

| # | Migration | Date | Tables touched | Tenant-owned? | Rollback | Notes |
|---|---|---|---|---|---|---|
| 1 | `20261125124928_InitialCustomers` | 2026-11-25 | `customers` (create), `ix_customers_created_at_id`, `ux_customers_email` | No (single-tenant default, ADR-003) | `dotnet ef database update 0 --project src/Fugue.Infrastructure` (the `Down` drops `customers`); local data only | Customers vertical slice. `xmin` is the concurrency token (system column, not created by the migration). Applied with `dotnet ef database update` using `ConnectionStrings__Postgres`. |
