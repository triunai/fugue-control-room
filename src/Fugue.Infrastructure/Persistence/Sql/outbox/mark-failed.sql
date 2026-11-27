-- Relay: records a failed relay attempt (retry or dead). Caller: OutboxDispatcher.
UPDATE outbox_messages SET status = @Status, attempts = @Attempts, next_attempt_at = @Next, last_error = @Error WHERE id = @Id
