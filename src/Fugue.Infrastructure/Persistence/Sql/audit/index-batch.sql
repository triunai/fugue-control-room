-- Keyset batch of audit_log after the search indexer checkpoint; EF composes the ordering and the limit. Caller: AuditIndexSource.
SELECT * FROM audit_log WHERE (occurred_at, id) > (@occurredAt, @id)
