-- Inserts one access_audit row. Caller: AccessAuditWriter.
INSERT INTO access_audit (id, occurred_at, event, outcome, reason_code, user_id, email_hash, ip, user_agent, correlation_id)
VALUES (@Id, @OccurredAt, @Event, @Outcome, @ReasonCode, @UserId, @EmailHash, @Ip, @UserAgent, @CorrelationId)
