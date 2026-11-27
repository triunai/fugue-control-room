-- Operator redeliver: revives a dead delivery in one statement so two operators cannot both win. Caller: WebhookDeliveryStore.
UPDATE webhook_deliveries
SET status = 'pending', attempts = 0, next_attempt_at = @Now, locked_until = NULL, claim_token = NULL, dead_lettered_at = NULL, last_error = NULL
WHERE id = @Id AND status = 'dead'
