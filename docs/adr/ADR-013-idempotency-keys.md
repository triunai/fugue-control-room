# ADR-013 — Idempotency keys with stored-response replay

- Status: accepted
- Date: 2026-11-27
- Decision entry: D-050 in `docs/decisions.md`

## Context
Creates are the only writes in Fugue that are not safe to repeat: PUT, DELETE and the state-transition POSTs already carry `If-Match`, so a replayed one is refused with 412. A client whose `POST /customers` timed out cannot tell whether the row exists; retrying makes a duplicate (or, for customers, a confusing 409), and retrying a webhook-subscription create makes a second subscription with a second secret.

## Decision
An opt-in `[Idempotent]` action filter on the two create POSTs. An optional `Idempotency-Key` header is scoped to (user, method + route template, key) and fingerprinted by the SHA-256 of the request body. The filter claims the key with a committed `pending` row before the action runs; the first 2xx response (status, `Location`, `ETag`, `Content-Type`, body) is stored encrypted with Data Protection and replayed with `Idempotent-Replayed: true` for 24 hours. A different body under the same key is 422 `idempotency.key_reused`; a concurrent duplicate is 409 `idempotency.in_progress` with `Retry-After`; a non-2xx result frees the key. Full detail: `docs/plans/w007-idempotency.md`.

## Alternatives considered
* **Middleware for every POST**: runs before routing and authorization, so it would store responses for requests that are then refused, and it cannot see route templates; opt-in per action keeps the surface explicit.
* **Natural keys only (unique email)**: covers customers, not webhook subscriptions, and turns a retry into an error instead of the original answer.
* **Store the key in the handler's unit of work**: closes the crash window described under Consequences, but moves an HTTP concern (headers, status, body) into `Fugue.Application`, which never references ASP.NET Core.
* **Plaintext response storage**: the webhook create response carries a one-time signing secret.

## Consequences
* One crash window remains: the action commits, then the process dies before the row is marked completed; after `LockSeconds` a retry re-executes. Customers are protected by the unique email; a duplicate webhook subscription is possible and visible in the list.
* Replays skip the handler, so they write no second audit row and emit no second outbox event: tests pin both.
* The OpenAPI snapshot changes (header parameter, 409/422 problems) and the console regenerates its client.

## Supersedes
none.
