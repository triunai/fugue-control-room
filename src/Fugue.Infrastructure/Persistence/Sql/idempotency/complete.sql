-- Stores the response of a pending claim, fenced by the claim token. Caller: IdempotencyStore.
UPDATE idempotency_keys
SET status = 'completed', response_status = @Status, response_headers = CAST(@Headers AS jsonb), response_body = @Body, updated_at = @Now, updated_by = @Actor
WHERE id = @Id AND claim_token = @ClaimToken AND status = 'pending'
