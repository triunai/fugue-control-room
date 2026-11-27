# W-007 plan: Idempotency-Key store with replay

Critical seam (idempotency, migration, a filter in front of `ApiControllerBase` actions): plan first, cross-family review after. Decision: D-050, promoted to ADR-013.

## Goal
A client that retries `POST /api/v1/customers` or `POST /api/v1/webhooks/subscriptions` with the same `Idempotency-Key` gets the first response back (same status, body, `Location`, `ETag`) instead of a second row or a 409, and a client that reuses a key for a different request is told so. Shape follows the IETF `Idempotency-Key` header draft (draft-ietf-httpapi-idempotency-key-header).

## Decisions
| Id | Decision |
|---|---|
| A | Opt-in per action with `[Idempotent]` (an MVC async action filter in `Fugue.Api`), applied to the two create POSTs only. The header is OPTIONAL: absent means today's behaviour. Present: 1-255 printable ASCII characters, else 400 `idempotency.key_invalid`. |
| B | Scope = (actor, endpoint, key): actor is the authenticated user id; endpoint is `METHOD + route template` (`POST api/v1/customers`). Two users can use the same key. The filter runs after authentication and authorization (an action filter), so an unauthorized request never touches the store. |
| C | Fingerprint = SHA-256 of the raw request body bytes (buffered; the body is small and JSON). Same scope + different fingerprint -> 422 `idempotency.key_reused` (problem+json). |
| D | Claim before execute: `INSERT ... ON CONFLICT DO NOTHING` a `pending` row (Dapper via `SqlExecutor`, its own short transaction, committed BEFORE the action runs). If the claim loses: row `completed` -> replay; row `pending` and younger than `LockSeconds` (default 30) -> 409 `idempotency.in_progress` with `Retry-After: 1`; row `pending` and older -> reclaim (update to a fresh pending under the same fingerprint) and execute. |
| E | After the action: a 2xx result is serialized exactly as it would be sent (status, `Location`, `ETag`, `Content-Type`, body) and the row is set `completed`. Any non-2xx result or an exception DELETES the pending row, so the client may fix the request and retry with the same key (validation errors are not cached). |
| F | Replay sends the stored status, headers and body, plus `Idempotent-Replayed: true`. The handler, audit and outbox do not run again (no second audit row, no second webhook event). |
| G | Stored bodies are encrypted with Data Protection (purpose `Fugue.Idempotency.v1`; key ring already persisted in Postgres by S4). The webhook create response carries the one-time signing secret, so the plaintext must never sit in a table; encrypting every body keeps one code path. |
| H | Table `idempotency_keys`: `id` uuid pk, `actor_id` text, `endpoint` text, `key` text, `request_hash` bytea, `status` text CHECK (`pending`,`completed`), `response_status` int null, `response_headers` jsonb null, `response_body` bytea null, `created_at`, `locked_at`, `completed_at` null, `expires_at`. UNIQUE (actor_id, endpoint, key); index on `expires_at`. Not `IAuditable`, excluded from `audit_log` capture (operational table, like the outbox), added to whichever fitness allowlist requires it. |
| I | Retention: `expires_at = created_at + RetentionHours` (default 24). A lookup treats an expired row as absent (it is replaced by the claim upsert). A hosted purge deletes expired rows every `PurgeIntervalMinutes` (default 60) in batches. Options section `Idempotency:` validated at start. |
| J | Known residual risk (documented in ADR-013): the action's own transaction commits before the row is marked completed. A crash in that gap leaves a pending row that a retry reclaims after `LockSeconds` and re-executes. For customers the unique email turns that into a 409; for webhook subscriptions a duplicate subscription is possible. Accepted: making the store part of the handler's unit of work would push HTTP concerns into Application. |
| K | Contract: OpenAPI shows an optional `Idempotency-Key` header parameter and the 409/422 problems on `[Idempotent]` actions (via `FugueOperationFilter`); snapshot refreshed; console client regenerated; the console sends a fresh key per create dialog submission and reuses it on retry. CORS already allows the header (ADR-012); `Idempotent-Replayed` is added to exposed headers. |

## Slices (commits, each green)
1. Schema: entity + id, EF mapping, migration, catalog row, schema pin test.
2. Store + filter + options + purge; tests: replay returns same id/status/Location and exactly one row + one audit row; different body -> 422; concurrent same key -> one 201 and one 409/replay; failed request (400/409) frees the key; expired key re-executes; other user same key is independent; stored webhook body is not plaintext (grep the bytea for the secret); missing header unchanged.
3. Contract: operation filter, snapshot, Bruno request(s), runbook section, console key header + regenerated client.

## Out of scope
Idempotency on PUT/DELETE (If-Match already makes them safe), on state-transition POSTs, a distributed lock, response streaming.
