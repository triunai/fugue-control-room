# ADR-013 — Idempotency keys with stored-response replay

- Status: accepted
- Date: 2026-11-27
- Decision entry: D-050 in `docs/decisions.md`

## Context
Creates are the only writes in Fugue that are not safe to repeat: PUT, DELETE and the suspend, reactivate and rotate-secret POSTs carry `If-Match`, so a replayed one is refused with 412, and webhook redelivery only re-queues a dead delivery (409 otherwise). A client whose `POST /customers` timed out cannot tell whether the row exists; retrying makes a duplicate (or, for customers, a confusing 409), and retrying a webhook-subscription create makes a second subscription with a second secret.

## Decision
An opt-in `[Idempotent]` resource filter on the two create POSTs (it runs after authorization, before model binding, and around result execution). An optional `Idempotency-Key` header (a Structured Field String) is scoped to (user, method + route template, key) and fingerprinted by the SHA-256 of the request body. The filter claims the key atomically with a committed `pending` row and a claim token before the action runs; the first 2xx response (status, `Location`, `ETag`, `Content-Type`, body) is stored encrypted with Data Protection and replayed with `Idempotent-Replayed: true` for 24 hours. A different body under the same key is 422 `idempotency.key_reused`; a concurrent duplicate is 409 `idempotency.in_progress` with `Retry-After`; a business 4xx frees the key; anything ambiguous (5xx, abort, a storage failure) leaves it pending, later answered 409 `idempotency.outcome_unknown`. A claimed key is never reclaimed, so an ambiguous outcome never runs twice. Full detail: `docs/plans/w007-idempotency.md`.

## Alternatives considered
* **Middleware after routing and authorization**: workable, but it must re-derive which endpoints opt in from metadata and sits outside MVC's result execution; a resource filter gets the opt-in, the route template and the executed result directly.
* **An action filter**: runs after model binding (the body is consumed) and before the result executes (`Location` and the serialized body do not exist yet). Rejected by the plan review.
* **Reclaiming a stale pending key**: needs ownership fencing against the slow original and can run a create twice; answering `outcome_unknown` is honest and cannot duplicate.
* **Natural keys only (unique email)**: covers customers, not webhook subscriptions, and turns a retry into an error instead of the original answer.
* **Store the key in the handler's unit of work**: closes the crash window described under Consequences, but moves an HTTP concern (headers, status, body) into `Fugue.Application`, which never references ASP.NET Core.
* **Plaintext response storage**: the webhook create response carries a one-time signing secret.

## Consequences
* One crash window remains: the action commits, then the process dies before the row is marked completed. The key then answers `outcome_unknown` until it expires; the client checks whether the resource exists and uses a new key. No duplicate is created.
* The Data Protection key ring sits unencrypted in the same database, so encryption protects the stored bodies against a table, log or partial-export leak, not a full dump; wrapping the key ring is deferred.
* Replays skip the handler, so they write no second audit row and emit no second outbox event: tests pin both.
* The OpenAPI snapshot changes (header parameter, 409/422 problems) and the console regenerates its client.

## Supersedes
none.

## Amendment 2026-11-27 (D-056): claim submit requires the key
`POST /api/v1/claims` opts into a required key: without `Idempotency-Key` it answers 400 `idempotency.key_required`. A duplicate claim is real harm (two officers, a double payment risk), so the server stops treating the key as a client courtesy on that one route. Customer and webhook-subscription creates keep the optional key described above. Source: Codex claims seam review finding 5.
