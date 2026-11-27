-- Deliver: leases due webhook_deliveries, counting the attempt at claim time. Caller: OutboxDispatcher.
UPDATE webhook_deliveries d
SET status = 'in_flight', attempts = d.attempts + 1, locked_until = @LeaseUntil, claim_token = gen_random_uuid()
FROM (
    SELECT id FROM webhook_deliveries
    WHERE (status = 'pending' AND next_attempt_at <= @Now) OR (status = 'in_flight' AND locked_until <= @Now)
    ORDER BY next_attempt_at, id
    LIMIT @Batch
    FOR UPDATE SKIP LOCKED
) due
WHERE d.id = due.id
RETURNING d.id AS Id, d.message_id AS MessageId, d.subscription_id AS SubscriptionId, d.attempts AS Attempts, d.claim_token AS ClaimToken
