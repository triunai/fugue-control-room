# 06 - Audit log: see who changed what, and try to tamper with it

Goal: make a change, find its audit row over HTTP and in psql, read the field-level diff, see a failure row, and watch the database refuse an UPDATE. Needs runbooks 03 (database up, migrated) and 05 (signing key, dev users). The design is `docs/adr/ADR-010-audit-log.md`.

Which table answers which question:

| Table | Holds | Ask it |
|---|---|---|
| `audit_log` | data changes (field-level) and failed commands | who changed this row, what was the old value, which request |
| `access_audit` | sign-ins, refresh, logout, 401s, 403s, disabled modules | who tried to get in, who was refused |
| `grant_audit` | role, group and override grants and revokes | who gave this user that permission |

## 1. Start the API and sign in as admin

```bash
set -a; source .env; set +a
ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/Fugue.Api -- --seed
```
In another terminal:
```bash
PW='Fugue-Dev-Passw0rd!'  # secrets-scan:ignore
TOKEN=$(curl -s -X POST localhost:5080/api/v1/auth/token -H 'Content-Type: application/json' \
  -d "{\"email\":\"admin@fugue.test\",\"password\":\"$PW\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')
```
Only `ADMIN` holds `AUDIT_READ`. `staff@fugue.test` and `self@fugue.test` get `403`.

## 2. Make a change under a known correlation id

```bash
CID="demo-$(date +%s)"
curl -s -X POST localhost:5080/api/v1/customers -H "Authorization: Bearer $TOKEN" -H "X-Correlation-Id: $CID" \
  -H 'Content-Type: application/json' -d '{"name":"Ada Lovelace","email":"ada.audit@example.test","phone":"+14155550123"}' -D - -o /dev/null | grep -i "^etag\|^location"
```
Expect: `201`, an `ETag` and a `Location` ending in the new customer id. Keep both: `ID=<the id>` and `ETAG='"<the etag>"'`.

## 3. Find the row over HTTP

```bash
curl -s "localhost:5080/api/v1/audit?correlationId=$CID" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect one row: `action` `customers.create`, `entityType` `Customer`, `entityId` the id, `outcome` `success`, `actorDisplay` `Dev Admin`, `permissionUsed` `CUSTOMERS_INSERT`, `httpMethod` `POST`, `routeTemplate` `api/v1/customers`, a `traceId`, and `changes` with a `new` value per field. The email shows as `a***@example.test` and the phone as `***23`: personal data is masked BEFORE it is stored, so no query can reveal it.

Change one field and read the diff:
```bash
curl -s -X PUT localhost:5080/api/v1/customers/$ID -H "Authorization: Bearer $TOKEN" -H "If-Match: $ETAG" -H 'Content-Type: application/json' \
  -d '{"name":"Ada King","email":"ada.audit@example.test","phone":"+14155550123"}' -o /dev/null -w "%{http_code}\n"
curl -s "localhost:5080/api/v1/audit?entityType=Customer&entityId=$ID&action=customers.update" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect `204`, then one row whose `changes` is only `{"name": {"old": "Ada Lovelace", "new": "Ada King"}}` (the email and phone were resubmitted unchanged, so they are not in the diff).

Filters (all exact match): `entityType`, `entityId`, `actorUserId`, `action`, `correlationId`, `from` (inclusive), `to` (exclusive), `limit` (1 to 200, default 50). Newest first. Page with the cursor:
```bash
curl -s -D - "localhost:5080/api/v1/audit?limit=2" -H "Authorization: Bearer $TOKEN" -o /dev/null | grep -i "x-next-cursor\|^link"
curl -s "localhost:5080/api/v1/audit?limit=2&cursor=<the X-Next-Cursor value>" -H "Authorization: Bearer $TOKEN"
```

## 4. A failed command leaves one failure row

