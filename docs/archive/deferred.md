# Deferred items (cold)

Pruned from `docs/plans/roadmap.md` and the board so the warm tier stays small. Each line says when to revive it. Reviving = copy it back into the roadmap and open a `W-NNN` thread.

| Item | Why parked | Revive when |
|---|---|---|
| S6 YARP gateway, load-balancing two API replicas in compose | no second consumer; adds a moving part | the API runs as more than one replica, or a gateway concern (routing, auth offload) appears |
| Kafka adapter behind `IEventPublisher` | out of scope for the 3-day horizon; the outbox seam already isolates it | a target domain needs a broker, or throughput outgrows the polling dispatcher |
| Tenancy toggle implementation (`Tenancy:Enabled`, ADR-003): EF query filter, tenant from JWT, leak tests | the template is single-tenant by default | a target domain is multi-tenant; do it after S3 so the claim exists |
| Dapper read paths | EF `AsNoTracking` projections cover reads (ADR-006) | a measured hot read path EF cannot serve |
| Audit PII redaction and field-level knobs | audit stores actor id and timestamps only | audit records start to carry personal data |
| Audit sampling | never: audit records are not sampled | never (kept so nobody re-proposes it) |
| Refresh tokens and `user_sessions` (UAM v3) | S3 ships 15 min access tokens only | a client needs sessions longer than 15 min, or logout must revoke |
| SSO columns and login (UAM v3 `users`) | local login only | an identity provider is introduced |
| Tenants table and `tenant_id` on roles, user_roles, overrides (UAM v3) | single-tenant default (ADR-003) | the tenancy toggle is implemented |
| Groups, group_members, group_roles (UAM v3) | roles assigned per user are enough | role assignment per user becomes unmanageable |
| access_audit and grant_audit (UAM v3) | audit stores actor and time only; grant audit is a strong next add | first admin-facing role or override change, or a compliance ask |
| Domain tables from UAM v3 (clients, projects, mappings, position types, assignments) | not part of a domain-neutral scaffold | a target domain needs them |
| User and role CRUD APIs, password reset and expiry, lockout | S3 seeds users and roles | self-service onboarding, or S5 rate limiting proves insufficient |
