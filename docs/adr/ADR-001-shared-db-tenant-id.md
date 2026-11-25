# ADR-001: Shared database with a `tenant_id` column

- Status: Superseded by ADR-003
- Date: 2026-11-11

## Context

An early design assumed Fugue was multi-tenant. A previous work-derived .NET template resolved the tenant from a client-supplied header that nothing verified, so any caller could name any tenant. The data tier was Postgres reached through a connection pooler, which makes per-tenant databases and per-request `SET` session state awkward.

> Superseded by ADR-003: Fugue is single-tenant by default and tenancy is a configuration toggle. The mechanics below (JWT-derived tenant, EF query filter, leak tests) still describe how the toggle is implemented when switched on.

## Decision

- One shared Postgres database. Every tenant-owned row carries a `tenant_id` column.
- The tenant is resolved from a claim in a verified JWT, after authentication. A client header or body field is never a source of tenant identity.
- EF Core enforces isolation with a global query filter on every tenant-owned entity. Raw SQL and Dapper reads must filter on `tenant_id` explicitly.
- Each module ships a mandatory cross-tenant-leak test.
- The fallback authorization policy requires an authenticated user.
- Postgres row-level security is deferred as an optional backstop, not the primary control.

## Consequences

- Simple operations: one schema, one migration path, cheap tenant creation.
- Isolation is application-enforced, so the leak tests and the Dapper rule are load-bearing.
- A missed filter leaks data; RLS can later be added as defence in depth without changing the model.
- Pooler (transaction mode) compatibility is kept, since no session state is required.
