# W-006 plan: transactional outbox and signed outbound webhooks (S4)

Critical seams: migration, signing, SSRF, secrets. No broker: the outbox is the seam, signed webhooks the first publisher; a Kafka adapter (`IEventPublisher`, keyed by aggregate id) is documented in ADR-011, not built.

## Decisions (D-037 to D-040, promoted to ADR-011)
| Id | Decision |
|---|---|
| A | `outbox_messages`: UUIDv7 id, occurred_at, versioned `event_type` (`customer.created.v1`), aggregate type/id, `payload` jsonb, correlation id, saga id (= aggregate id when unset), traceparent, status (`pending`/`dispatched`/`dead`), attempts, next_attempt_at, last_error, dispatched_at. Same transaction as the change. Not `IAuditable` (append-only allowlist, has `occurred_at`); a trigger rejects any UPDATE that changes the immutable columns (payload included), so status changes stay legal. DELETE stays legal (retention). `[NoAudit]`. |
| B | Capture: `Customer` raises domain events (created, updated, suspended, reactivated, deleted). An EF SaveChanges interceptor turns them into outbox rows in the same SaveChanges; the Dapper insert path calls `IOutbox` inside the transaction (mirrors `IAuditTrail`). Payload is "thin" and masked with the audit redaction rules (email and phone partially masked); receivers fetch the resource by id. |
| C | `webhook_subscriptions` (IAuditable, xmin): url, filters (`customer.*`, `*`, exact), is_active. Secret `whsec_...` encrypted with Data Protection; key ring in the database (`data_protection_keys`, `[NoAudit]`) so every instance shares it. Shown once on create or rotate. CRUD plus rotate: `[Module("WEBHOOKS")]`, `WEBHOOKS_MANAGE`, ADMIN. |
| D | SSRF: https only, no userinfo, resolve DNS and reject loopback, private, link-local (metadata), CGNAT, multicast, reserved. Checked at subscribe time AND at connect time (`ConnectCallback` dials the verified IP, defeating DNS rebinding); auto-redirect off. `Webhooks:AllowInsecureLocal` (Development only; startup fails elsewhere) permits http plus loopback and private, never link-local. |
| E | Two stages. Relay: `FOR UPDATE SKIP LOCKED` claim in one transaction, publish via `IEventPublisher` (the webhook adapter inserts one `webhook_deliveries` row per matching subscription, `ON CONFLICT DO NOTHING`), mark dispatched; a savepoint per message isolates poison. Delivery: lease claim (`UPDATE ... IN (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING`), HTTP outside any transaction, result write fenced by attempt number, expired lease reclaimed. Exponential backoff with jitter; dead after `MaxAttempts`. |
| F | `AddStandardResilienceHandler`, pipeline per authority (timeouts, circuit breaker). Its own retry is one attempt and disabled for POST: the outbox is the single retry authority (durable, counted, long backoff, visible); a hidden in-memory retry blurs the attempt count and holds the lease. |
| G | Signature `X-Fugue-Signature: v1=hex(HMAC-SHA256(secret, timestamp + "." + rawBody))`; headers event id, type, timestamp, correlation, saga, traceparent. At-least-once: receivers verify, reject timestamps older than 5 minutes, dedupe on event id. |
| H | Development-only sink `POST /api/v1/dev/webhook-sink` (anonymous by design, signature is the credential; 404 outside Development) verifies, dedupes, keeps the last N in memory; `GET` lists. |

## Commits (green seams)
1. Schema: entities, ids, configs, `AddOutboxWebhooks` migration (triggers, CHECKs, seed), catalog, pin tests, fitness baselines.
2. Capture: domain events, interceptor, `IOutbox`, payload masking, tests.
3. Subscriptions: SSRF guard, Data Protection, CRUD, tests.
4. Dispatcher, signing, resilience, redeliver, sink, tests.
5. Docs, Bruno, ADR-011, runbook 07.

## Non-goals
Kafka, outbox pruning, ordering guarantees, rotate overlap window, metrics (S5).

## Stop conditions
A package will not restore; a migration needs a destructive change.
