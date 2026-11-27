-- Dead-letters an in-flight delivery whose lease expired on its final attempt. Caller: OutboxDispatcher.
UPDATE webhook_deliveries
SET status = 'dead', dead_lettered_at = @Now, locked_until = NULL, claim_token = NULL,
    last_error = COALESCE(last_error, 'The final attempt was claimed and never recorded (the worker stopped).')
WHERE status = 'in_flight' AND locked_until <= @Now AND attempts >= @MaxAttempts
