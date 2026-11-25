---
name: migration-retrofit-qa
description: Use before applying or committing ANY EF Core migration in Fugue, and whenever a migration is authored, amended or reviewed. Runs the retrofit gate - forward migration, idempotent SQL script review, Down() rollback, Testcontainers pin test, migration-catalog row in the same commit - then a cross-family review. Triggers - "add a migration", "check this migration", "is this ready to apply", "dotnet ef migrations", "migration QA".
---

# Migration retrofit QA (EF Core / Postgres)

Migrations are a critical seam: plan before coding (`docs/plans/`), independent review before merge, catalog updated in the SAME commit (CLAUDE.md).

## Gate (all steps, in order)
1. **Forward migration**: `dotnet ef migrations add <Name> --project src/Fugue.Infrastructure --output-dir Persistence/Migrations`. Read the generated `Up()` and the snapshot diff: only the intended change, no accidental drops or renames (a rename scaffolds as drop+add: fix it by hand to `RenameColumn`/`RenameTable`). `xmin` is a system column: never create it; concurrency token only.
2. **Idempotent script review**: `dotnet ef migrations script --idempotent --project src/Fugue.Infrastructure -o /tmp/<name>.sql` (use the scratchpad), then read it. Check: locks on large tables (index creation: consider `CONCURRENTLY` via raw SQL, which cannot run inside a transaction), NOT NULL on existing rows needs a default or backfill, data loss, ordering, snake_case names.
3. **`Down()` rollback**: written and sane. Prove it on a scratch database: `dotnet ef database update <PreviousMigration>` then back. Destructive downs are called out in the catalog row.
4. **Testcontainers pin test**: an xUnit test on real Postgres (not in-memory) that migrates to latest and asserts the new schema fact (column, index, constraint, or backfilled data), RED before the migration exists and GREEN after. For state machines and SQL logic write the test first.
5. **Catalog row**: `docs/migrations/migration-catalog.md`, same commit: number, migration id, date, tables touched, tenant-owned?, rollback, notes. The fitness test `Fitness_Migrations_every_migration_has_a_catalog_row` fails the build otherwise.
6. **Tenancy checklist** (when `Tenancy:Enabled` work lands): tenant column, query filter, leak test.
7. **Execute, do not infer**: run `dotnet build` (0 warnings), `dotnet test --no-build`, `dotnet format --verify-no-changes`. Quote the counts.
8. **Review**: run `dual-gate-review` with a cross-family reviewer tracing every consumer of the changed schema (repositories, Dapper SQL strings in `SqlExecutor` callers, projections, seeders, Bruno requests). Every HIGH/MED gets a pin.
9. **Apply** only against the local compose database, and only after 1 to 8. Never `dotnet ef database drop` without `--dry-run`, never against a shared database; the safe-command guard blocks it.

## Pitfalls already hit
`xmin::text::bigint` for Dapper SQL; host Postgres on 5432 shadows the container (use `POSTGRES_PORT`); the Mediator generator ignores test-assembly handlers. See `docs/failure-modes.md`.
