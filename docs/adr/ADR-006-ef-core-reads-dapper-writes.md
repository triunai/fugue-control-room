# ADR-006: EF Core for reads and concurrency-checked updates, Dapper for inserts and set-based writes

- Status: Accepted
- Date: 2026-11-25

## Context

An external review suggested deferring Dapper: "EF is enough until a measured hot path". The owner overrode that. The project exists to practise the full data-access toolbox, and a split with a clear rule is a better rehearsal than one tool used everywhere. Two things make a split dangerous if left implicit: optimistic concurrency (a lost update if two writers race) and atomicity (an EF change and a Dapper statement committing separately).

## Decision

| Operation | Tool | Why |
|---|---|---|
| Reads: get, list | EF Core, `AsNoTracking` projections, keyset paging | Composable queries, typed projections, no change tracker cost. |
| Single-aggregate update or delete that must honour `If-Match` | EF Core, tracked entity | Postgres `xmin` is the concurrency token (`IsRowVersion`). EF adds `WHERE xmin = @original` to the UPDATE/DELETE and raises `DbUpdateConcurrencyException`, which becomes `PreconditionFailed`. The ETag is the `xmin` value. |
| Inserts, upserts (`INSERT ... ON CONFLICT`), complex or set-based writes | Dapper | Explicit SQL, `RETURNING xmin`, unique-violation handling by SQLSTATE `23505`, upserts EF cannot express. |

Rules:

- **One connection, one transaction.** Dapper never opens its own connection. `SqlExecutor` runs on `db.Database.GetDbConnection()` and `db.Database.CurrentTransaction?.GetDbTransaction()`. `IUnitOfWork` (`BeginAsync` / `CommitAsync`, dispose without commit rolls back) starts the EF transaction, so EF `SaveChanges` and Dapper statements inside it are atomic. This is the seam the S4 outbox uses: the business change and the outbox row commit together.
- **Audit stamping on both paths.** `IAuditStamp` is the single source of now/actor. The EF path is `AuditableSaveChangesInterceptor`; the Dapper path binds `IAuditStamp.Capture()` values explicitly in its SQL. An integration test proves both.
- **Dapper does not read domain aggregates.** Reads stay in EF so projections and filters have one home.
- Application code sees only repository interfaces; `Fugue.Domain` and `Fugue.Application` reference neither EF nor Dapper nor Npgsql (arch test).

## Consequences

- A developer must pick the right tool per operation; this ADR is the rule.
- A Dapper write that needs a version check must do it in SQL (`WHERE xmin = ...`); the default is to route such writes through EF.
- `xid` has no SQL cast to `bigint`: reads project the raw `uint` and widen in memory; Dapper uses `xmin::text::bigint`.
- Cost: two data-access libraries to keep patched. Benefit: the rehearsal covers both, and the transaction seam is explicit.
