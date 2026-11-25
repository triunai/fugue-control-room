# ADR-003: Single-tenant by default, tenancy as a configuration toggle

- Status: Accepted
- Date: 2026-11-25
- Supersedes: ADR-001

## Context

ADR-001 made every table tenant-owned (shared database, `tenant_id` on every row). Fugue is now a domain-neutral scaffold meant to be reused for different target domains, and an external review (2026-11-25) pointed out that in some of those domains markets or regions are **dimensions of the data, not tenants**: they need cross-cutting reporting across all markets, which a hard tenant filter forbids. Forcing tenancy on every project also taxes every query, test and migration with a concern many projects do not have.

History: the first recommendation was a shared database with Postgres row-level security; the owner chose shared database plus `tenant_id` only (no RLS). This ADR keeps that mechanism but makes it optional.

## Decision

- Fugue is **single-tenant by default**. Nothing in the base scaffold assumes a tenant.
- Tenancy is a configuration toggle: `Tenancy:Enabled=false` (default).
- When enabled (implemented later, tracked in `docs/plans/roadmap.md`):
  - the tenant is read from a **verified JWT claim after authentication**, never from a header, query or body;
  - tenant-owned entities carry `tenant_id` and an EF Core global query filter keyed on the current tenant;
  - each tenant-owned feature ships a cross-tenant-leak test;
  - cross-tenant misses answer 404, not 403 (see the `MapStatus` decision comment).
- Markets, regions and similar dimensions are modelled as ordinary columns/entities, not tenants.
- Postgres RLS stays out of scope.

## Consequences

- The default project is simpler: no filter, no tenant claim, no leak tests.
- Turning tenancy on is a deliberate step with its own review (it is a critical seam in `AGENTS.md`).
- `ICurrentUser.TenantId` stays nullable and unused until the toggle is implemented.
- Raw SQL, if ever introduced, must add the tenant predicate itself when tenancy is on; EF filters do not apply to it.
