# 12 - Kafka: watch claim events on a topic

Goal: run a single-node broker next to the stack, make a claim move, and watch its events arrive on `fugue.claims.v1` keyed by the claim id. Needs runbooks 03 and 05 (database, signing key) and 11 (claims). The design is the Kafka amendment in `docs/adr/ADR-011-outbox-and-signed-webhooks.md`.

How it flows: the outbox relay (ADR-011) calls every registered `IEventPublisher` per message. `Events:Publisher` picks them: `Webhooks` (default, nothing changes), `Kafka`, or `Both`. The Kafka adapter produces to a topic per event family (`fugue.claims.v1`, `fugue.customers.v1`, else `fugue.events.v1`), key = aggregate id (the claim id, so one claim's events stay in order), value = the same JSON envelope the webhooks sign, headers `event-type`, `event-id`, `correlation-id`, `saga-id`, `traceparent`. The relay waits for the broker acknowledgement before it marks the outbox row dispatched.

## 1. Start the broker and the API

Whole stack in Docker (the broker has the `kafka` profile, so a plain `docker compose up` is unchanged):
```bash
EVENTS_PUBLISHER=Both docker compose --profile kafka up -d --build
```
Or the broker alone and the API on the host (runbook 03 for the database):
```bash
docker compose --profile kafka up -d redpanda          # broker on localhost:${KAFKA_PORT:-19092}
set -a; source .env; set +a
ASPNETCORE_ENVIRONMENT=Development Events__Publisher=Both Events__Kafka__BootstrapServers=localhost:19092 \
  dotnet run --project src/Fugue.Api -- --seed
```
`Kafka` publishes to the topic only; `Both` also fans out to webhook subscriptions. The host refuses to start with `Kafka` or `Both` and no `Events:Kafka:BootstrapServers`.

## 2. Move a claim, then watch the topic

Submit and pick up a claim as in runbook 11 section 2, then:
```bash
docker exec fugue-redpanda rpk topic consume fugue.claims.v1 --offset start -f 'key=%k\nheaders=%h{%k=%v;}\nvalue=%v\n---\n'
```
Expect one record per event (`claim.submitted.v1`, `claim.assigned.v1`, ...), every record of a claim with the same key, and `event-id` equal to the outbox message id. Consumers dedupe on `event-id`: delivery is at least once.

## 3. Break the broker on purpose

```bash
docker stop fugue-redpanda
```
Move a claim. Its `outbox_messages` row stays `pending`, `attempts` climbs with backoff, and `last_error` names the failed produce (`SELECT status, attempts, last_error FROM outbox_messages ORDER BY occurred_at DESC LIMIT 3;`). `docker start fugue-redpanda`: the next retry publishes it and marks it `dispatched`. After `Webhooks:MaxAttempts` failures a message is `dead` (the same dead-letter rule as webhooks).

## Clean up

`docker compose --profile kafka down` stops the broker with the rest. The broker keeps no volume: topics vanish with the container.
