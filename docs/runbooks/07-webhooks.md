# 07 - Webhooks: receive a signed event, verify it, force a dead letter, redeliver

Goal: subscribe the Development sink, change a customer, watch the signed event arrive with its correlation and saga ids, verify the signature by hand, then break a receiver on purpose and redeliver. Needs runbooks 03 (database up, migrated) and 05 (signing key, dev users). The design is `docs/adr/ADR-011-outbox-and-signed-webhooks.md`.

How it flows: a customer change writes an `outbox_messages` row in the SAME transaction. A background dispatcher relays each message to one `webhook_deliveries` row per matching subscription, then sends them (HTTP outside any transaction), signed with the subscription's secret. Delivery is at least once: receivers verify, reject old timestamps and dedupe on the event id.

## 1. Start the API with local destinations allowed

Webhooks go to `https` public hosts only. To send to the sink on this machine, run in Development with the local switch (the host refuses to start with it on anywhere else). The short backoff and attempt cap make section 6 quick:
```bash
set -a; source .env; set +a
ASPNETCORE_ENVIRONMENT=Development Webhooks__AllowInsecureLocal=true Webhooks__MaxAttempts=3 Webhooks__BaseDelaySeconds=2 \
  ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/Fugue.Api -- --seed
```
With `--seed` the sink is already subscribed to `claim.*` (subscription `dev sink (claim events)`, secret handed to the sink at startup; see runbook 11 section 5), so claim moves show up in the sink with no steps from section 2; sections 2 onward subscribe it to `customer.*` by hand.

In another terminal sign in as admin (`WEBHOOKS_MANAGE` is admin only):
```bash
PW='Fugue-Dev-Passw0rd!'  # secrets-scan:ignore
TOKEN=$(curl -s -X POST localhost:5080/api/v1/auth/token -H 'Content-Type: application/json' \
  -d "{\"email\":\"admin@fugue.test\",\"password\":\"$PW\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')
```

## 2. Subscribe the sink

```bash
SUB=$(curl -s -X POST localhost:5080/api/v1/webhooks/subscriptions -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"dev sink","url":"http://localhost:5080/api/v1/dev/webhook-sink","eventTypes":["customer.*"]}')
SECRET=$(echo "$SUB" | python3 -c 'import sys,json;print(json.load(sys.stdin)["secret"])')
curl -s -X PUT localhost:5080/api/v1/dev/webhook-sink/secret -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"secret\":\"$SECRET\"}" -w "%{http_code}\n"
```
Expect: the subscription comes back with a `whsec_...` secret (the ONLY time it is shown; it is stored encrypted) and the second call prints `204`. The sink is an anonymous endpoint that exists only in Development: its credential is the signature, and it trusts the one secret you just gave it. Filters are exact (`customer.created.v1`), prefix (`customer.*`) or `*`.

## 3. Change a customer and watch the event arrive

```bash
CID="hook-$(date +%s)"
curl -s -X POST localhost:5080/api/v1/customers -H "Authorization: Bearer $TOKEN" -H "X-Correlation-Id: $CID" \
  -H 'Content-Type: application/json' -d '{"name":"Ada Lovelace","email":"ada.hook@example.test","phone":"+14155550123"}' -o /dev/null -w "%{http_code}\n"
sleep 3
curl -s localhost:5080/api/v1/dev/webhook-sink -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect `201`, then one received event: `eventType` `customer.created.v1`, `correlationId` equal to `$CID`, `sagaId` equal to the customer id (the saga defaults to the aggregate id), a `traceparent`, and a `body` whose `data` is the thin, masked payload (`a***@example.test`, `***23`). Receivers fetch the resource by id for anything else. The headers a receiver sees: `X-Fugue-Signature`, `X-Fugue-Timestamp`, `X-Fugue-Event-Id`, `X-Fugue-Event-Type`, `X-Correlation-Id`, `X-Saga-Id`, `traceparent`.

The admin view of the same delivery:
```bash
curl -s "localhost:5080/api/v1/webhooks/deliveries?limit=5" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect `status` `succeeded`, `attempts` 1, `lastStatusCode` 200. No body or header is stored with a delivery, only the clipped error.

## 4. Verify a signature yourself

The signature is `v1=` plus the hex HMAC-SHA256, keyed with the secret string, over `"{timestamp}.{raw body}"`. Take the timestamp and body from the sink's answer:
```bash
EVENT=$(curl -s localhost:5080/api/v1/dev/webhook-sink -H "Authorization: Bearer $TOKEN" \
  | python3 -c 'import sys,json;e=json.load(sys.stdin)[0];print(e["timestamp"]);print(e["body"],end="")')
TS=$(echo "$EVENT" | head -1); BODY=$(echo "$EVENT" | tail -n +2)
printf '%s.%s' "$TS" "$BODY" | openssl dgst -sha256 -hmac "$SECRET" | sed 's/^.* /v1=/'
```
Expect a `v1=<64 hex>` string; it is what the receiver compared with the `X-Fugue-Signature` header. The same in C# (a receiver must compare in constant time and reject a timestamp older than five minutes):
```csharp
using System.Security.Cryptography;
using System.Text;

static bool Verify(string secret, string timestamp, byte[] rawBody, string signatureHeader)
{
    byte[] message = [.. Encoding.UTF8.GetBytes(timestamp + "."), .. rawBody];
    byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message);
    string header = signatureHeader.StartsWith("v1=", StringComparison.Ordinal) ? signatureHeader[3..] : "";
    return header.Length == 64 && CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(header));
}
```
Always hash the RAW bytes you received, never a re-serialised object. Changing one character of the body, or the secret, gives a different value.

