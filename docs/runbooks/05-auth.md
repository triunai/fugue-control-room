# 05 - Auth: sign in, call as each role, and what the dev users are

Goal: get a token, call the API as admin, staff, support and self-service users, and see 401, 403 and the ownership 404. Needs runbook 03 (database up and migrated) and a signing key.

## 0. Signing key (required; the host refuses to start without one)

The key is the base64 of at least 32 random bytes. Generate it and store it in user-secrets (or `Auth__SigningKey` in your untracked `.env`):
```bash
dotnet user-secrets set Auth:SigningKey "$(openssl rand -base64 32)" --project src/Fugue.Api
```
Expect: the API starts. A missing, non-base64, short (< 32 bytes), repetitive or placeholder key stops startup with a message naming the problem. Never commit a key.

## 1. Run with the development users, and get a token

```bash
set -a; source .env; set +a
ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/Fugue.Api -- --seed
```
The seed (Development only) creates 25 customers and five users, all with the same **development-only password**: `Fugue-Dev-Passw0rd!` (override with `Seed:DevPassword`). It exists only in Development and only in this runbook, the Bruno `local` environment and the seeder; never reuse it.

| User | Role | What it demonstrates |
|---|---|---|
| `admin@fugue.test` | `ADMIN` | everything, including `CUSTOMERS_ALL` (all rows) and module management |
| `staff@fugue.test` | `STAFF` | all `CUSTOMERS_*`, all rows |
| `support@fugue.test` | none directly; member of group `SUPPORT` (role `STAFF`) | permissions arriving through a group |
| `self@fugue.test` | `SELF_SERVICE` | `CUSTOMERS_VIEW` + `CUSTOMERS_EDIT` on their OWN customer only (seeded customer 01) |
| `self2@fugue.test` | `SELF_SERVICE` | owns seeded customer 02 |

