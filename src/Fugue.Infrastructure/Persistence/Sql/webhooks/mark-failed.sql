-- Records a failed or dead-lettered delivery, fenced by the claim token. Caller: OutboxDispatcher.
UPDATE webhook_deliveries
SET status = @Status, next_attempt_at = @NextAttemptAt, locked_until = NULL, claim_token = NULL, last_status_code = @StatusCode, last_error = @Error,
    duration_ms = @DurationMs, dead_lettered_at = @DeadLetteredAt
WHERE id = @Id AND status = 'in_flight' AND claim_token = @ClaimToken
