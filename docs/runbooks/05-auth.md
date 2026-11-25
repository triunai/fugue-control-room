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
