# ADR-008 — Authentication, authorization and the identity model

- Status: accepted
- Date: 2026-11-25
- Decision entry: D-016, D-017, D-018, D-019, D-020, D-022, D-024, D-026 in `docs/decisions.md`

## Context
S3 needs sign-in, per-user permissions and ownership scoping on a domain-neutral scaffold. The owner's UAM v3 design (users, roles, a module/resource/action/permission catalog, groups, overrides, sessions, two audit tables) is the model. The first plan was rejected by Codex (2 HIGH, 7 MED, 1 LOW); the owner accepted all ten findings and widened the scope. Plan and resolution table: `docs/plans/s3-identity.md`; review: `docs/reviews/codex/2026-11-25-s3-identity-plan.md`.

## Decision
* **Authentication.** JwtBearer, HS256 pinned, issuer, audience, signature and lifetime validated (lifetime against `TimeProvider`), claims not remapped. The signing key is the base64 of at least 32 random bytes from user-secrets or the environment; a missing, undecodable, short, repetitive or placeholder key stops startup. Access tokens live 15 minutes and carry identity claims only.
* **Sign-in.** `POST /api/v1/auth/token`: one uniform `401 auth.invalid_credentials` for every failure and one hash verification on every attempt (a dummy hash when the user does not exist); `PasswordHasher<User>` (PBKDF2-HMAC-SHA512) only, behind `IPasswordVerifier` in `Fugue.Infrastructure.Identity`. No lockout (it would let an attacker lock a victim out). The built-in ASP.NET rate limiter partitions by client IP (middleware) and by canonical email (action filter, window plus concurrency) and runs BEFORE any password work; 429 problem+json with `Retry-After`.
* **Sessions.** Opaque 256-bit refresh tokens; only the SHA-256 digest is stored with a family id. Rotation uses one conditional UPDATE; reuse of a rotated token revokes the family; logout revokes the family. Refresh and logout are anonymous (the token is the credential).
* **Permissions.** Effective permissions are ONE raw-SQL query per authenticated request in `OnTokenValidated` (once per request, can fail the ticket), added as in-memory `perm` claims. NO cache in S3 (the first plan's 60 s cache had invalidation races and delayed override expiry; HybridCache is S5, trigger in `docs/archive/deferred.md`). Effective = (active roles, directly and through active groups) + unexpired allow overrides - unexpired deny overrides, i.e. deny > allow > role; `expires_at <= now` is expired; permissions under deleted or inactive modules, deleted resources or actions, or modules switched off in configuration are dropped. `[HasPermission("X_Y")]` builds a `perm:` policy through a delegating `IAuthorizationPolicyProvider`; the fallback policy is "authenticated"; anonymous endpoints are explicit.
* **Ownership.** `customers.user_id`. `CUSTOMERS_ALL` widens every customers permission from own rows to all rows. The repository filters get, list (so cursor pages), find-for-update; a miss is `404`, identical for absent and foreign rows. Create binds the owner to the caller unless the caller has effective `CUSTOMERS_ALL`; email changes are staff-only (no email-existence oracle).
* **Data.** 16 tables (`AddIdentity`); canonical email key (trim + lower-invariant) with a unique index over non-deleted rows only; soft delete where rows carry identity or are referenced (users, modules, resources, actions); every mutable entity is `IAuditable`; the two audit tables are append-only (`occurred_at` + actor) and on a shrink-only allowlist. `tenants` and `users.tenant_id` exist with NO query filter (ADR-003 toggle stays off).
* **Audit.** `grant_audit` rows (group membership, overrides) are staged on the same DbContext and commit in the SAME transaction as the grant. `access_audit` rows (login, refresh, reuse, logout, 403, module-disabled) are best effort (a failed write is logged, never a 500); email is stored only as a keyed HMAC; no password or token is ever stored.

## Alternatives considered
* Permission claims in the token: stale until expiry, large tokens.
* A 60 s permission cache with eviction: races and a delayed expiry boundary (Codex 3-4); an uncached indexed query is simpler and exact at this scale.
* `IAuthorizationService` resource handlers for ownership: need ASP.NET in Application or a fetch-then-check in the controller, cannot filter lists, invite a leaky 403.
* Lockout counters: state, and a denial-of-service lever against the victim.
* RS256/JWKS: one process is both issuer and verifier; revisit if a broker is extracted.
* `[Authorize(Roles=...)]`: couples endpoints to role names.

## Consequences
* A permission change, group change or override takes effect on the user's next request; the cost is one extra indexed query per request.
* Every controller action declares `[HasPermission]` or `[AllowAnonymous]`; the anonymous set is exactly token, refresh, logout, ping (fitness functions pin both).
* `Fugue.Application` references no JwtBearer, IdentityModel or ASP.NET Core; hashing lives only in `Fugue.Infrastructure.Identity` (fitness functions).
* `CLAUDE.md` says "`PasswordHasher<T>` / Argon2id": `PasswordHasher<T>` is PBKDF2; the wording is the owner's to amend.
* Follow-ups: an `expired` grant-audit sweeper; per-token rate limiting on refresh; HybridCache for permissions; the rich `audit_log` (next batch) builds on the shared transaction and `ICorrelationContext`.
* **W-013 amendments (owner calls).** D-034: customer email is unique per owner (`ux_customers_owner_email`) and among unowned rows (`ux_customers_unowned_email`), closing the create-path existence oracle. D-035: login is throttled by IP, by (IP, email) and by a loose per-email ceiling, all before password verification, so no single IP can lock a victim out.
* **W-013 hardening (D-036).** Overrides are optimistic-concurrency guarded (`If-Match`/xmin); group membership has a self-target guard; module config fails closed and `SYSTEM` is never config-disabled; an inactive tenant locks its users out; `access_audit` clips to column widths and hashes email under an HMAC subkey.

## Supersedes
None.
