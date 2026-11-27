# 11 - Claims: the platform, the accounts and a walk-through

Goal: submit a claim as a claimant, work it as an officer, decide and settle it as a manager, and see the 404, 409, 412 and 428 guards for yourself. Needs runbook 03 (database up, migrated) and runbook 05 (signing key); the API running with `--seed` on port 5080. Plan and decisions: `docs/plans/claims-window.md`.

## What it is

Motor and property claims in six markets (SG, MY, TH, HK, ID, PH; one fixed currency each). A claim moves through a state machine owned by the `Claim` aggregate; each move writes the new status, one claimant-visible timeline row, an audit row and an outbox event (`claim.*.v1`) in ONE transaction.

```
Submitted -> UnderReview (pickup assigns the officer) -> InfoRequested -> UnderReview (the claimant answers)
UnderReview -> Approved | Rejected          Approved -> Settled          Rejected and Settled are terminal
```

Rules worth knowing: approving needs a reserve and `0 < amount <= reserve`; only the assigned officer (or a caller who also holds `CLAIMS_SETTLE`) may reserve, ask or decide; a claimant sees only their own claims (anything else is 404, never 403) and never the reserve; money is bigint minor units in the market's currency and never summed across currencies.

## Accounts (password in runbook 05)

| Account | Role | Can |
|---|---|---|
| `self@fugue.test`, `self2@fugue.test` | `SELF_SERVICE` | submit, read their own, answer questions on their own. `self@` owns 3 seeded claims, one waiting for an answer |
| `staff@fugue.test`, `support@fugue.test` | `STAFF` | read all, pick up, reserve, ask, decide, workload |
| `manager@fugue.test` | `MANAGER` | staff plus settle, work any officer's claim, exposure |
| `admin@fugue.test` | `ADMIN` | everything |

## Endpoints (`/api/v1/claims`)

