-- Creates one pending webhook_deliveries row per subscription; a repeat is a no-op. Caller: WebhookFanOutPublisher.
INSERT INTO webhook_deliveries (id, message_id, subscription_id, occurred_at, status, attempts, next_attempt_at)
VALUES (@Id, @MessageId, @SubscriptionId, @Now, 'pending', 0, @Now)
ON CONFLICT (message_id, subscription_id) DO NOTHING
