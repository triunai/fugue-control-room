# 04 - Customers API walk-through

Goal: exercise the Customers endpoints by hand and see ETag / `If-Match`, 409, 412 and 428 for yourself, then run the same flows with Bruno. Needs runbook 03 (database up, migrated, API running with `--seed` on port 5080).

> **Auth required.** Every endpoint except `POST /api/v1/auth/token` and `GET /api/v1/diagnostics/ping` needs a bearer token. Get one first (runbook 05) and use the staff or admin user for this walk-through: self-service users can only see and edit their own customer. Customers answer **204 No Content with a new `ETag` header** to `PUT`, `suspend` and `reactivate`; `GET` the customer to read the result.

Set variables once (token from runbook 05, section 1):
```bash
B=http://localhost:5080/api/v1/customers
TOKEN=$(login admin@fugue.test | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')   # `login` is defined in runbook 05, section 1
H="Authorization: Bearer $TOKEN"
```
Then add `-H "$H"` to every `curl` below.

## 1. Create (201, Location, ETag)

```bash
curl -si -X POST $B -H 'Content-Type: application/json' \
  -d '{"name":"Ada Lovelace","email":"Ada@Example.Test","phone":"+14155550100"}'
```
Expect: `HTTP/1.1 201 Created`, `Location: http://localhost:5080/api/v1/customers/<id>`, `ETag: "<number>"`, and a body whose `email` is lower-cased (`ada@example.test`), `status` is `Active`, `createdBy` is the signed-in user's id, `version` equals the ETag number.

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

## 3. Update with the right If-Match (204, new ETag)

```bash
curl -si -X PUT $B/$ID -H 'Content-Type: application/json' -H "If-Match: $ETAG" \
  -d '{"name":"Grace B. Hopper","email":"grace@example.test","phone":null}' | grep -iE '^HTTP|^etag'
```
Expect: `204 No Content` and a **different** `ETag` header (no body; `GET` it to see the change). Keep the old one (`OLD=$ETAG`) and store the new one in `ETAG`.

## 4. Update with the stale tag (412)

```bash
curl -si -X PUT $B/$ID -H 'Content-Type: application/json' -H "If-Match: $OLD" \
  -d '{"name":"Lost update","email":"grace@example.test","phone":null}'
```
Expect: `412 Precondition Failed`, `Content-Type: application/problem+json`, `"code":"customer.version_mismatch"`. Re-read the customer, take the new ETag, retry.

## 5. Update or delete without If-Match (428)

```bash
curl -si -X PUT $B/$ID -H 'Content-Type: application/json' -d '{"name":"x","email":"grace@example.test"}'
curl -si -X DELETE $B/$ID
```
Expect: `428 Precondition Required`, `"code":"customer.if_match_required"`.

Suspend and reactivate do not require `If-Match` (the state machine already guards them); if you send one it is enforced.

## 6. Suspend, suspend again (409), reactivate

```bash
curl -si -X POST $B/$ID/suspend | grep -iE '^HTTP|^etag'
curl -si -X POST $B/$ID/suspend
curl -si -X POST $B/$ID/reactivate | grep -iE '^HTTP|^etag'
```
Expect: first `204` with a new ETag (a `GET` now shows `Suspended`). The second suspend is `409 Conflict`, `"code":"customer.invalid_transition"`. Reactivate is `204` (a `GET` shows `Active`).

## 7. Duplicate email (409)

```bash
curl -si -X POST $B -H 'Content-Type: application/json' -d '{"name":"Other","email":"GRACE@example.test"}'
```
Expect: `409 Conflict`, `"code":"customer.email_taken"` (emails are unique case-insensitively).

## 8. Keyset paging

```bash
curl -si "$B?limit=5&search=sample" | grep -iE '^HTTP|^x-next-cursor|^link'
```
Expect: `200`, an `X-Next-Cursor` header and `Link: <http://localhost:5080/api/v1/customers?limit=5&search=sample&cursor=...>; rel="next"`. Follow the `Link` URL for the next page; the last page has neither header. `limit` is 1 to 100 (default 20); `status=active|suspended` and `search` (name or email, wildcards are escaped) are optional filters. Cursors are opaque; do not build them.

## 9. Malformed id (400)

```bash
curl -si $B/not-a-guid
```
Expect: `400 Bad Request`, `Content-Type: application/problem+json`, `"code":"validation.failed"`, and `traceId` plus `correlationId` in the body.

## 10. Delete (204)

```bash
curl -si -X DELETE $B/$ID -H "If-Match: $ETAG" | head -1
```
Expect: `204 No Content`. A following `GET` is `404` with `"code":"customer.not_found"`.

## Run the same flows with Bruno

With the API running on port 5080 (runbook 03, step 4):
```bash
npm install
npm run api:test
```
`api:test` runs `bru run --env local` from `bruno/fugue/` (environment file `environments/local.bru`, `baseUrl=http://localhost:5080`). A collection-level pre-request script signs in once as the dev admin and reuses the token; requests run in order and pass the customer id and ETag along as variables. The `auth/` folder covers login, the uniform 401, 403 and the self-service ownership 404. Every assertion is green (no `MapStatus` exceptions remain). Open the collection in the Bruno desktop app via "Open Collection" on `bruno/fugue`.