Repeat the create with the same email (a duplicate):
```bash
CID2="demo-fail-$(date +%s)"
curl -s -X POST localhost:5080/api/v1/customers -H "Authorization: Bearer $TOKEN" -H "X-Correlation-Id: $CID2" \
  -H 'Content-Type: application/json' -d '{"name":"Dup","email":"ada.audit@example.test"}' -o /dev/null -w "%{http_code}\n"
curl -s "localhost:5080/api/v1/audit?correlationId=$CID2" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect `409`, then one row with `outcome` `failure`, `errorCode` `customer.email_taken`, `changes` null. A validation error (`400`) or a stale `If-Match` (`412`) leaves a row the same way. The row also carries `errorType` (`Conflict`), `errorMessage` (an audit-safe summary, capped at 2000 characters: for a validation failure `invalid: ` plus the sorted field NAMES, never the submitted values; null for other business errors, whose `errorCode` is the identifier, because handler messages can interpolate request values) and `durationMs`. These three are null on success rows. For an unhandled exception `errorType` is `Unhandled` and `errorMessage` is the exception TYPE name only, never the exception's text (it can carry SQL values or personal data). Sign-in failures are NOT here: they are in `access_audit`.

## 5. The same data in psql

```bash
docker exec -it fugue-postgres psql -U fugue -d fugue
```
```sql
SELECT occurred_at, action, outcome, error_code, entity_type, entity_id, actor_display, changes
FROM audit_log ORDER BY occurred_at DESC, id DESC LIMIT 5;

SELECT changes->'name'->>'new' AS new_name FROM audit_log WHERE entity_type = 'Customer' AND action = 'customers.update';
SELECT action, count(*) FROM audit_log WHERE outcome = 'failure' GROUP BY action ORDER BY 2 DESC;
SELECT * FROM audit_log WHERE correlation_id = 'demo-...';   -- every row of one request
```

## 6. Try to tamper with it (this must fail)

```sql
UPDATE audit_log SET action = 'tampered';
DELETE FROM audit_log;
TRUNCATE audit_log;
```
Expect each to stop with `ERROR:  audit_log is append-only: UPDATE is not allowed` (or `DELETE`, `TRUNCATE`; SQLSTATE `23001`). The rows are untouched. What the trigger does NOT stop: the table owner or a superuser running `ALTER TABLE audit_log DISABLE TRIGGER ...` or `DROP TABLE`. In production run the API as a role that does not own the table.

## 7. Knobs (`appsettings.json`, section `Audit`, or `Audit__...` environment variables)

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` stops recording; the host refuses to start with it off outside Development |
| `MaskedProperties` | `[]` | extra property names whose values become `***` (built in: `PasswordHash`, `TokenHash`, `SigningKey`) |
| `PartialMaskPersonalData` | `true` | `Email`, `Phone`, `ContactNumber` become `a***@example.test` / `***23` |
| `PartialMaskedProperties` | `[]` | extra names to partially mask |
| `ExcludedEntities` | `[]` | CLR entity names that are not audited (built in: the audit tables, `RefreshToken`) |
| `MaxChangesBytes` | `16384` | past it, trailing fields are dropped and `"_truncated": true` is added |
| `CaptureClientIp` | `true` | `false` stores no client IP |
| `UnauthenticatedRowsPerIpPerMinute` | `30` | cap on `access_audit` rows for 401 challenges per client IP bucket per minute (`0` records none); past it the 401 is still returned and `fugue.audit.write_failures` counts `reason=suppressed` |
| `UnauthenticatedBuckets` | `4096` | how many fixed buckets the 401 limiter hashes client IPs into; memory is proportional to this, not to the number of distinct addresses (at least `1`) |

On code: `[AuditMask]` on an entity property masks it, `[NoAudit]` on a property omits it, on an entity class excludes the entity, on a command skips the failure row (only the authentication commands use it). Rows are never sampled.

## 401s in `access_audit`

A request that is refused with `401` (no token, an expired token, any other invalid token) leaves one `access_audit` row: `event` `unauthenticated`, `outcome` `denied`, `reason_code` `missing_token`, `expired` or `invalid_token`, no user and no email. The client sees the same `401` problem and `WWW-Authenticate: Bearer` whatever the reason; the token itself is never stored. The rows are bounded per client IP (`UnauthenticatedRowsPerIpPerMinute`, a fixed one-minute window in a fixed array of `UnauthenticatedBuckets` buckets keyed by a hash of the connection address, never a forwarding header; two addresses that collide share a bucket, which can only drop rows, never add them), because an anonymous caller can otherwise write a row per request. Excess requests are answered normally and counted, not stored. Find them with `SELECT occurred_at, reason_code, ip, correlation_id FROM access_audit WHERE event = 'unauthenticated' ORDER BY occurred_at DESC LIMIT 20;`.

