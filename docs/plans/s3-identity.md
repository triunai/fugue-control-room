# S3 identity plan (W-005): JWT login and a lean UAM

Status: proposed, awaiting cross-family review (critical seam: auth, migration). No code until reviewed. D-016 to D-021 are `proposed`; ADR-008 is written with the build. The model is a port of the owner's UAM v3 design, trimmed; schema is ported, not redesigned.

Premises checked: `ICurrentUser` and `ClaimsCurrentUser` exist; no auth is wired in `Program.cs`; `MapStatus` throws (W-009, the owner's); the last migration is `InitialCustomers`; a fitness test bans `Microsoft.AspNetCore*` in Application.

## 1. Authentication
| Decision | Recommendation | Rejected |
|---|---|---|
| Scheme | JwtBearer 8.x, `MapInboundClaims=false`, `ValidAlgorithms=[HS256]`, validate issuer, audience, lifetime, key | Cookies |
| Signing | HS256, key from `Auth__SigningKey` (env / user-secrets), at least 32 bytes, checked at startup (`ValidateOnStart`). Issuer `fugue`, audience `fugue-api` in appsettings | RS256/JWKS: issuer and verifier are one process; revisit if a broker is extracted |
| Token source | `POST /api/v1/auth/token` (email + password), all environments; `PasswordHasher<User>` behind an `IPasswordVerifier` port in Infrastructure, `IterationCount` 210,000 | `dotnet user-jwts`: dev-only, own key, unusable from Bruno or Testcontainers |
| TTL, skew | 15 min, `ClockSkew` 30 s | Default 5 min skew |
| Claims | `sub`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp` only; no roles or permissions | Permission claims (section 2) |
| Refresh tokens | DEFER (D-021): about a day of the ~3 left; re-login after 15 min | Include |
| Login safety | One failure `auth.invalid_credentials` (Unauthorized) for unknown email, wrong password, inactive, deleted, or `local_login_enabled=false`. The handler always does one hash verify (a fixed dummy hash when no user is found). Validator: NotEmpty and max length only | Distinct messages; early return (timing oracle) |
| Lockout | None in S3; S5 rate limiting partitions `/auth/token` | Counters: state, and lets an attacker lock out a victim |
| Secrets | None in appsettings; `.env.example` placeholder; tests use a random key | Committed dev key |
| Open point | `CLAUDE.md` says "`PasswordHasher<T>` / Argon2id"; `PasswordHasher<T>` is PBKDF2-HMAC-SHA512 (v3). Plan follows `PasswordHasher<T>`; owner to amend the wording | Argon2 package |

## 2. Authorization (UAM v3, lean)
Chain: **module -> resource; resource x action -> permission; role_permissions -> role; user_roles -> user; user_permission_overrides.** Roles are global.

**Precedence: deny override > allow override > role grant.** Effective = (grants of the user's active roles + unexpired allow overrides) minus unexpired deny overrides. `expires_at` in the past means the override is ignored. Inactive or deleted users and inactive roles contribute nothing. One override row per `(user_id, permission_id)`, so allow and deny never collide.

**Delivery: cached DB lookup, not token claims.** `JwtBearerEvents.OnTokenValidated` calls `IUserAccessReader` (Application port, EF in Infrastructure), cached in `IMemoryCache` for 60 s and evicted when a role link or override changes; it adds in-memory `perm` claims and fails the ticket for an inactive or deleted user. Revocation latency is at most 60 s, independent of the token TTL. Rejected: claims (stale until expiry, large token); uncached lookup (a query per call).

**Enforcement.** `[HasPermission("CUSTOMERS_EDIT")]` sets `Policy = "perm:<code>"`; a custom `IAuthorizationPolicyProvider` builds it (authenticated + `PermissionRequirement`) and delegates other names to the default; `FallbackPolicy` = authenticated user; `[AllowAnonymous]` only on token, ping, health (S5). `ICurrentUser` gains `HasPermission(string)`. Rejected: `[Authorize(Roles=..)]` (couples endpoints to role names).

**Codes.** v3 form `RESOURCE_ACTION` (`CUSTOMERS_EDIT`), generated from the pair. Seed: module `CORE`; resources `CUSTOMERS`, `USERS`, `OVERRIDES`; actions `VIEW, INSERT, EDIT, DELETE, SUSPEND, ALL`. Permissions are an explicit pair list, not a full cross product (no meaningless codes). Roles (all `is_system`): `ADMIN` (every permission), `STAFF` (all `CUSTOMERS_*`), `SELF_SERVICE` (`CUSTOMERS_VIEW`, `CUSTOMERS_EDIT`). Users are never in a migration.

## 3. Resource ownership
Nullable `customers.user_id` (typed `UserId?`, FK to users, unique where not null). Staff set it via `CreateCustomerCommand.UserId`. v3 keeps scope out of RBAC ("data filtering"); adaptation: the action `ALL` (`CUSTOMERS_ALL`) widens every customers permission from own row to all rows, so a deny override can narrow a staff user. Handlers build an `OwnerScope` from `ICurrentUser`; the repository filters by it on get, list, find-for-update and delete. A miss is `customer.not_found` (404), identical for an absent id and another person's row. Self-service: view and edit own only; create, suspend, delete are staff. Rejected: `IAuthorizationService` resource handlers (need ASP.NET in Application or fetch-then-check in the controller, cannot filter lists, invite a leaky 403).

## 4. Data and migration
One migration `AddIdentity` (down drops all), catalog row 2. Tables: `users`, `roles`, `modules`, `resources`, `actions`, `permissions`, `role_permissions`, `user_roles`, `user_permission_overrides`, plus `customers.user_id`. Typed ids: `UserId, RoleId, ModuleId, ResourceId, ActionId, PermissionId, UserRoleId, UserPermissionOverrideId`. Seed rows use fixed literal ids.

| Kept verbatim from v3 | Changed (why) |
|---|---|
| `users`: `display_name`, `email`, `contact_number`, `is_active`, `local_login_enabled`, `password_last_changed_at`; unique email | `password_hash`+`password_salt` BYTEA become one `password_hash` text (`PasswordHasher` encodes the salt); the unique email becomes a partial unique index `WHERE NOT is_deleted` so a deleted email can be reused; SSO, reset-token, expiry, last-login columns dropped (non-goals) |
| `roles`: `name`, `code`, `description`, `is_system`, `is_active` | `tenant_id` and its composite uniques dropped (ADR-003): `UNIQUE(code)`; delete of a `is_system` role refused in the repository |
| `modules`, `resources`, `actions`, `permissions`: all columns and uniques (`code`; `(module_id, code)`; `(resource_id, action_id)`) | none beyond the conventions below |
| `role_permissions`: composite PK, both cascades; `user_roles`: surrogate `id`, `assigned_by_user_id`, `assigned_at` | `user_roles` unique is `(user_id, role_id)` (no tenant) |
| `user_permission_overrides`: `is_allowed`, `reason`, `created_at/by` | `tenant_id` dropped; `expires_at timestamptz null` added (audit note); unique `(user_id, permission_id)` |
| Soft delete (`is_deleted`, `deleted_at`, `deleted_by`) on users, modules, resources | Rule written down: soft delete where rows carry identity or are referenced (those three); roles use `is_active`; actions and permissions change only by migration |
| Indexes on `user_roles(role_id)`, `role_permissions(permission_id)`, overrides `(user_id)` | `idx_users_email` dropped (the unique index covers it) |

Convention-forced changes, all tables: `TIMESTAMP` becomes `timestamptz`; snake_case from EF; `uuid-ossp` and `uuid_generate_v7()` dropped, ids are app-generated UUIDv7 (`Uuid7`, Postgres 17, no DB default); entity tables take `IAuditable` string actor stamps (ADR-006 interceptor) instead of uuid `created_by`/`updated_by` FKs. `migration-retrofit-qa` applies in part (forward and rollback SQL, catalog, adversarial review, run on a throwaway container); its Supabase and RPC checks are n/a. The Development-only `--seed` adds admin, staff and a self-service user linked to a customer, with one documented dev password.

## 5. Error mapping
| Case | Result |
|---|---|
| No, expired, bad-signature token | Framework 401, problem+json `auth.unauthenticated` via `OnChallenge` and `IProblemDetailsService` (correlationId from enrichment) |
| Authenticated, missing permission | Framework 403 `auth.forbidden` via `OnForbidden`; names no permission |
| Bad login | `FugueErrors.Unauthorized("auth.invalid_credentials")` |
| Cross-owner or absent record | `NotFound` (404, never 403) |
| Override targets the caller | `Conflict` `override.self_target` |
Login 401 and ownership 404 flow through `MapStatus` (W-009): their HTTP assertions are red-by-design with `RED:` comments, and the logic is pinned green at Result level as `CustomerSeamTests` does. Framework 401/403 do not depend on `MapStatus`.

## 6. Fitness functions (baselines start empty, shrink-only)
1. Every controller action has `[HasPermission]` or `[AllowAnonymous]` on the method.
2. Application references no `Microsoft.IdentityModel`, `System.IdentityModel`, `Microsoft.AspNetCore` (extends the existing rule; covers JwtBearer).
3. Types using `Microsoft.AspNetCore.Identity` live in `Fugue.Infrastructure.Identity`; no direct `Rfc2898DeriveBytes`.
4. Every `[HasPermission]` literal exists in the seeded permission catalog.
5. Runtime: every endpoint carries authorize metadata; the anonymous set is exactly {auth/token, ping}. New typed ids pass the existing id rule.

## 7. Tests (lean)
Testcontainers on `WebApplicationFactory`, random key, users seeded through the real hasher: 401 no token, expired, bad signature; 403 missing permission; 404 self-service on another's customer; 200 own; 200 staff; list scoping; login unknown email vs wrong password give equal status and body; inactive user blocked mid-session; effective-permission table tests (role only, allow adds, deny removes, expired ignored). Bruno: `auth/` folder (token, wrong password, no token, own, other), a collection pre-request script that logs in once and sets `token`, dev credentials in `environments/local.bru`; `npm run api:test` stays one command.

## 8. Docs
Runbook `docs/runbooks/05-auth.md` (get a token, call as each role, dev password); ADR-008 from D-016 to D-020; failure-mode candidates: permissions stale up to 60 s; `OnTokenValidated` double-adding claims; policy provider not delegating to the default; `MapInboundClaims` renaming `sub`; ai-journal entry (the owner overrode the earlier "defer overrides" advice and chose a lean port of UAM v3).

## 9. Slices (each green: build 0 warnings, `dotnet test --no-build`, `dotnet format --verify-no-changes`, `Refs: W-005`)
1. **Schema:** entities, typed ids, EF config, `AddIdentity`, catalog row, reference seed. Gate: migration applies and rolls back.
2. **Authentication:** JwtBearer, options validation, login, hasher port, problem+json events, fallback policy, `AllowAnonymous`, test auth helper; existing tests and Bruno log in. Gate: the 48 green stay green.
3. **Authorization:** reader (role grants), `OnTokenValidated`, policy provider, `[HasPermission]` everywhere, fitness 1 to 4. Gate: 403 matrix.
4. **Ownership:** `customers.user_id`, `OwnerScope`, scoped repository, 404 cases, Bruno own and other.
5. **Overrides (cut last):** precedence, `expires_at`, set/remove/view endpoints (`OVERRIDES_*`, admin only, never on self), cache eviction, tests, ADR-008, runbook final. If cut, the table stays empty and the reader ignores it.

Non-goals (parked in `docs/archive/deferred.md`): refresh tokens and `user_sessions`; SSO and its columns; tenants (ADR-003 toggle); groups, group_members, group_roles; access_audit and grant_audit (grant audit is a strong next add); all domain tables (clients, projects, mappings, position types, assignments); user and role CRUD APIs; password reset and expiry; lockout; RS256; rehash on login. Stop if: a premise is false; a gate fails twice; `MapStatus` would need implementing; secrets or tokens reach logs. Risks: dummy-hash timing is approximate; the cache is per instance.

## What I could NOT verify
That `OnForbidden` fires for policy failures under .NET 8 as assumed (fallback: `IAuthorizationMiddlewareResultHandler`); that `IterationCount` applies to new v3 hashes; the secrets-scan reaction to the dev password; hasher timings. The v3 SQL was read for design, not run.
