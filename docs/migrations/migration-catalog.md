# Migration catalog

One row per EF Core migration (and any hand-written SQL applied outside EF).

**Same-commit rule.** Any commit that adds, edits or removes a migration updates this catalog IN THE SAME COMMIT. A migration without a catalog row (or the reverse) is a defect. Numbers are execution order, not labels.

| # | Migration | Date | Tables touched | Tenant-owned? | Rollback | Notes |
|---|---|---|---|---|---|---|
| 1 | `20261125124928_InitialCustomers` | 2026-11-25 | `customers` (create), `ix_customers_created_at_id`, `ux_customers_email` | No (single-tenant default, ADR-003) | `dotnet ef database update 0 --project src/Fugue.Infrastructure` (the `Down` drops `customers`); local data only | Customers vertical slice. `xmin` is the concurrency token (system column, not created by the migration). Applied with `dotnet ef database update` using `ConnectionStrings__Postgres`. |
| 2 | `20261125141352_AddIdentity` | 2026-11-25 | `tenants`, `users`, `roles`, `modules`, `resources`, `actions`, `permissions`, `role_permissions`, `user_roles`, `groups`, `group_members`, `group_roles`, `user_permission_overrides`, `refresh_tokens`, `grant_audit`, `access_audit` (create); `customers.user_id` (add, FK `SET NULL`, index `ix_customers_user_id`, not unique); reference seed via `HasData` (3 modules, 4 resources, 7 actions, 12 permissions, 3 roles, role grants, group `SUPPORT`) | No (single-tenant default, ADR-003; `users.tenant_id` is a nullable FK, no query filter) | `dotnet ef database update InitialCustomers --project src/Fugue.Infrastructure` (the `Down` drops the 16 tables and `customers.user_id`); local data only | UAM v3 lean port, D-016..D-026. Partial unique index `ux_users_email_active` (`WHERE NOT is_deleted`) on the canonical email. Retrofit QA 2026-11-25: forward, rollback, forward again and the idempotent script (run twice) verified on a throwaway postgres:17 container; seed counts stable at 12 permissions. Users are never in a migration. |
