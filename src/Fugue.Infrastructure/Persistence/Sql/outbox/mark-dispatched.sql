-- Relay: marks an outbox message dispatched. Caller: OutboxDispatcher.
UPDATE outbox_messages SET status = 'dispatched', attempts = attempts + 1, dispatched_at = @Now, last_error = NULL WHERE id = @Id
