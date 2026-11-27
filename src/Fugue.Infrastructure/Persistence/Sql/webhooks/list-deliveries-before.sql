-- Keyset page of webhook_deliveries before the cursor; EF composes the filters on top. Caller: WebhookDeliveryStore.
SELECT * FROM webhook_deliveries WHERE (occurred_at, id) < (@createdAt, @id)
