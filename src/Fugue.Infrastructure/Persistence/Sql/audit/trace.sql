-- Every row one correlation id left behind (D-064): audit rows (the command), outbox messages (the event), webhook deliveries of those messages. Caller: AuditTraceReader.
SELECT 0 AS rank, 'command' AS kind, a.id, a.occurred_at AS at, NULL::timestamptz AS end_at,
       a.action AS label, NULLIF(concat_ws(' ', a.http_method, a.route_template), '') AS detail, a.outcome AS status,
       a.duration_ms, NULL::int AS attempts, NULL::int AS status_code, a.error_code, a.entity_type, a.entity_id
FROM audit_log a
WHERE a.correlation_id = @correlationId
UNION ALL
SELECT 1, 'event', m.id, m.occurred_at, m.dispatched_at,
       m.event_type, NULL, m.status,
       NULL, m.attempts, NULL, NULL, m.aggregate_type, m.aggregate_id
FROM outbox_messages m
WHERE m.correlation_id = @correlationId
UNION ALL
SELECT 2, 'delivery', d.id, d.occurred_at, coalesce(d.delivered_at, d.dead_lettered_at),
       m.event_type, s.name, d.status,
       d.duration_ms, d.attempts, d.last_status_code, NULL, m.aggregate_type, m.aggregate_id
FROM webhook_deliveries d
JOIN outbox_messages m ON m.id = d.message_id
JOIN webhook_subscriptions s ON s.id = d.subscription_id
WHERE m.correlation_id = @correlationId
ORDER BY at, rank, id
LIMIT 500
