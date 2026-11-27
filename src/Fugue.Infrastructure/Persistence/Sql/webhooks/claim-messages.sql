-- Relay: claims due outbox_messages with FOR UPDATE SKIP LOCKED (keep the clause). Caller: OutboxDispatcher.
SELECT id AS Id, occurred_at AS OccurredAt, event_type AS EventType, aggregate_type AS AggregateType, aggregate_id AS AggregateId,
       payload::text AS Payload, correlation_id AS CorrelationId, saga_id AS SagaId, traceparent AS Traceparent, attempts AS Attempts
FROM outbox_messages
WHERE status = 'pending' AND next_attempt_at <= @Now
ORDER BY next_attempt_at, id
LIMIT @Batch
FOR UPDATE SKIP LOCKED
