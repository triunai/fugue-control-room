# S3 identity plan (W-005): JWT login, lean UAM v3, module management

Status: REVISED 2026-11-25 after the cross-family review (`docs/reviews/codex/2026-11-25-s3-identity-plan.md`, verdict REJECT, 10 findings; the owner accepted all 10). Owner scope changes folded in (section 10). Critical seam: auth, migration. D-016 to D-026 carry the decisions; ADR-008 (auth model) and ADR-009 (module management) are written with the build. The model is a port of the owner's UAM v3 design, trimmed; schema is ported, not redesigned.

Premises checked: `ICurrentUser` and `JwtCurrentUser` exist; no auth is wired in `Program.cs`; `MapStatus` is IMPLEMENTED (4e8ee4e, W-009, closes review finding 8); the last migration is `InitialCustomers`; a fitness test bans `Microsoft.AspNetCore*` in Application.

## Review resolutions (Codex REJECT, all 10 accepted)
| # | Sev | Finding | Resolution | Pinned by |
|---|---|---|---|---|
| 1 | HIGH | create bypasses ownership | The owner of a new customer is the caller unless the caller has effective `CUSTOMERS_ALL`; only then may `userId` be supplied | staff with an explicit `CUSTOMERS_ALL` deny cannot create for another owner |
| 2 | HIGH | login abuse | Built-in ASP.NET rate limiter on `POST /auth/token` and `/auth/refresh`: partitions by client IP and by canonical email, fixed window plus concurrency limit, evaluated BEFORE password verification; 429 problem+json with `Retry-After` | burst test: 429, no hash verify |
| 3-4 | MED | permission cache races and expiry | NO cache in S3. Effective permissions are one indexed query per authenticated request, run in `JwtBearerEvents.OnTokenValidated` (once per request: the handler caches its result; an `IClaimsTransformation` runs on every `AuthenticateAsync`, risks double-added claims and cannot fail the ticket as cleanly). Caching moves to S5 HybridCache (`deferred.md`: revive when p95 of the permission query exceeds 5 ms or auth dominates DB load) | expiry boundary tests with `FakeTimeProvider` |
| 5 | MED | email oracle | Email change is staff-only (`CUSTOMERS_ALL`); a self-service update that changes the email is 403 `customer.email_change_forbidden` (the row is the caller's own, so nothing leaks) | self-service update with a new email |
| 6 | MED | email canonicalisation | One canonical key: trim + `ToLowerInvariant`, stored; partial unique index `WHERE NOT is_deleted`; login and uniqueness use non-deleted rows only | case variants; deleted-email reuse |
| 7 | MED | deleted catalog still grants | Effective query excludes soft-deleted modules, resources and actions, inactive modules, inactive roles and groups | one test per level |
| 8 | MED | HTTP contracts were 500s | Satisfied: `MapStatus` is done. Every HTTP assertion in S3 is green; none red-by-design | whole suite green |
| 9 | MED | verification optional | Mandatory acceptance gates (section 7). ONE complete security vertical slice first (auth + permissions + ownership), then module management, then the additions, overrides last | gates are tests |
| 10 | LOW | key entropy | `Auth:SigningKey` is base64 of at least 32 random bytes (`openssl rand -base64 32`), from user-secrets or env; startup fails if missing, undecodable, shorter than 32 bytes, or a documented placeholder | options-validation tests |

## 1. Authentication
| Decision | Choice | Rejected |
|---|---|---|
| Scheme | JwtBearer 8.x, `MapInboundClaims=false`, `ValidAlgorithms=[HS256]`, validate issuer, audience, lifetime, signing key | Cookies |
| Signing | HS256; key contract above; issuer `fugue`, audience `fugue-api` in appsettings; `ValidateOnStart` | RS256/JWKS (one process is issuer and verifier) |
| Token source | `POST /api/v1/auth/token` (email + password), `[AllowAnonymous]`; `PasswordHasher<User>` behind `IPasswordVerifier` in Infrastructure | `dotnet user-jwts` |
| TTL, skew | access 15 min, `ClockSkew` 30 s | default 5 min skew |
| Claims | `sub`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp` only; permissions are looked up, not carried | permission claims |
| Refresh | IN S3 (owner, section 10): opaque 256-bit token, only its SHA-256 stored, rotation, family reuse detection, logout | refresh inside the JWT |
| Login safety | One failure `auth.invalid_credentials` for unknown email, wrong password, inactive, deleted, `local_login_enabled=false`; always one hash verify (dummy hash when no user) | distinct messages; early return |
| Lockout | none (a lockout lets an attacker lock a victim out); the rate limiter is the brute-force control | counters |
| Secrets | none in appsettings; `.env.example` placeholder; tests generate a random key | committed dev key |
| Open point | `CLAUDE.md` says "`PasswordHasher<T>` / Argon2id"; `PasswordHasher<T>` is PBKDF2-HMAC-SHA512 (v3). Plan follows `PasswordHasher<T>`; owner to amend the wording | Argon2 package |

## 2. Authorization (UAM v3, lean)
Chain: **module -> resource; resource x action -> permission; role_permissions -> role; user_roles -> user; group_members -> group -> group_roles -> role; user_permission_overrides.** Roles are global.

**Effective permissions** = (permissions of the user's active roles, directly or through active groups) + unexpired allow overrides, minus unexpired deny overrides (**deny > allow > role**). An override with `expires_at <= now` is expired and ignored (`now` from `TimeProvider`). Then filtered: only permissions whose module is active, not deleted and enabled in configuration, and whose resource and action are not deleted. An inactive or deleted user has none and the ticket fails (401). One override row per `(user_id, permission_id)`.

**Delivery:** `OnTokenValidated` calls `IUserAccessReader` (Application port; one raw-SQL CTE through EF in Infrastructure) and adds in-memory `perm` claims. Nothing is cached; revocation is immediate.

**Enforcement:** `[HasPermission("CUSTOMERS_EDIT")]` -> policy `perm:<code>`, built by a custom `IAuthorizationPolicyProvider` that delegates other names to the default; `FallbackPolicy` = authenticated; `[AllowAnonymous]` only on token, refresh, ping. Framework 401/403 are problem+json with `correlationId` (`OnChallenge`, and `IAuthorizationMiddlewareResultHandler` for 403). `ICurrentUser` gains `HasPermission(string)`.

**Codes:** `RESOURCE_ACTION`. Seed: modules `SYSTEM`, `CUSTOMERS`, `IDENTITY`; resources `MODULES` (SYSTEM), `CUSTOMERS` (CUSTOMERS), `GROUPS`, `OVERRIDES` (IDENTITY); actions `VIEW, INSERT, EDIT, DELETE, SUSPEND, ALL, MANAGE`; explicit permission list: `CUSTOMERS_{VIEW,INSERT,EDIT,DELETE,SUSPEND,ALL}`, `GROUPS_{VIEW,MANAGE}`, `OVERRIDES_{VIEW,MANAGE}`, `MODULES_{VIEW,MANAGE}`. Roles (all `is_system`): `ADMIN` (everything), `STAFF` (all `CUSTOMERS_*`), `SELF_SERVICE` (`CUSTOMERS_VIEW`, `CUSTOMERS_EDIT`). One seeded group `SUPPORT` holding role `STAFF`. Users are never in a migration.

## 3. Resource ownership
Nullable `customers.user_id` (FK, indexed, NOT unique: a restricted creator owns every customer they create). `CUSTOMERS_ALL` widens every customers permission from own rows to all rows; a deny override narrows a staff user. Handlers build an `OwnerScope(All, UserId)` from `ICurrentUser`; the repository filters on get, list (the owner filter is part of the query, so cursor pages are filtered), find-for-update and delete. Create: see finding 1. A miss is `customer.not_found` (404), identical for an absent id and another person's row; update, delete, suspend, reactivate by a non-owner are 404.

## 4. Data and migration
One migration `AddIdentity` (down drops all), catalog row 2. Tables: `tenants`, `users`, `roles`, `modules`, `resources`, `actions`, `permissions`, `role_permissions`, `user_roles`, `groups`, `group_members`, `group_roles`, `user_permission_overrides`, `refresh_tokens` (UAM `user_sessions`), `grant_audit`, `access_audit`, plus `customers.user_id`. Ids are typed UUIDv7.

**Base entity contract (owner):** every mutable entity implements `IAuditable` (stamped by the EF interceptor, or `IAuditStamp` on Dapper paths). Append-only rows (`grant_audit`, `access_audit`) carry `occurred_at` plus an actor and sit on an allowlist baseline. Fitness: every Domain entity is `IAuditable` or allowlisted.

Differences from v3: `TIMESTAMP` -> `timestamptz`; snake_case; app-generated UUIDv7; string actor stamps; `password_hash`+`password_salt` -> one `password_hash` text; unique email is a partial index on the canonical email `WHERE NOT is_deleted`; `tenant_id` only on `users` (nullable FK, NO query filter; the ADR-003 toggle stays off); `UNIQUE(code)` on roles; soft delete (`is_deleted`, `deleted_at`, `deleted_by`) on users, modules, resources, actions; roles and groups use `is_active`; `expires_at` on overrides; SSO, reset-token, expiry, last-login columns stay out. `migration-retrofit-qa` applies in part (forward and rollback SQL, idempotent script review, catalog, adversarial review, run on a throwaway container); hosted-platform RPC checks are n/a.

## 5. Error mapping
| Case | Result |
|---|---|
| No, expired, bad-signature, wrong issuer/audience/algorithm token | 401 `auth.unauthenticated` problem+json |
| Authenticated, missing permission | 403 `auth.forbidden`; names no permission |
| Bad login or refresh | 401 `auth.invalid_credentials` / `auth.invalid_refresh_token` |
| Rate limited | 429 `auth.rate_limited` + `Retry-After` |
| Cross-owner or absent record | 404 `customer.not_found` |
| Module disabled | 404 `module.disabled` (existence not revealed; logged at Information) |
| Override targets the caller | 409 `override.self_target` |

## 6. Fitness functions (baselines shrink-only)
1. Every controller action has `[HasPermission]` or `[AllowAnonymous]`. 2. Application references no `Microsoft.IdentityModel`, `System.IdentityModel`, `Microsoft.AspNetCore`. 3. `Microsoft.AspNetCore.Identity` only in `Fugue.Infrastructure.Identity`; no direct `Rfc2898DeriveBytes`. 4. Every `[HasPermission]` literal exists in the seeded catalog. 5. Runtime: the anonymous endpoint set is exactly {auth/token, auth/refresh, ping}. 6. Every feature controller carries exactly one `[Module]`; system controllers (auth, diagnostics, modules) on an allowlist baseline. 7. Every Domain entity is `IAuditable` or on the append-only allowlist.

## 7. Mandatory acceptance gates (tests, not optional)
Cross-owner update, delete, suspend, reactivate -> 404; restricted staff create (bound to the caller; an explicit `CUSTOMERS_ALL` deny cannot assign another owner); override expiry boundary with `FakeTimeProvider` (`expires_at == now` is expired); cursor pages never contain another owner's rows; JWT rejects wrong issuer, audience, algorithm, expired and bad signature; signing-key validation (missing, undecodable, short, placeholder); login rate limit 429; uniform login failure; email canonicalisation and deleted-email reuse; effective-permission table (role only, group role, allow adds, deny wins, expired ignored, inactive role, deleted module/resource/action, inactive module, config-off module); module toggling (config-off wins over db-on, db-off -> 404, round-trip, `MODULES_MANAGE` required, If-Match); refresh rotate, reuse revokes the family, expired -> 401, logout; audit rows written in the same transaction (rolled back with it); self-service cannot change email. Plus happy paths (200 own, 200 staff, 403 matrix). Lean beyond that.

## 8. Module management (owner addition B)
`modules` is the runtime registry; configuration `Modules:<CODE>:Enabled` (default true; appsettings/env) is the hard kill switch. Effective enabled = config AND db `is_active`. `[Module("CUSTOMERS")]` on each feature controller plus an action filter: disabled -> 404 problem+json `module.disabled`, logged at Information (and an `access_audit` row). `GET /api/v1/modules` lists code, config state, db state, effective; `PATCH /api/v1/modules/{code}` toggles `is_active` (`MODULES_MANAGE`, `If-Match`; `SYSTEM` cannot be disabled). A disabled module's permissions leave the effective set. Fitness 6. ADR-009.

## 9. Slices (each green: build 0 warnings, tests, `dotnet format`, `Refs: W-005`)
1. Plan, decisions, journal (docs). 2. Schema: domain, typed ids, EF config, `AddIdentity`, catalog row, seed, base-entity fitness. 3. SECURITY VERTICAL SLICE: JwtBearer, key validation, login + rate limiter, problem+json 401/403, reader, policy provider, `[HasPermission]` everywhere, ownership scoping, 204s, existing tests and Bruno on tokens, the section 7 gates for these. 4. Module management. 5. Tenants, groups (effective permissions, seed). 6. Refresh tokens and logout. 7. `grant_audit` / `access_audit` (same-transaction writes). 8. Overrides last (precedence, expiry, endpoints, grant audit). 9. Docs: ADR-008/009, runbook `05-auth.md`, Bruno `auth/`. If scope threatens the finish: stop after groups and report what is left.

## 10. Owner scope changes (2026-11-25)
A. 204 for Customers `PUT`, `suspend`, `reactivate` (`SendNoContent<T>`: 204 + ETag); DELETE already 204; tests, Bruno scripts and runbook 04 follow. B. Module management (section 8). C. Basic tenants, refresh tokens, groups, grant_audit and access_audit are built and removed from `docs/archive/deferred.md`. A separate rich general `audit_log` is the NEXT batch (another agent): the audit tables here write inside the `IUnitOfWork` transaction and read `ICorrelationContext`, which is the seam. Reminder kept in hot-state: clone the work repo to study its model/entity file conventions (patterns only, never copy files or names).

Non-goals: SSO columns; tenant query filter; domain tables (clients, projects, ...); user and role CRUD APIs; password reset and expiry; lockout; RS256; rehash on login; permission caching (S5).
Stop if: a premise is false; a gate fails twice; secrets or tokens reach logs.

## What I could NOT verify (at plan time)
That `OnForbidden` fires for policy failures under .NET 8 (fallback used: `IAuthorizationMiddlewareResultHandler`); that `IterationCount` applies to new v3 hashes; the secrets-scan reaction to the dev password. The v3 SQL was read for design, not run.
