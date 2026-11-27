-- Keyset page of audit_log before the cursor; EF composes the filters on top. Caller: AuditLogReader.
SELECT * FROM audit_log WHERE (occurred_at, id) < (@createdAt, @id)