| Call | Permission | Notes |
|---|---|---|
| `POST /` | `CLAIMS_SUBMIT` | body `market, line, policyNumber, incidentDate, incidentDescription, incidentLocation?`; 201 + `Location` + `ETag`. Incident date: not in the future, not older than a year (market's calendar day) |
| `GET /` | `CLAIMS_READ` | keyset paged (`limit`, `cursor`; next page in `Link` and `X-Next-Cursor`). Claimant: own claims, newest first. Staff (`CLAIMS_ALL`): all claims, OLDEST first. Filters `status` (repeat or comma), `assigned=me\|none\|any`, `market`, `line`, `q` (claim or policy number) |
| `GET /{id}`, `GET /{id}/timeline` | `CLAIMS_READ` | own claim or `CLAIMS_ALL`, else 404 |
| `POST /{id}/pickup` | `CLAIMS_HANDLE` | `If-Match` required |
| `PUT /{id}/reserve` | `CLAIMS_HANDLE` | body `{amountMinor}` |
| `POST /{id}/info-requests` | `CLAIMS_HANDLE` | body `{question}` |
| `POST /{id}/info-requests/{requestId}/response` | `CLAIMS_RESPOND` | body `{answer}`; the claim must be the caller's own |
| `POST /{id}/decision` | `CLAIMS_HANDLE` | body `{outcome: approve\|reject, amountMinor?, reason}` |
| `POST /{id}/settle` | `CLAIMS_SETTLE` | Approved only |
| `GET /workload` | `CLAIMS_WORKLOAD` | per officer: open by status, oldest open age, decided in 7 and 30 days, median days to decide; unassigned; ageing bands |
| `GET /exposure` | `CLAIMS_EXPOSURE` | per market, line and currency: open count, outstanding reserve, approved-not-settled; totals per currency |

Every mutation after submit answers 200 with the claim as it now stands and its new `ETag`. Errors are RFC 9457 problems; the `code` is stable: `claim.not_found` (404), `claim.version_mismatch` (412), `claim.if_match_required` (428), `claim.invalid_transition`, `claim.reserve_required`, `claim.not_assigned_to_you` (409), `claim.amount_invalid` (400), `claim.info_request_not_found` (404).

## 1. Set up (bash)

```bash
B=http://localhost:5080/api/v1/claims
PW='Fugue-Dev-Passw0rd!'  # secrets-scan:ignore
tok() { curl -s -X POST http://localhost:5080/api/v1/auth/token -H 'Content-Type: application/json' -d "{\"email\":\"$1\",\"password\":\"$PW\"}" | sed 's/.*"accessToken":"\([^"]*\)".*/\1/'; }
SELF=$(tok self@fugue.test); OTHER=$(tok self2@fugue.test); STAFF=$(tok staff@fugue.test); MGR=$(tok manager@fugue.test)
# call METHOD PATH TOKEN [JSON]: sends If-Match from $ETAG, prints the HTTP status and the claim's status or the problem code, keeps the new ETag
call() {
  local out; out=$(curl -si -X "$1" "$B$2" -H "Authorization: Bearer $3" -H "If-Match: $ETAG" -H 'Content-Type: application/json' ${4:+-d "$4"})
  echo "$out" | grep -oE '^HTTP/[0-9.]+ [0-9]+ [A-Za-z ]+|"code":"[^"]*"|"status":"[A-Za-z]+"' | tr '\n' ' '; echo
  local new; new=$(echo "$out" | grep -i '^etag:' | sed 's/^[^:]*: //' | tr -d '\r'); [ -n "$new" ] && ETAG=$new; return 0
}
```

## 2. The happy path

```bash
YDAY=$(date -u -v-1d +%F 2>/dev/null || date -u -d yesterday +%F)
R=$(curl -si -X POST $B -H "Authorization: Bearer $SELF" -H 'Content-Type: application/json' \
  -d "{\"market\":\"SG\",\"line\":\"Motor\",\"policyNumber\":\"POL-SG-5001\",\"incidentDate\":\"$YDAY\",\"incidentDescription\":\"Rear-ended at the lights.\"}")
echo "$R" | grep -iE '^HTTP|^location|^etag'
ID=$(echo "$R" | grep -i '^location:' | sed 's#.*/##' | tr -d '\r'); ETAG=$(echo "$R" | grep -i '^etag:' | sed 's/^[^:]*: //' | tr -d '\r')
```
Expect: `201 Created`, a `Location` ending in the new id, an `ETag`, and a body with `"status":"Submitted"` and a number like `CLM-SG-2026-000001`.

```bash
call POST /$ID/pickup $STAFF                                                   # 200, status UnderReview, you are the officer
call PUT  /$ID/reserve $STAFF '{"amountMinor":250000}'                         # SGD 2,500.00 (minor units); the claimant never sees it
call POST /$ID/info-requests $STAFF '{"question":"Photos of the damage?"}'     # status InfoRequested
RID=$(curl -s $B/$ID -H "Authorization: Bearer $SELF" | sed 's/.*"openInfoRequest":{"id":"\([^"]*\)".*/\1/')
call POST /$ID/info-requests/$RID/response $SELF '{"answer":"Attached."}'      # the claimant answers: UnderReview again
call POST /$ID/decision $STAFF '{"outcome":"approve","amountMinor":200000,"reason":"Covered."}'   # Approved
call POST /$ID/settle $MGR                                                      # Settled (only a manager)
curl -s $B/$ID/timeline -H "Authorization: Bearer $SELF"
```
Expect: each `call` prints `HTTP/1.1 200 OK` and the claim's new status (`UnderReview`, `UnderReview`, `InfoRequested`, `UnderReview`, `Approved`, `Settled`); the timeline lists Submitted, UnderReview, InfoRequested, UnderReview, Approved, Settled, with staff actions shown to the claimant as `Claims team`. The reserve (250000) appears nowhere in anything the claimant reads; as staff, `curl -s $B/$ID -H "Authorization: Bearer $STAFF"` shows `"reserveMinor":250000`.

## 3. The guards

Start a fresh claim for each (repeat the submit in section 2), then:

| Demo | Command | Expect |
|---|---|---|
| **404**, not yours | `curl -si $B/$ID -H "Authorization: Bearer $OTHER"` | `404`, `claim.not_found` (also for the timeline and for answering): a claim you cannot see looks absent |
| **428**, no ETag | `curl -si -X POST $B/$ID/pickup -H "Authorization: Bearer $STAFF"` | `428`, `claim.if_match_required` |
| **412**, stale ETag | pick up as staff (`call POST /$ID/pickup $STAFF`), then `OLD=$ETAG`; `call PUT /$ID/reserve $STAFF '{"amountMinor":1000}'`; then `ETAG=$OLD; call PUT /$ID/reserve $STAFF '{"amountMinor":2000}'` | `412`, `claim.version_mismatch`: re-read and retry with the new ETag |
| **409**, wrong state | fresh ETag (`ETAG=$(curl -si $B/$ID -H "Authorization: Bearer $STAFF" \| grep -i '^etag:' \| sed 's/^[^:]*: //' \| tr -d '\r')`), then `call POST /$ID/pickup $STAFF` again | `409`, `claim.invalid_transition` ("cannot move from UnderReview to UnderReview") |
| **409**, no reserve | on a picked-up claim with no reserve: `call POST /$ID/decision $STAFF '{"outcome":"approve","amountMinor":1,"reason":"x"}'` | `409`, `claim.reserve_required` |
| **400**, over the reserve | set a reserve of 1000, then approve 1001 | `400`, `claim.amount_invalid` |
| **409**, not your claim | as another officer (support, `tok support@fugue.test`) request info on a claim `staff@` holds | `409`, `claim.not_assigned_to_you`; `manager@` may |
| **403**, staff cannot settle | `call POST /$ID/settle $STAFF` | `403` |

## 4. Workload and exposure

```bash
curl -s http://localhost:5080/api/v1/claims/workload -H "Authorization: Bearer $STAFF"
curl -s http://localhost:5080/api/v1/claims/exposure -H "Authorization: Bearer $MGR"
```
Expect on a freshly seeded database: `workload` lists `Dev Staff` and `Dev Manager` with their open claims by status, `unassignedCount` 4, ageing bands 0-2, 3-7, 8-14, 15+; `exposure` has rows per market and line and `totals` with one entry per currency (SGD, MYR, THB, HKD, IDR, PHP), never a number that mixes two currencies. Staff calling `exposure` get `403`.

## 5. The seed

`--seed` (Development) writes 20 claims, idempotently (a second run writes none): every market, both lines, every status, amounts in minor units (all six currencies treated as 2 decimals, so IDR 15,000,000 is `1500000000`), timelines built through the real state machine, a mix of officers (`staff@`, `manager@`), three claims owned by `self@` (one InfoRequested with an open question), and no outbox events (seeding must not fire webhooks). Their numbers are `CLM-<market>-<year>-9000NN`.

## 6. Events

Every move also emits an outbox event keyed by the claim id (so per-claim order holds): `claim.submitted.v1`, `claim.assigned.v1`, `claim.info.requested.v1`, `claim.info.provided.v1`, `claim.decided.v1` (status says approved or rejected), `claim.settled.v1`. Payloads are thin on purpose: `id`, `number`, `status`, `market` (`line` on submitted); never the description, reserve or amounts. Subscribe to `claim.*` as in runbook 07.