## What is and is not audited

Audited: every EF `SaveChanges` on a non-excluded entity (so users, roles, groups, overrides, modules, customers), the Dapper customer insert, the seeders (one row per customer created or overwritten, nothing on a re-run), forced "last writer wins" writes (flagged `unconditionalWrite` in `metadata`), and every failed mutating command. Not audited: reads, refresh-token rows (their record is `access_audit`), and the audit tables themselves. Audit rows are not sampled and there is no retention yet (`docs/archive/deferred.md`).

## When an audit row could not be written

A failing failure-row write never changes the command's result, so it would otherwise be silent. Two signals say it happened:

- A counter `fugue.audit.write_failures` on the meter `Fugue.Audit` (no extra package; watch it with `dotnet-counters monitor --counters Fugue.Audit` or any OpenTelemetry meter subscription). Tags: `table` (`audit_log` or `access_audit`) and `reason` (the exception type name; `suppressed` for a 401 row dropped by the per-IP cap; `canceled` for a 401 row lost because the audit write was canceled while the request was still live).
- A Warning log "Lost a failure audit row" carrying the action, entity type, entity id, error code and correlation id of the lost row (never its changes or metadata), so the row can be rebuilt from the logs.

## Search

Typo-tolerant, ranked, highlighted search over `audit_log` (D-058). It is optional: the table stays the source of truth and the index is a rebuildable copy.

**Turn it on.** The compose stack runs Meilisearch (`getmeili/meilisearch:v1.12.8`, service `meilisearch`, volume `fugue-meili`, port `MEILI_PORT`, default 7700) and points the API at it. Put a key of at least 16 bytes in `.env` first: `MEILI_MASTER_KEY=$(openssl rand -base64 24)`. For a host-run API (`dotnet run`) set `Search__Meilisearch__Url=http://localhost:7700` and `Search__Meilisearch__ApiKey=<the same key>`. With no URL, search is off: `GET /api/v1/audit/search` answers `503` with problem code `search.unavailable` and nothing else changes. An engine that is configured but unreachable answers the same `503`.

**How rows get there.** A background indexer (runs once at startup, then every `Search:Meilisearch:IndexerIntervalSeconds`, default 5) reads `audit_log` oldest first in keyset batches of `IndexerBatchSize` (default 500) after its checkpoint, upserts them into the `audit` index by row id, waits for the engine to apply the batch, then moves the checkpoint. The checkpoint is one document in the engine's own `audit_checkpoint` index, so there is no migration and no extra table. A crash between the upsert and the checkpoint repeats a batch; upserts are idempotent, so nothing is duplicated or skipped. Search is therefore eventually consistent: a row appears after the next cycle. What is indexed is what the row stores (masked values stay masked): action, entity type and id, actor display and id, outcome, error code and message, route, time. `changes` and `metadata` are not indexed.

**Search.** Needs `AUDIT_READ`, like the list.
```bash
curl -s "localhost:5080/api/v1/audit/search?q=email_taken&outcome=failure&limit=5" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Parameters: `q` (text, matched on action, entity type and id, actor display, error code and message, route; empty returns the newest rows), `entityType`, `action`, `outcome` (exact), `from` (inclusive), `to` (exclusive), `limit` (1 to 100, default 20). Response: `{ "hits": [...], "estimatedTotalHits": N }`, newest first; each hit has `highlights` (field to text with matches in `<em>`; the rest of the text is not HTML-escaped, so a console must escape everything but the `em` tags). `estimatedTotalHits` is the engine's estimate, not a count.

**Rebuild.** Delete the two indexes (`DELETE /indexes/audit` and `/indexes/audit_checkpoint` with the master key, or remove the `fugue-meili` volume) and restart the API; the indexer re-reads the whole table.

**Check it.**
```bash
curl -s localhost:7700/health                                                              # {"status":"available"}
curl -s localhost:7700/indexes/audit/stats -H "Authorization: Bearer $MEILI_MASTER_KEY"    # numberOfDocuments
```
