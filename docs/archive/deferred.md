# Deferred items (cold)

Pruned from `docs/plans/roadmap.md` and the board so the warm tier stays small. Each line says when to revive it. Reviving = copy it back into the roadmap and open a `W-NNN` thread.

| Item | Why parked | Revive when |
|---|---|---|
| S6 YARP gateway, load-balancing two API replicas in compose | no second consumer; adds a moving part | the API runs as more than one replica, or a gateway concern (routing, auth offload) appears |
| Kafka adapter behind `IEventPublisher` | out of scope for the 3-day horizon; the outbox seam already isolates it | a target domain needs a broker, or throughput outgrows the polling dispatcher |
| Tenancy toggle implementation (`Tenancy:Enabled`, ADR-003): EF query filter, tenant from JWT, leak tests | the template is single-tenant by default | a target domain is multi-tenant; do it after S3 so the claim exists |
| Dapper read paths | EF `AsNoTracking` projections cover reads (ADR-006) | a measured hot read path EF cannot serve |
| Per-user deny overrides in authorization | not needed by two role policies | a resource needs "allow role, deny user" |
| Audit PII redaction and field-level knobs | audit stores actor id and timestamps only | audit records start to carry personal data |
| Audit sampling | never: audit records are not sampled | never (kept so nobody re-proposes it) |
