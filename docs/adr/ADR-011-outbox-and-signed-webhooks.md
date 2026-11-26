# ADR-011 — Transactional outbox and signed outbound webhooks

- Status: accepted
- Date: 2026-11-26
- Decision entry: D-037, D-038, D-039, D-040 in `docs/decisions.md`

## Context
Other systems need to learn what changed (a customer was created, suspended, ...). The owner asked "REST or Kafka?". There is no broker in scope, but the answer must not box one out. The hard part is not the transport: it is that an event must exist if and only if the business change committed, and must reach the receiver at least once even when processes crash. Plan: `docs/plans/s4-outbox-webhooks.md`.

## Decision
* **Outbox.** `outbox_messages` is written in the same transaction as the change (ADR-006's unit of work). `Customer` raises domain events; an EF SaveChanges interceptor converts them to outbox rows in the same SaveChanges (ADR-010's pattern), and the Dapper insert path calls `IOutbox` inside the same transaction. Columns: UUIDv7 id, `event_type` (versioned, `customer.created.v1`), aggregate type and id, jsonb payload, correlation id, saga id (the aggregate id when the request carried none), traceparent, status (`pending`, `dispatched`, `dead`), attempts, next attempt, last error, dispatched at. The payload is thin and masked with the audit rules (email and phone partially masked, never secrets); receivers fetch the resource by id. Event columns are frozen by a trigger (status columns move), DELETE is legal for retention, the entity is `[NoAudit]` and on the append-only allowlist (it has `occurred_at`).
* **Subscriptions.** `webhook_subscriptions` (IAuditable, `xmin` token): url, filters (`customer.*`, `*`, an exact type), is_active, an encrypted secret. CRUD plus rotate under `[Module("WEBHOOKS")]` and `WEBHOOKS_MANAGE` (admin). The secret is `whsec_` plus 32 random bytes, encrypted with Data Protection whose key ring lives in `data_protection_keys`; it is shown once, on create and on rotate.
* **SSRF.** https only, no userinfo; resolve and reject loopback, private, link-local (metadata), CGNAT, multicast, reserved (IPv4-mapped IPv6 included), at subscribe time and again at connect time through a `ConnectCallback` that dials the address it verified (DNS rebinding); redirects are not followed. `Webhooks:AllowInsecureLocal` (Development only, startup fails elsewhere) permits http plus loopback and private, never link-local.
* **Dispatcher.** A `BackgroundService` runs two stages. Relay: `SELECT ... FOR UPDATE SKIP LOCKED` claim in one transaction, `IEventPublisher` per message (the webhook adapter inserts one `webhook_deliveries` row per matching active subscription, `ON CONFLICT DO NOTHING`), mark dispatched; a savepoint per message isolates a poison message. Delivery: a lease claim (`UPDATE ... IN (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING`, attempt counted at claim time), the HTTP call with no transaction open, a result write fenced by the attempt number. An expired lease is reclaimed. Backoff is exponential with jitter; after `MaxAttempts` the delivery is dead; `POST /webhooks/deliveries/{id}/redeliver` revives it with a fresh budget.
* **Resilience.** `AddStandardResilienceHandler` (Microsoft.Extensions.Http.Resilience, Polly v8) per authority: attempt and total timeouts, circuit breaker, rate limiter. Its retry is one attempt and disabled for POST: the outbox is the single retry authority.
* **Signing.** `X-Fugue-Signature: v1=<hex HMAC-SHA256(secret, timestamp + "." + rawBody)>`, `X-Fugue-Event-Id`, `X-Fugue-Event-Type`, `X-Fugue-Timestamp` (unix seconds), `X-Correlation-Id`, `X-Saga-Id`, `traceparent`. Delivery is at-least-once: a receiver verifies the signature in constant time, rejects a timestamp older than 5 minutes (replay window) and dedupes on the event id.
* **Broker seam.** `IEventPublisher` is the port; the webhook fan-out is its first adapter. A Kafka adapter would produce each outbox message to a topic with the AGGREGATE ID as the message key (Kafka orders within a partition and a key maps to one partition, so per-aggregate order holds), the event id as an idempotency header, `acks=all` and an idempotent producer; the relay's mark-dispatched step stays after the produce, so delivery stays at-least-once and consumers dedupe on the event id exactly like webhook receivers. Not built. Webhooks give no ordering guarantee under retries.
* **Dev sink.** `POST /api/v1/dev/webhook-sink` (Development only, 404 elsewhere, anonymous because the signature is the credential) verifies signature and timestamp, dedupes by event id, keeps the last N; `GET` lists them.

## Alternatives considered
* **Dual write** (commit, then publish): loses the event when the process dies between the two, and publishes events for changes that later roll back. The outbox exists to remove this.
* **Publish inside the transaction** (call the receiver before commit): couples request latency and success to a third party and still cannot be atomic.
* **Database triggers or logical decoding as the capture**: no actor, correlation or masking context; the audit decision (ADR-010) already ruled this out.
* **Hold row locks across the HTTP call** (claim, send, update in one transaction): pins a connection per slow receiver and blocks other workers; leases keep the claim short and crash safe.
* **Rely on the resilience handler's retries**: invisible to the data, not counted against a budget, holds the lease. The outbox retries across polls with real backoff.
* **Hash the subscription secret**: signing needs the raw value. Encrypted at rest is the compromise.
* **Secret from a config key (AES-GCM by hand)**: the owner asked for Data Protection; rolling our own crypto is also a hard "no".
* **Kafka now**: a broker, schema and consumer story for a scaffold; the seam keeps the option open.

## Consequences
* A new event type needs a versioned name constant and a payload mapping (fitness function); renaming means a new `v2`, never an edit.
* The relay and the delivery worker are safe to run in several instances; a lease expiring during a very slow send can double-send, so receivers MUST dedupe on the event id.
* The key ring and the ciphertext share a database: a dump exposes both. Production should protect the key ring at rest (`ProtectKeysWith*`); losing the key ring makes stored secrets unreadable (rotate each).
* `Application` never references `HttpClient` or Data Protection (fitness function); it sees ports (`IEventPublisher`, `IOutbox`, `ISecretProtector`, `IWebhookUrlGuard`).
* The outbox, deliveries and key ring are `[NoAudit]`; subscription changes and redeliveries are audited (the secret is `[NoAudit]` on the entity).
* Outbox pruning, per-subscription ordering, a rotate overlap window and metrics are out of scope (`docs/archive/deferred.md`, S5).

## Supersedes
None. Builds on ADR-005 (ids on the wire), ADR-006 (unit of work), ADR-010 (interceptor and redaction pattern).

## Amendment 2026-11-26 (D-041)

The delivery result write is fenced by a per-claim random token (`claim_token`), not by the attempt number: redeliver restarts the counter, so a stale worker could match a newer attempt. An expired lease on the final attempt dead-letters the delivery; a queued delivery whose lease cannot cover another attempt is released unsent. The Development sink requires the event id and type headers to match the signed body.
