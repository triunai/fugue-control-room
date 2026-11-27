# Lane readability-sql (Sonnet builder): runtime SQL in .sql files (D-054)

## Objective
Every runtime SQL statement in `src/Fugue.Infrastructure` reads as SQL in its own `.sql` file, loaded by one small `SqlScripts` helper, with behaviour unchanged and proven unchanged.

## Lane facts
Given by the lead in the dispatch prompt: worktree path, branch, base sha, and which other lane runs in parallel.

## Current state (verify; docs rot)
- Inline raw-string SQL (`"""`) or string-constant SQL in about 14 files: `Outbox/OutboxWriter.cs`, `Identity/RefreshTokenStore.cs`, `Identity/UserAccessReader.cs`, `Identity/AccessAuditWriter.cs`, `Audit/AuditTrail.cs`, `Idempotency/IdempotencyStore.cs`, `Persistence/Customers/CustomerRepository.cs`, `Persistence/Claims/ClaimRepository.cs`, `Persistence/Claims/ClaimReports.cs`, `Persistence/Seeding/ClaimSeeder.cs`, `Persistence/Seeding/CustomerSeeder.cs`, `Webhooks/WebhookDeliveryStore.cs`, `Webhooks/OutboxDispatcher.cs`, `Webhooks/WebhookFanOutPublisher.cs`. Find the full set yourself (`grep -rn --include='*.cs' -e '"""' -e 'SELECT ' -e 'INSERT ' -e 'UPDATE ' src/Fugue.Infrastructure`, excluding `Migrations/`).
- Schema migrations are EF Core and STAY EF Core.

## Desired final state
1. `src/Fugue.Infrastructure/Persistence/Sql/<area>/<name>.sql` (areas: `audit`, `claims`, `customers`, `identity`, `idempotency`, `outbox`, `seeding`, `webhooks`), embedded resources (`<EmbeddedResource Include="Persistence/Sql/**/*.sql" />`).
2. `SqlScripts.Get("claims/list-page")` (internal static, reads each resource once into a `FrozenDictionary` or `ConcurrentDictionary`; throws `InvalidOperationException` naming the missing script). Callers keep a `private static readonly string` or call it inline; no behaviour change, same parameter names.
3. A test (`tests/Fugue.Tests/Infrastructure/SqlScriptsTests.cs`) that (a) every `SqlScripts.Get("...")` literal in `src/` resolves to an embedded script and (b) no embedded script is unreferenced.
4. A short header comment in each `.sql` file: one line on what it does and who calls it.
5. Dynamic SQL built from fragments (if any, e.g. a WHERE assembled from filters) stays in C# with a comment saying why; list them in the report.
6. `npm run db:script` in root `package.json`: `dotnet ef migrations script --idempotent --project src/Fugue.Infrastructure --startup-project src/Fugue.Api -o artifacts/db/migrate.sql` (make sure `artifacts/` is gitignored; verify the command produces the file).
7. `scripts/db/fugue-readonly-role.sql`: idempotent creation of a `fugue_readonly` LOGIN role (password from a psql variable, never hard-coded), `GRANT CONNECT`, `USAGE ON SCHEMA public`, `SELECT ON ALL TABLES` + `ALTER DEFAULT PRIVILEGES ... GRANT SELECT`, and `ALTER ROLE fugue_readonly SET default_transaction_read_only = on`. Prove it on a throwaway DB: a SELECT works, an INSERT fails.
8. Project `.mcp.json` at repo root with a `postgres-readonly` server: `uvx postgres-mcp --access-mode=restricted` with `DATABASE_URI` taken from the env var `FUGUE_READONLY_DATABASE_URI` (no secret in the file). Do not install or run the MCP; config only.
9. Runbook `docs/runbooks/03-local-database.md` gains a short "SQL files, migration script, read-only access for agents" section.

## Allowed files
`src/Fugue.Infrastructure/**` (not `Migrations/**`), `tests/Fugue.Tests/Infrastructure/SqlScriptsTests.cs` (new), `package.json` (the one script), `.gitignore`, `scripts/db/**`, `.mcp.json`, `docs/runbooks/03-local-database.md`.

## Architecture rules
Layering and arch tests unchanged; no new packages; Dapper/Npgsql calls unchanged apart from where the string comes from; parameter names and order unchanged; `xmin` and `FOR UPDATE` clauses preserved byte for byte.

## Acceptance (binary)
1. `dotnet build` 0 warnings; `dotnet test` all green (ArchTests + Tests; counts in the report, copied from output).
2. `docs/api/openapi.v1.json` byte-identical (`git diff --exit-code docs/api/openapi.v1.json`).
3. No SQL literal left in `src/Fugue.Infrastructure` outside `Migrations/` and the documented dynamic fragments (show the grep).
4. `npm run db:script` produces `artifacts/db/migrate.sql`; the read-only role proof output pasted in the report.
5. `dotnet format --verify-no-changes` clean.

## Validation commands
```bash
dotnet build -c Debug 2>&1 | tail -3
dotnet test --no-build 2>&1 | grep -E "Passed!|Failed!"
git diff --exit-code docs/api/openapi.v1.json && echo SNAPSHOT-IDENTICAL
dotnet format --verify-no-changes && echo FORMAT-CLEAN
npm run db:script && ls -la artifacts/db/migrate.sql
```

## Non-goals
EF migrations, query rewrites or "improvements" to any SQL, Dapper-vs-EF changes, the Application/Domain/Api layers (another lane is consolidating files there), CLAUDE.md/AGENTS.md (the lead edits rule files), the doc spine (`docs/hot-state.md`, `docs/decisions.md`, journal).

## Stop conditions
A moved statement changes a test result and the cause is not a typo in the move: stop and report. An arch test forbids embedded resources or the helper's placement: report with the rule text.

## Commits (pathspec only; trailers `Refs: W-007, D-054` + `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`)
1. `refactor(persistence): runtime SQL in embedded .sql files`
2. `test(persistence): every SQL script referenced and present`
3. `build(infra): idempotent migration script and a read-only database role`

## Verify the premises
Before acting on any claim here, check it against the repo. If false, say what is true (`file:line`) and proceed on the fact.

## Report
Write `docs/ai/lanes/readability-sql-status.md` (commits, counts, the greps, the role proof, deviations from this plan, dynamic SQL left in C#, what you could NOT verify and what would settle it), commit it by pathspec, reply ONE line.

## Recite
SQL as SQL: embedded `.sql` files + `SqlScripts.Get`, a both-ways existence test, `db:script`, a proven read-only role, MCP config; behaviour and the OpenAPI snapshot unchanged; Infrastructure only.
