-- Deletes a claim, fenced by the claim token. Caller: IdempotencyStore.
DELETE FROM idempotency_keys WHERE id = @Id AND claim_token = @ClaimToken
