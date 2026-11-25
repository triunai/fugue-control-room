# 04 - Customers API walk-through

Goal: exercise the Customers endpoints by hand and see ETag / `If-Match`, 409, 412 and 428 for yourself, then run the same flows with Bruno. Needs runbook 03 (database up, migrated, API running with `--seed` on port 5080).

> **Known red until the owner writes `ApiControllerBase.MapStatus`.** Every failure response from a handler (404, 409, 412, 428, handler 400) currently comes back as `500` with code `unexpected`, because `MapStatus` throws `NotImplementedException`. The steps below show the **intended** status; "Today" notes what you will see until `MapStatus` exists. Success paths and the framework 400 (a malformed id) work now.

Set a variable once:
```bash
B=http://localhost:5080/api/v1/customers
```

## 1. Create (201, Location, ETag)

```bash
curl -si -X POST $B -H 'Content-Type: application/json' \
  -d '{"name":"Ada Lovelace","email":"Ada@Example.Test","phone":"+14155550100"}'
```
Expect: `HTTP/1.1 201 Created`, `Location: http://localhost:5080/api/v1/customers/<id>`, `ETag: "<number>"`, and a body whose `email` is lower-cased (`ada@example.test`), `status` is `Active`, `createdBy` is `system` (no authenticated user yet), `version` equals the ETag number.

Save the id and the tag:
```bash
RESP=$(curl -si -X POST $B -H 'Content-Type: application/json' -d '{"name":"Grace Hopper","email":"grace@example.test"}')
ID=$(echo "$RESP" | grep -i '^location:' | sed 's#.*/##' | tr -d '\r')
ETAG=$(echo "$RESP" | grep -i '^etag:' | sed 's/^[^:]*: //' | tr -d '\r')
echo "$ID $ETAG"
```

## 2. Get (ETag present)

```bash
curl -si $B/$ID | grep -iE '^HTTP|^etag'
```
Expect: `200 OK` and the same `ETag` as at creation.

## 3. Update with the right If-Match (200, new ETag)

```bash
curl -si -X PUT $B/$ID -H 'Content-Type: application/json' -H "If-Match: $ETAG" \
  -d '{"name":"Grace B. Hopper","email":"grace@example.test","phone":null}' | grep -iE '^HTTP|^etag'
```
Expect: `200 OK` and a **different** `ETag`. Keep the old one (`OLD=$ETAG`) and store the new one in `ETAG`.

## 4. Update with the stale tag (412)

```bash
curl -si -X PUT $B/$ID -H 'Content-Type: application/json' -H "If-Match: $OLD" \
  -d '{"name":"Lost update","email":"grace@example.test","phone":null}'
```
Expect: `412 Precondition Failed`, `Content-Type: application/problem+json`, `"code":"customer.version_mismatch"`. Re-read the customer, take the new ETag, retry. Today: `500`, `"code":"unexpected"`.

## 5. Update or delete without If-Match (428)

```bash
curl -si -X PUT $B/$ID -H 'Content-Type: application/json' -d '{"name":"x","email":"grace@example.test"}'
curl -si -X DELETE $B/$ID
```
Expect: `428 Precondition Required`, `"code":"customer.if_match_required"`. Today: `500`.

Suspend and reactivate do not require `If-Match` (the state machine already guards them); if you send one it is enforced.

## 6. Suspend, suspend again (409), reactivate

```bash
curl -si -X POST $B/$ID/suspend | grep -iE '^HTTP|^etag|"status"'
curl -si -X POST $B/$ID/suspend
curl -si -X POST $B/$ID/reactivate | grep -iE '^HTTP|"status"'
```
Expect: first `200`, status `Suspended`, new ETag. The second suspend is `409 Conflict`, `"code":"customer.invalid_transition"` (today `500`). Reactivate is `200`, `Active`.

## 7. Duplicate email (409)

```bash
curl -si -X POST $B -H 'Content-Type: application/json' -d '{"name":"Other","email":"GRACE@example.test"}'
```
Expect: `409 Conflict`, `"code":"customer.email_taken"` (emails are unique case-insensitively). Today: `500`.

## 8. Keyset paging

```bash
curl -si "$B?limit=5&search=sample" | grep -iE '^HTTP|^x-next-cursor|^link'
```
Expect: `200`, an `X-Next-Cursor` header and `Link: <http://localhost:5080/api/v1/customers?limit=5&search=sample&cursor=...>; rel="next"`. Follow the `Link` URL for the next page; the last page has neither header. `limit` is 1 to 100 (default 20); `status=active|suspended` and `search` (name or email, wildcards are escaped) are optional filters. Cursors are opaque; do not build them.

## 9. Malformed id (400, works today)

```bash
curl -si $B/not-a-guid
```
Expect: `400 Bad Request`, `Content-Type: application/problem+json`, `"code":"validation.failed"`, and `traceId` plus `correlationId` in the body.

## 10. Delete (204)

```bash
curl -si -X DELETE $B/$ID -H "If-Match: $ETAG" | head -1
```
Expect: `204 No Content`. A following `GET` is `404` with `"code":"customer.not_found"` (today `500`).

## Run the same flows with Bruno

With the API running on port 5080 (runbook 03, step 4):
```bash
npm install
npm run api:test
```
`api:test` runs `bru run --env local` from `bruno/fugue/` (environment file `environments/local.bru`, `baseUrl=http://localhost:5080`). Requests run in order and pass the customer id and ETag along as variables. Expect 18 of 26 assertions green today: the 8 red ones are the status and code assertions on requests 06, 07, 09 and 13, which need `MapStatus`. Open the collection in the Bruno desktop app via "Open Collection" on `bruno/fugue`.
