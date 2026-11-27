-- Keyset page of webhook_subscriptions after the cursor; EF composes the filters on top. Caller: WebhookSubscriptionStore.
SELECT * FROM webhook_subscriptions WHERE (created_at, id) > (@createdAt, @id)