```bash
PW='Fugue-Dev-Passw0rd!'  # secrets-scan:ignore
login() { curl -s -X POST http://localhost:5080/api/v1/auth/token -H 'Content-Type: application/json' -d "{\"email\":\"$1\",\"password\":\"$PW\"}"; }
login admin@fugue.test
```
Expect: `{"accessToken":"eyJ...","tokenType":"Bearer","expiresIn":900,"refreshToken":null}` (15 minutes). The token holds only identity claims (`sub`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp`); permissions are looked up on every request, so a revoked role or override takes effect immediately.

Email is canonical (trimmed, case-insensitive). Any failure (unknown email, wrong password, inactive, deleted, local login disabled) returns the same `401` `auth.invalid_credentials`. Sign-in is rate limited per IP and per email (`429`, `Retry-After`, `auth.rate_limited`).

## 2. Call as each user

```bash
T=$(login self@fugue.test | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')
curl -s -H "Authorization: Bearer $T" 'http://localhost:5080/api/v1/customers?limit=100'      # only self's customer(s)
curl -si -H "Authorization: Bearer $T" -X DELETE http://localhost:5080/api/v1/customers/$(uuidgen) | head -1   # 403
curl -si http://localhost:5080/api/v1/customers | head -1                                      # 401 (no token)
```
Expect: the list holds only rows whose `ownerId` is the caller; `DELETE` is `403` with `"code":"auth.forbidden"` (the body never names the permission); no token is `401` with `"code":"auth.unauthenticated"`. Another self-service user asking for self's customer by id gets `404 customer.not_found`, identical to an id that does not exist (never `403`: that would confirm the row exists).

## 3. Ownership in one paragraph

`customers.user_id` is the owner. A caller with effective `CUSTOMERS_ALL` (admin, staff) works on every row and may set the owner on create; everyone else works on their own rows only and a create is bound to themselves, whatever `userId` the body says. Self-service users cannot change a customer's email (`403 customer.email_change_forbidden`); that closes an email-existence oracle through the unique index.

## 4. Reference

Permissions are `RESOURCE_ACTION` codes seeded by migration `AddIdentity`; effective permissions = roles (direct and through groups) + unexpired allow overrides - unexpired deny overrides, then dropped when the module is inactive, deleted or switched off in configuration. Tokens are HS256, validated for issuer, audience, signature and lifetime (30 s skew); anything else is `401`.

## 5. Modules: switch a feature off

A **module** is a feature area (`CUSTOMERS`, `IDENTITY`, and the protected `SYSTEM`). A feature controller declares `[Module("CUSTOMERS")]`. Two independent switches decide whether it answers:

| Switch | Where | Meaning |
|---|---|---|
| Configuration | `Modules:CUSTOMERS:Enabled` (appsettings or env `Modules__CUSTOMERS__Enabled=false`), default on | the hard kill switch; restart or config reload to change; the database cannot override it |
| Registry | `modules.is_active`, changed by `PATCH /api/v1/modules/{code}` | the runtime switch |

**Effective enabled = configuration on AND registry on.** A disabled module answers `404` with `"code":"module.disabled"` (never 403: that would confirm the feature exists) and logs one Information line; its permissions also drop out of everyone's effective set. Anonymous callers still get `401`.

```bash
T=$(login admin@fugue.test | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')
curl -s -H "Authorization: Bearer $T" http://localhost:5080/api/v1/modules
```
Expect: `[{"code":"CUSTOMERS","name":"Customers","configEnabled":true,"dbActive":true,"effective":true,"version":N}, ...]`. `version` is the ETag.
```bash
curl -si -X PATCH http://localhost:5080/api/v1/modules/CUSTOMERS -H "Authorization: Bearer $T" \
  -H 'Content-Type: application/json' -H 'If-Match: "N"' -d '{"isActive":false}' | head -3
curl -si -H "Authorization: Bearer $T" http://localhost:5080/api/v1/customers | head -1
```
Expect: `204` plus a new `ETag`, then `404` `module.disabled`. Re-enable with the new ETag. `PATCH` needs `MODULES_MANAGE` (admin), `GET` needs `MODULES_VIEW`; a missing `If-Match` is `428`, a stale one `412`, an unknown code `404 module.not_found`, and disabling `SYSTEM` is `409 module.system_protected` (it holds module management itself). Try the config switch: start the API with `Modules__CUSTOMERS__Enabled=false` and see `configEnabled:false` with `dbActive:true`, `effective:false`.

## 6. Groups and tenants

A **group** holds roles; every member inherits them (`support@fugue.test` has no role of its own, only membership of `SUPPORT`, which holds `STAFF`). `GET /api/v1/groups` needs `GROUPS_VIEW`; `PUT` or `DELETE /api/v1/groups/{code}/members/{userId}` need `GROUPS_MANAGE`. A membership change takes effect on the member's very next request (permissions are read per request, never cached) and writes a `grant_audit` row (who, what, to whom, granted or revoked, correlation id) in the same transaction as the change; the member list only changes if the audit row is written too.

A **tenant** is a row in `tenants` that a user may point at through the nullable `users.tenant_id`. The tenant id reaches `ICurrentUser.TenantId` (and error responses) from the verified token's user, but nothing filters by it: the `Tenancy:Enabled` toggle of ADR-003 stays off.

## 7. Refresh tokens and logout

`POST /api/v1/auth/token` also returns a `refreshToken`: an opaque 256-bit value shown once. Only its SHA-256 digest is stored (`refresh_tokens.token_hash`), with a `family_id`, `expires_at` (7 days), `revoked_at`, `replaced_by_id` and the creating IP and user agent.
```bash
R=$(login self@fugue.test | sed 's/.*"refreshToken":"\([^"]*\)".*/\1/')
curl -s -X POST http://localhost:5080/api/v1/auth/refresh -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$R\"}"   # new access + new refresh
curl -si -X POST http://localhost:5080/api/v1/auth/refresh -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$R\"}" | head -1   # 401: already rotated
```
Expect: the first call is `200` with a different `refreshToken`. Presenting the OLD token again is `401` `auth.invalid_refresh_token` AND revokes the whole family (theft response): the newest token of that login stops working too, and the user signs in again. Other logins (other devices) are separate families and are unaffected. `POST /api/v1/auth/logout` with a refresh token revokes its family and is always `204` (it reveals nothing about whether the token existed). Refresh and logout are anonymous (the refresh token is the credential) and use the same per-IP rate limit as login; an expired token, an unknown token and a deactivated user are all the same `401`.

## 8. The audit trails

Two append-only tables (they carry `occurred_at` and an actor, not the `created_*` stamps; see the base-entity fitness function):

* `grant_audit`: every grant or revoke of a group membership (and, with overrides, a permission override), written in the SAME transaction as the change: subject type and id, target user, `granted` / `revoked` / `expired`, optional reason, before and after JSON, `correlation_id`, `saga_id`.
* `access_audit`: `login` (success or failure with the real internal reason: `unknown_user`, `bad_password`, `inactive`, `local_login_disabled`), `refresh`, `refresh_reuse`, `logout`, `forbidden` (the missing permission, internal), `module_disabled`. Email is stored only as an HMAC-SHA256 keyed by the signing key; passwords and tokens are never stored. Writing an access row is best effort (a failure is logged, never a 500).

```bash
docker compose exec postgres psql -U fugue -d fugue -c "select occurred_at, event, outcome, reason_code, correlation_id from access_audit order by occurred_at desc limit 10"
```
Find one request everywhere with its `X-Correlation-Id` (runbook 02). A richer general `audit_log` (field-level diffs) is a later batch; these tables share the unit-of-work transaction and the correlation context it will build on.
