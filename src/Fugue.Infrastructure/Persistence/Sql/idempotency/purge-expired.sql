-- Deletes one batch of expired idempotency keys. Caller: IdempotencyStore.
DELETE FROM idempotency_keys
WHERE id IN (SELECT id FROM idempotency_keys WHERE expires_at < @Now ORDER BY expires_at LIMIT @Batch)
