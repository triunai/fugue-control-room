-- Gives a claim back without costing an attempt (the lease ran out before it was reached). Caller: OutboxDispatcher.
UPDATE webhook_deliveries
SET status = 'pending', attempts = attempts - 1, locked_until = NULL, claim_token = NULL
WHERE id = @Id AND status = 'in_flight' AND claim_token = @ClaimToken