## 5. Replay and duplicates

Send the same event twice to the sink and the second answer is `duplicate` (it dedupes on the event id), the behaviour a real receiver needs because delivery is at least once. `curl -X DELETE localhost:5080/api/v1/dev/webhook-sink -H "Authorization: Bearer $TOKEN"` forgets everything for a repeatable demo.

## 6. Force a dead letter and redeliver

Point a subscription at a port nothing listens on, then create a customer:
```bash
curl -s -X POST localhost:5080/api/v1/webhooks/subscriptions -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"broken","url":"http://127.0.0.1:9/hook","eventTypes":["customer.created.v1"]}' -o /dev/null -w "%{http_code}\n"
curl -s -X POST localhost:5080/api/v1/customers -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Dead Letter","email":"dead.hook@example.test","phone":null}' -o /dev/null -w "%{http_code}\n"
sleep 15
curl -s "localhost:5080/api/v1/webhooks/deliveries?status=dead" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect `attempts` 3 (the `MaxAttempts` you set), `status` `dead`, `deadLetteredAt` set and a clipped `lastError`; the backoff between attempts doubled with jitter. Fix the receiver (here: set the subscription active against the sink with a PUT carrying the `If-Match` from the create, or just redeliver after fixing the URL) and revive the delivery:
```bash
DELIVERY=<the id from the list>
curl -s -X POST localhost:5080/api/v1/webhooks/deliveries/$DELIVERY/redeliver -H "Authorization: Bearer $TOKEN" -w "%{http_code}\n"
```
Expect `204`; the delivery goes back to `pending` with a fresh attempt budget and the dispatcher sends it again. Redeliver of a delivery that is not dead answers `409`. Every redeliver writes an `audit_log` row (runbook 06).

## 7. Look at it in psql

```bash
docker exec -it fugue-postgres psql -U fugue -d fugue -c "select event_type, status, attempts, correlation_id, saga_id from outbox_messages order by occurred_at desc limit 5;"
docker exec -it fugue-postgres psql -U fugue -d fugue -c "select status, attempts, last_status_code, next_attempt_at from webhook_deliveries order by occurred_at desc limit 5;"
```
Try `update outbox_messages set payload = '{}'` on a row: the trigger refuses it (the payload is immutable; status columns stay writable).

## 8. Retry a subscription create safely (Idempotency-Key)

`POST /webhooks/subscriptions` accepts the optional `Idempotency-Key` header (ADR-013; the rules and error codes are in runbook 04, section 11). The secret is shown only once, so a retry matters here: with the same key and body the retry returns the SAME subscription and the SAME secret instead of creating a second subscription.

```bash
KEY="hook-$(date +%s)"
BODY='{"name":"retry hook","url":"http://localhost:5080/api/v1/dev/webhook-sink","eventTypes":["customer.*"]}'
for i in 1 2; do curl -si -X POST localhost:5080/api/v1/webhooks/subscriptions -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -H "Idempotency-Key: \"$KEY\"" -d "$BODY" | grep -iE '^HTTP|^idempotent-replayed|^cache-control|"secret"'; done
```
Expect: two `201` answers with the same `whsec_...` secret; the second has `Idempotent-Replayed: true`. Responses to a keyed create carry `Cache-Control: no-store`. The stored copy is encrypted with Data Protection (the key ring sits unencrypted in the same database, so this protects against a leaked table or log, not a full database dump). Delete the subscription afterwards (section 2 shows the id).

## If it does not work

| Symptom | Cause | Fix |
|---|---|---|
| The host will not start, `AllowInsecureLocal can only be true in the Development environment` | The local switch with another environment | Set `ASPNETCORE_ENVIRONMENT=Development` |
| `400` creating a subscription to `http://localhost` | Local destinations are blocked | `Webhooks__AllowInsecureLocal=true` (Development only) |
| Delivery stays `pending` with `lastStatusCode` 503 | The sink has no secret yet, so it will not guess | Section 2, second call |
| Delivery `pending` with a 401 | The sink trusts another secret (a second subscription, or a rotate) | Put the current secret into the sink |
| Nothing ever arrives | The module is off, or `Webhooks:Dispatcher:Enabled` is false | `GET /api/v1/modules`; check the setting |
| Docker or the test run freezes | See failure mode 23 | Run test classes separately, keep one container per fixture |
