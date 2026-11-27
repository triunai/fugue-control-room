-- Inserts one outbox_messages row (the ADR-011 write path). Caller: OutboxWriter.
INSERT INTO outbox_messages (id, occurred_at, event_type, aggregate_type, aggregate_id, payload, correlation_id, saga_id, traceparent,
                             status, attempts, next_attempt_at)
VALUES (@Id, @OccurredAt, @EventType, @AggregateType, @AggregateId, CAST(@Payload AS jsonb), @CorrelationId, @SagaId, @Traceparent,
        @Status, 0, @NextAttemptAt)
