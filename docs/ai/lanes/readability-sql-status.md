# Lane readability-sql: status

Branch `rd-sql`, base ce403cb. Builder: Sonnet. Infrastructure only.

## Commits
1. a9081c9 `refactor(persistence): runtime SQL in embedded .sql files`
2. 07fd013 `test(persistence): every SQL script referenced and present`
3. f9deb05 `build(infra): idempotent migration script and a read-only database role`
4. this report

## Counts (from output)
- `dotnet build -c Debug`: 0 Warning(s), 0 Error(s).
- `Fugue.Tests`: Passed 623, Failed 0 (618 base + 5 new SqlScriptsTests).
- `Fugue.ArchTests`: Passed 37 (after the arch commit; 36 of 37 before it).
- `git diff --exit-code ce403cb -- docs/api/openapi.v1.json`: SNAPSHOT-IDENTICAL.
- `dotnet format --verify-no-changes`: clean.
- `npm run db:script`: wrote `artifacts/db/migrate.sql` (82741 bytes); `artifacts/` was already gitignored.

## Arch rules now read the .sql files (commit test(arch): fitness rules read the .sql files)
Only the two rules in `tests/Fugue.ArchTests/FitnessTests.cs` changed; no baseline touched (`RefreshTokenStore.cs` still matches, so the new scan sees its scripts).
- audit_log insert rule: scans .cs and .sql, expects exactly `insert-audit-log.sql`; fails on an empty `.sql` glob.
- raw-sql-writes ratchet: each .cs file's text plus the comment-stripped text of every script it loads via `SqlScripts.Get`; asserts scripts exist and write statements were found.
- `-- Caller:` contract: `SqlScriptsTests.Every_script_header_names_a_caller_file_that_exists`.
Final counts: ArchTests 37 of 37 pass, Tests 623 pass, format clean.
Teeth (temporary edits, reverted): (1) `IAuditTrail` renamed away in CustomerRepository.cs ->
`[raw-sql-writes] new violations: CustomerRepository.cs` (RED). (2) header of `outbox/insert-message.sql` set to `Caller: NoSuchClass.` ->
`First line must be '-- <what>. Caller: <ClassName>.' naming an existing <ClassName>.cs: insert-message.sql` (RED). Both green again after revert.

## Greps
Remaining SQL literals in `src/Fugue.Infrastructure` outside Migrations (`"""`, `SELECT `, `INSERT `, `UPDATE `, `DELETE FROM`, `SAVEPOINT`, code lines only): none
(one comment mentions "the UPDATE below" in `RefreshTokenStore.cs:102`). 40 scripts under `Persistence/Sql/{audit,claims,customers,idempotency,identity,outbox,seeding,webhooks}`.

## Read-only role proof (throwaway `fugue-rdsql-pg`, postgres:17-alpine, port 5444; removed)
Migrated with `artifacts/db/migrate.sql` (27 public tables), then:
```
run 1 (first password)  OK        run 2 (second password)  OK  (idempotent; password rotated)
login with the old credential: FATAL: authentication failed for user "fugue_readonly"
SELECT count(*) FROM customers -> 0;  SELECT id FROM late_table (created AFTER the role) -> 7
INSERT INTO customers ...:   ERROR: cannot execute INSERT in a read-only transaction
show default_transaction_read_only -> on
SET default_transaction_read_only=off; INSERT ...: ERROR: permission denied for table customers
SET default_transaction_read_only=off; CREATE TABLE ...: ERROR: permission denied for schema public
```
Note: connecting over 127.0.0.1 inside the postgres image is `trust` (the first check wrongly accepted any password); the results above use the container hostname (scram).

## Deviations from the brief / Phase 1 plan
- `db:script` uses `--startup-project src/Fugue.Infrastructure` (not Api), as the lead said; it runs.
- Embedded resource uses `LogicalName="sql/%(RecursiveDir)%(Filename)%(Extension)"` so names are stable (`area/name`); `SqlScripts.Get` trims the trailing newline, so statement text equals the old raw strings.
- Extra scripts beyond the 14 listed files: IdentitySeeder, AuditLogReader, WebhookSubscriptionStore, relay SAVEPOINT statements (`webhooks/relay-*savepoint`).
- EF-interpolated statements became `FromSqlRaw` / `SqlQueryRaw` / `ExecuteSqlRawAsync` with named `NpgsqlParameter`s (same values, same types by inference). Caveat: EF applies `string.Format` to raw SQL, so a script must never contain `{` or `}`; none does.
- A `.sql` header comment is `-- <what>. Caller: <Class>.` (one line).
- Secrets-scan rejected a literal sample password in the script header and runbook; replaced by `REPLACE_ME` and `read -rs`.

## Dynamic SQL left in C#
None. Every keyset filter composes through EF LINQ on top of a fixed `FromSqlRaw` script.

## Could NOT verify, and what would settle it
- `.mcp.json` runtime behaviour (the `${VAR}` expansion and `uvx postgres-mcp` startup): config only by the brief. Settle: set `FUGUE_READONLY_DATABASE_URI`, open Claude Code in the repo, `/mcp`.
- Windows resource names (`RecursiveDir` uses backslashes; `SqlScripts` normalises them): not run on Windows.
