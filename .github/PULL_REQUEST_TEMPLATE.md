# Pull Request

## Summary

<!-- 1-3 bullets: what changed and why -->

## Test plan

<!-- How was this verified? Commands run, endpoints exercised, test names -->

- [ ]

---

## Migration checklist (skip if the PR adds or changes no EF migration / SQL)

- [ ] `docs/migrations/migration-catalog.md` updated in the SAME commit
- [ ] Rollback path written (down migration or documented manual steps) and tried on a scratch database
- [ ] Every new tenant-owned table has `tenant_id`, an index leading with it, and the EF global query filter
- [ ] No destructive change without a separate expand/contract step

## API contract checklist (skip if no controller, route, DTO or status-code change)

- [ ] OpenAPI snapshot updated and the diff reviewed
- [ ] Errors are `ProblemDetails`; no new ad-hoc error shape
- [ ] New list endpoints use keyset pagination; new unsafe POSTs accept an idempotency key where money or stock moves

## Tenancy checklist (skip if no tenant-owned entity, query, or auth policy change)

- [ ] Cross-tenant leak test added or extended for this module (tenant A cannot read or write tenant B data)
- [ ] Tenant comes from the verified JWT claim, never a client header or body field
- [ ] Raw SQL / Dapper reads filter on `tenant_id` explicitly (the EF filter does not cover them)

---

## Other checks

- [ ] No new `Skip =` on a test without a `RED:` comment
- [ ] No secrets, real connection strings or live keys in the diff
