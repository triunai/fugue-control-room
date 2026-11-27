-- Records a successful delivery, fenced by the claim token. Caller: OutboxDispatcher.
UPDATE webhook_deliveries
SET status = 'succeeded', delivered_at = @Now, locked_until = NULL, claim_token = NULL, last_status_code = @StatusCode, last_error = NULL, duration_ms = @DurationMs
WHERE id = @Id AND status = 'in_flight' AND claim_token = @ClaimToken
