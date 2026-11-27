-- Claims an idempotency key; only an EXPIRED row is taken over. Returns the claim token when it won. Caller: IdempotencyStore.
INSERT INTO idempotency_keys (id, actor_id, endpoint, key, request_hash, claim_token, status, response_status, response_headers, response_body,
                              expires_at, created_at, created_by, updated_at, updated_by)
VALUES (@Id, @ActorId, @Endpoint, @Key, @RequestHash, @ClaimToken, 'pending', NULL, NULL, NULL, @ExpiresAt, @Now, @Actor, @Now, @Actor)
ON CONFLICT (actor_id, endpoint, key) DO UPDATE SET
    id = EXCLUDED.id, request_hash = EXCLUDED.request_hash, claim_token = EXCLUDED.claim_token, status = 'pending',
    response_status = NULL, response_headers = NULL, response_body = NULL, expires_at = EXCLUDED.expires_at,
    created_at = EXCLUDED.created_at, created_by = EXCLUDED.created_by, updated_at = EXCLUDED.updated_at, updated_by = EXCLUDED.updated_by
WHERE idempotency_keys.expires_at < @Now
RETURNING claim_token
