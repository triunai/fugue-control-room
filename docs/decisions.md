# Fugue — decisions (why it is this way)

Latest first. Add an entry when a call settles. To change a decision, add a new entry that supersedes it. Never delete.

Heading form: `## D-NNN — <title> — <YYYY-MM-DD>`. NNN is three digits. Only the orchestrator mints numbers.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)` for decisions older than the rule. Never guess.
Then: Context (one or two lines) · Decision · Why · Consequences · Supersedes (if any), and three closing lines: `Who:` (owner, AI, review) · `Status:` (accepted, superseded by D-NNN, ...) · `Promote:` (`no`, or `yes (ADR-NNN)`).
Capture a decision THE SAME TURN it is made, as a D entry. Promote it to an ADR file (`docs/adr/_template.md`: context, decision, alternatives, consequences, supersedes) when it changes architecture, a public contract, the data model, security posture or a cross-cutting convention; the D entry links the ADR. `node scripts/spine/adr-check.mjs` (pre-push) fails on an unlinked ADR or a `Promote: yes` without its file.

## Index (D-NNN to ADR file)

| D | Title | ADR |
|---|---|---|
| D-001 | Adopt the doc spine | - |
| D-002 | Retarget .NET 8, FluentResults, single-tenant default (covers the .NET 8 and FluentResults owner overrides) | [ADR-003](adr/ADR-003-single-tenant-default-tenancy-toggle.md), [ADR-004](adr/ADR-004-fluentresults-with-errortype.md) |
| D-003 | Three-layer correlation | [ADR-005](adr/ADR-005-trace-correlation-saga-ids.md) |
| D-004 | EF Core reads, Dapper writes | [ADR-006](adr/ADR-006-ef-core-reads-dapper-writes.md) |
| D-005 | Shared database with `tenant_id`, no RLS (superseded by D-002) | [ADR-001](adr/ADR-001-shared-db-tenant-id.md) |
| D-006 | MVC controllers over `ApiControllerBase`; source-generated Mediator | [ADR-002](adr/ADR-002-controllers-with-api-controller-base.md) |
| D-007 | Clean-room rewrite after the template audit | - |
| D-008 | Name: Fugue; theme the brand, keep domain names plain | - |
| D-009 | `MapStatus` is the owner's | [ADR-004](adr/ADR-004-fluentresults-with-errortype.md) |
| D-010 | OrbStack as the container runtime | - |
| D-011 | Fable architecture critique: accepted, with two challenges | - |
| D-012 | Pin every catch | - |
| D-013 | Beads is a local queue; commits cite only committed ids | [ADR-007](adr/ADR-007-ai-harness-gates.md) |
| D-014 | Codex review gate is advisory, seam-triggered, capped | [ADR-007](adr/ADR-007-ai-harness-gates.md) |
| D-015 | Commit gates and the safe-command guard | [ADR-007](adr/ADR-007-ai-harness-gates.md) |
| D-016 | S3: JWT bearer (HS256), local token endpoint, refresh in S3 (D-024) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-017 | S3: lean UAM v3 port and precedence deny > allow > role (owner overrode "defer overrides") | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-018 | S3: permissions via uncached DB lookup in `OnTokenValidated`, not token claims | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-019 | S3: ownership by repository scoping, 404 on cross-owner | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-020 | S3: uniform login failure, dummy hash, no lockout | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-021 | S3: deferrals and exclusions from UAM v3 | - |
| D-022 | S3: abuse controls, create ownership, canonical email, key hygiene | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-023 | S3: module management (config kill switch + runtime registry) | [ADR-009](adr/ADR-009-module-management.md) |
| D-024 | S3: tenants, refresh tokens, groups, grant/access audit built basic | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-025 | Customers PUT/suspend/reactivate answer 204 + ETag | - |
| D-026 | Base entity contract: IAuditable everywhere, append-only allowlist | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-027 | W-012: `audit_log` table shape, append-only trigger, keyset indexes (no BRIN) | [ADR-010](adr/ADR-010-audit-log.md) |
| D-028 | W-012: capture in the same transaction (EF interceptor in the same SaveChanges; Dapper through `IAuditTrail`) | [ADR-010](adr/ADR-010-audit-log.md) |
| D-029 | W-012: failures recorded by `AuditBehavior` as one failure row; auth commands exempt | [ADR-010](adr/ADR-010-audit-log.md) |
| D-030 | W-012: safety knobs (masking at write time, partial masks, size bound, never sampled, not disable-able outside Development) | [ADR-010](adr/ADR-010-audit-log.md) |
| D-031 | W-012: read API behind `AUDIT_READ`, admin only, keyset paged | [ADR-010](adr/ADR-010-audit-log.md) |
| D-032 | W-012: `grant_audit` and `access_audit` stay specialised; relationship to `audit_log` | [ADR-010](adr/ADR-010-audit-log.md) |
| D-033 | W-012: retention and archival deferred | - |
| D-034 | W-013: customer email is unique per owner (owner call) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-035 | W-013: login limiter keyed on (IP, email) plus a loose per-email ceiling (owner call) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |
| D-036 | W-013: S3 review hardening (override If-Match, group self-target, fail-closed module config, tenant is_active, audit key hygiene) | [ADR-008](adr/ADR-008-auth-and-identity-model.md) |

The ADR files stay the home of architecture reasoning; a D entry below is the short why plus the link. Trailer ids a commit may cite: `W-NNN` (`docs/workstreams.md`), `D-NNN` (this file), `ADR-NNN` (`docs/adr/`).

## D-036 — W-013 S3 review hardening of the admin surface — 2026-11-26
WS: WS-13
Context: the remaining MED and LOW findings of the S3 build review (rv-s3-build.md) after the two owner calls.
Decision: (1) override PUT/DELETE on an existing row require `If-Match` (xmin; 428 without, 412 stale, 412 for a concurrent create or a version sent for a vanished row), creating sends none; the list carries `version`. (2) group membership cannot target oneself (409 `group.self_target`), as overrides already could not. (3) unique-violation and lost-update races in group add/remove and override set are answered (add: success, remove: 404, override: 412), not 500. (4) module configuration fails closed: only true/1/yes/on, blank or absent keep a module enabled; `SYSTEM` can never be config-disabled. (5) `tenants.is_active = false` blocks login, refresh and live tokens, with the same uniform 401. (6) `access_audit` clips reason, ip, user agent and correlation id to their columns instead of losing the row; the email hash uses an HMAC subkey derived from the signing key, and with no usable key stores no hash (the old constant-key fallback was dictionary-reversible). (7) the refresh port takes a `RotationRequest`, not a placeholder token.
Why: each is a way for a stale client, a typo or a race to produce a wrong state or a lost audit row; fail-closed is the safe direction for a kill switch.
Consequences: override API clients must send `If-Match` when changing a row (Bruno and runbook 05 updated); email hashes written before this change cannot be correlated with new ones (the key changed); `audit_log` already records module PATCH (the interceptor), now pinned. Recorded without a fix: raw user GUIDs in `createdBy` (actor ids are the stamp format by design), the anonymous `access_audit` growth (deferred.md, with the audit retention item).
Who: AI (owner calls D-034 and D-035 set the scope)
Status: accepted
Promote: yes (ADR-008)

## D-035 — W-013 login limiter: strict (IP, email) partition plus a loose per-email ceiling — 2026-11-26
WS: WS-13
Context: S-M3. The per-email limiter charged every attempt, so an attacker could hold a victim at 429 from any IP (a lockout lever, the thing D-020 refused to build).
Decision: three limiters, all evaluated before password verification and chained so a request must pass each: per IP (unchanged), strict per (client IP, canonical email), and a loose per-email ceiling (default 50 per 15 minutes, configurable) that only unsuccessful attempts charge. A throttled request is a 429 problem+json with Retry-After. Refresh has no email (the token is the credential), so it keeps only the per-IP policy; there is nothing to partition.
Why: one IP hammering a victim exhausts only its own (IP, email) bucket, so the victim from another IP still signs in; the loose ceiling still bounds a distributed guesser while leaving real headroom.
Consequences: 50+ failed attempts from any mix of IPs push a victim's logins to 429 for the window (accepted trade-off; one IP at the strict rate needs about ten minutes of sustained abuse, and the victim's own good logins never charge the ceiling). Partition state is in memory per process.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-034 — W-013 customer email is unique per owner — 2026-11-26
WS: WS-13
Context: C3 / S-M1. A global unique index on customers.email made create a cross-owner existence oracle (409 vs 201) for restricted callers.
Decision: unique on (user_id, canonical email) for owned rows, plus a partial unique index on email `WHERE user_id IS NULL` for unowned (staff-created) rows. Customers are hard-deleted, so every row counts. The 409 `customer.email_taken` stays but can only fire inside the target owner's scope; a CUSTOMERS_ALL caller creating for another owner can still hit it (they see everything anyway).
Why: per-owner uniqueness removes the oracle without a generic-response trick; two users can legitimately hold the same contact.
Consequences: migration `CustomerEmailPerOwner` swaps the index (Down restores the global index and fails if per-owner duplicates exist by then). Deleting a user sets their customers' owner to NULL (FK SET NULL) and could collide with an unowned row; users are not hard-deleted today.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-033 — W-012 retention and archival of the audit log are deferred — 2026-11-26
WS: WS-12
Context: an append-only table grows forever; the owner asked for the log, not for its lifecycle.
Decision: no retention, archival or partitioning in W-012; the revive trigger is in `docs/archive/deferred.md`.
Why: partitioning by month plus a detach policy is a separate migration and an operational decision (how long, where to).
Consequences: the table is unbounded until that thread opens; the append-only trigger must be reconsidered then (a retention job needs a controlled bypass).
Who: AI (owner scoped it out in the brief)
Status: accepted
Promote: no

## D-032 — W-012 grant_audit and access_audit stay specialised — 2026-11-26
WS: WS-12
Context: three audit tables risk overlap.
Decision: `audit_log` = data changes plus command outcomes; `access_audit` = authN/authZ events; `grant_audit` = privilege changes. They stay separate and `audit_log` excludes them.
Why: different shapes (hashed email, subject and target) and different write guarantees (best effort for anonymous callers); folding would churn S3's tests and let a login flood into the data log.
Consequences: a grant leaves one `grant_audit` row and one `audit_log` row per changed entity (two views of one change).
Who: AI
Status: accepted
Promote: yes (ADR-010)

## D-031 — W-012 read API behind AUDIT_READ, admin only, keyset paged — 2026-11-26
WS: WS-12
Context: the log must be queryable by who, what, which row, when and which request.
Decision: `GET /api/v1/audit` (`[Module("AUDIT")]`, `[HasPermission("AUDIT_READ")]`), exact-match filters, newest first, cursor over `(occurred_at, id)`. `AUDIT_READ` is granted to `ADMIN` only; staff do not get it (an override can grant it).
Why: a row shows other users' data; least privilege is the safe default.
Consequences: no write verb exists; the response never un-masks (masking is at write time); tenant filtering waits for the ADR-003 toggle.
Who: AI (owner asked for the permission; "staff read?" was left open)
Status: accepted
Promote: yes (ADR-010)

## D-030 — W-012 safety knobs for the audit log — 2026-11-26
WS: WS-12
Context: an audit table is a PII and secret magnet.
Decision: mask at write time (built-in `PasswordHash`, `TokenHash`, `SigningKey`, a configurable list, `[AuditMask]`, `[NoAudit]`), partial-mask email and phone by default, bound `changes` with `MaxChangesBytes` and a `_truncated` marker, optional client IP, per-entity exclusions, never sample, refuse `Audit:Enabled=false` outside Development.
Why: nothing sensitive is stored, so no read path can leak it; the size bound keeps one bad row from bloating the table.
Consequences: a masked-field change shows as `*** -> ***` (it changed, the value is not recorded); partially masked emails cannot be searched by value.
Who: owner (requirements) and AI (defaults)
Status: accepted
Promote: yes (ADR-010)

## D-029 — W-012 failed commands are recorded by AuditBehavior — 2026-11-26
WS: WS-12
Context: a command that fails with a business error changes nothing, so the interceptor never sees it.
Decision: `AuditBehavior` (order Logging, Audit, Validation) writes ONE failure row with the error code in one insert on its own connection and transaction (it survives a surrounding rollback), and opens the ambient scope the interceptor reads; successes stay with the interceptor so there are no double rows. Authentication commands carry `[NoAudit]` (their record is `access_audit`).
Why: a tracked context may be dirty after a failed save and a surrounding transaction may be aborted; an independent plain insert depends on neither. Validation failures are audited because the behavior wraps validation.
Consequences: a failing audit write is logged, never surfaced; failure rows are best effort, success rows are transactional.
Who: owner (requirement) and AI (placement)
Status: accepted
Promote: yes (ADR-010)

## D-028 — W-012 audit rows are written in the same transaction as the change — 2026-11-26
WS: WS-12
Context: an audit row that can outlive a rolled-back change, or be missing from a committed one, is worse than none.
Decision: an EF `SaveChangesInterceptor` adds the rows to the same SaveChanges; Dapper writes call `IAuditTrail` through `SqlExecutor` inside the same `IUnitOfWork` transaction (or a short one). A fitness rule makes every raw SQL write call `IAuditTrail` or sit on a baseline.
Why: atomicity without a second transaction; the change tracker already holds old and new values.
Consequences: a new Dapper write path fails the build until it records its audit row.
Who: owner (requirement) and AI
Status: accepted
Promote: yes (ADR-010)

## D-027 — W-012 audit_log table shape, append-only trigger, indexes — 2026-11-26
WS: WS-12
Context: one general, rich audit table on the S3 seam.
Decision: columns for who, what, change (jsonb), context and metadata; CHECKs on `actor_type` and `outcome`; a trigger rejects UPDATE, DELETE and TRUNCATE; a btree per filter ending in `occurred_at DESC, id DESC`; no BRIN.
Why: the trigger stops the app and accidents (not the table owner; a non-owner role is deferred); the index shape serves the keyset order directly.
Consequences: rows are never edited, so a wrong masking rule cannot be fixed in place (fix forward).
Who: owner (requirements) and AI
Status: accepted
Promote: yes (ADR-010)

## D-026 — S3 base entity contract: IAuditable everywhere, append-only allowlist — 2026-11-25
WS: WS-05
Context: the owner wants one base contract for every entity.
Decision: every mutable entity (UAM tables, Customer, tenants, groups, refresh tokens) implements `IAuditable`; append-only audit rows use `occurred_at` plus an actor and sit on a shrink-only allowlist; a fitness function enforces it. A rich general `audit_log` is the next batch; the audit tables share the `IUnitOfWork` transaction and read `ICorrelationContext` as the seam.
Why: one stamping path, no entity forgotten.
Consequences: `tests/Fugue.ArchTests/Baselines/append-only-entities.txt`.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-025 — Customers PUT, suspend, reactivate answer 204 + ETag — 2026-11-25
WS: WS-05
Context: mutations returning a full body invite stale reads; `SendNoContent<T>` exists (4e8ee4e).
Decision: Customers `PUT`, `suspend`, `reactivate` return 204 with the new ETag; DELETE already 204. Clients GET to read the result.
Why: owner preference; the ETag header still chains If-Match.
Consequences: integration tests, Bruno scripts and runbook 04 updated.
Who: owner
Status: accepted
Promote: no

## D-024 — S3 scope: tenants, refresh tokens, groups, grant_audit, access_audit built basic — 2026-11-25
WS: WS-05
Context: the owner moved the previously deferred UAM v3 pieces into S3 as minimal versions, sliced after the core security slice.
Decision: tenants (table, nullable `users.tenant_id`, NO query filter); refresh tokens (`refresh_tokens`, SHA-256 hash only, family_id, rotation, reuse revokes the family, `/auth/refresh`, `/auth/logout`, rate limited); groups, group_members, group_roles (effective = user roles + group roles, then overrides); grant_audit (same transaction as the grant) and access_audit (login, refresh, reuse, 403, module-disabled).
Why: owner call; each is small once the schema exists.
Consequences: removed from `docs/archive/deferred.md`; if time runs out the build stops after groups.
Who: owner
Status: accepted
Promote: yes (ADR-008)

## D-023 — Module management: config kill switch plus runtime registry — 2026-11-25
WS: WS-05
Context: the owner wants toggleable modules.
Decision: `Modules:<CODE>:Enabled` (config, default true) is the hard switch; the `modules` table `is_active` is the runtime switch; effective = both. `[Module("CODE")]` on feature controllers + an action filter -> 404 `module.disabled`; `GET /modules`, `PATCH /modules/{code}` (`MODULES_MANAGE`, If-Match); disabled modules' permissions are not effective; `SYSTEM` cannot be disabled.
Why: a kill switch operators control without a deploy of data, plus a runtime toggle with audit.
Consequences: fitness: every feature controller has exactly one `[Module]`.
Who: owner
Status: accepted
Promote: yes (ADR-009)

## D-022 — S3 abuse controls and key hygiene (Codex 2, 10) — 2026-11-25
WS: WS-05
Context: Codex REJECT findings 1, 2, 5, 6, 10.
Decision: create binds the owner to the caller without effective `CUSTOMERS_ALL`; built-in rate limiter on token and refresh; email change staff-only; one canonical email key with an active-row unique index; signing key = base64 of at least 32 random bytes, placeholders refused at startup.
Why: each closes a reviewed hole with a test.
Consequences: see `docs/plans/s3-identity.md` review resolutions.
Who: review (Codex), owner accepted
Status: accepted
Promote: yes (ADR-008)

## D-21 — S3 deferrals and exclusions from UAM v3 — 2026-11-25
WS: WS-05
Context: the owner wants a lean port.
Decision: Not built in S3: SSO columns, tenant query filter (ADR-003 toggle stays off), domain tables, user and role CRUD, password reset, lockout, RS256, permission cache. Refresh tokens, tenants, groups, grant_audit and access_audit moved INTO S3 by D-024. Rest parked in `docs/archive/deferred.md`.
Why: each is outside the S3 acceptance and the ~3 day horizon.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: no

## D-20 — S3 login failure is uniform; no lockout — 2026-11-25
WS: WS-05
Context: login must not reveal whether an email exists.
Decision: One `auth.invalid_credentials` for every failure and one hash verify on every attempt (dummy hash when no user); no lockout; the built-in rate limiter (by IP and by canonical email, before verification, 429 + Retry-After) is IN S3 (Codex finding 2).
Why: removes the enumeration and timing oracle without new state or a lock-out-a-victim vector.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-19 — S3 ownership by repository scoping, 404 on cross-owner — 2026-11-25
WS: WS-05
Context: self-service users see only their own record while Application has no ASP.NET dependency.
Decision: `CUSTOMERS_ALL` widens scope; handlers build an `OwnerScope` from `ICurrentUser`; repositories filter; a miss is NotFound.
Why: filters lists, leaks nothing, keeps layering; `IAuthorizationService` resource handlers fail all three.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-18 — S3 permissions by uncached lookup in OnTokenValidated — 2026-11-25
WS: WS-05
Context: permissions must be revocable faster than the token TTL without bloating the token.
Decision: Effective permissions are one indexed DB query per authenticated request in `OnTokenValidated` (once per request, can fail the ticket), added as in-memory claims. NO cache in S3; HybridCache is S5. Supersedes the first draft (60 s cache), rejected by Codex findings 3-4.
Why: immediate revocation, no invalidation races, expiry boundaries exact.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-17 — S3 lean UAM v3 port; precedence deny > allow > role — 2026-11-25
WS: WS-05
Context: the owner's UAM v3 design is the model; the owner overrode the earlier recommendation to defer per-user overrides.
Decision: Port users, modules, resources, actions, permissions, roles, role_permissions, user_roles, user_permission_overrides (with `expires_at`); precedence deny override > allow override > role grant; overrides are their own, last-cut slice.
Why: precedence was unwritten in v3; the written rule and expiry close the audit gaps.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-16 — S3 JWT bearer, HS256, local token endpoint, refresh in S3 (D-024) — 2026-11-25
WS: WS-05
Context: Fugue needs authentication that Bruno and Testcontainers can use.
Decision: JwtBearer 8.x, HS256 with a 32-byte-minimum key from env, 15 min TTL, 30 s skew, `POST /api/v1/auth/token`, `PasswordHasher<T>`; refresh tokens deferred. CLAUDE.md says Argon2id but `PasswordHasher<T>` is PBKDF2: owner to amend the wording.
Why: one process is issuer and verifier, so an asymmetric key buys nothing yet.
Consequences: see `docs/plans/s3-identity.md`.
Who: AI, reviewed by Codex (REJECT, 10 findings), owner accepted
Status: accepted (revised after the Codex plan review; owner accepted all 10 findings)
Promote: yes (ADR-008)

## D-015 — Commit gates and the safe-command guard — 2026-11-25
WS: WS-08
Context: the owner's two sibling repos learned the hard way that hooks and prompts do not hold a rule; machine checks do.
Decision: `commit-msg` runs `check-refs-trailer` (agent commits need a resolvable `Refs:`) and `check-fix-has-test` (a `fix` touching `src/` touches `tests/` or says `Tests: none (reason)`); a project-level PreToolUse hook (`scripts/safe-command-guard.mjs`) blocks force push, `--no-verify`, hard reset on main, stray `rm -rf`, `curl | sh`, secret printing and an undry-run `ef database drop`.
Why: each rule matches a failure already seen elsewhere; the guard is narrow because a false positive trains people to ignore it.
Consequences: human commits are exempt from the refs gate; hooks stay client-side, so CI remains the real backstop when added.
Who: owner+AI
Status: accepted
Promote: yes (ADR-007)

## D-014 — Codex review gate is advisory, seam-triggered, capped — 2026-11-25
WS: WS-08
Context: cross-family review is valuable on critical seams but quota is finite and an LLM deciding "should I review" gets skipped.
Decision: `scripts/codex-gate.mjs` runs at pre-push, decides from the diff alone (seam globs), dedupes by diff hash, caps at 8 launches per day, runs detached, and never blocks or delays the push. Findings land in `docs/reviews/codex/`. It reads Codex output, never its exit code.
Why: the trigger is a pure function of the diff; non-blocking keeps the owner's override cheap.
Consequences: an override on quota is recorded in the commit or handoff; the gate is advisory, so the review gate matrix in `AGENTS.md` still requires an independent final-diff review.
Who: owner
Status: accepted
Promote: yes (ADR-007)

## D-013 — Beads is a local queue; commits cite only committed ids — 2026-11-25
WS: WS-08
Context: `bd init` rewrites `core.hooksPath`, can add a Dolt remote, and committed its own config on this repo.
Decision: `.beads/` is gitignored; nothing beads-related is committed. A `Refs:` trailer may cite only `W-NNN`, `D-NNN` or `ADR-NNN`. `core.hooksPath` stays unset.
Why: a public repo must not depend on a local database, and an id a reader cannot resolve is noise.
Consequences: beads ids appear only in local notes. See `docs/failure-modes.md`.
Who: owner
Status: accepted
Promote: yes (ADR-007)

## D-012 — Pin every catch — 2026-11-25
WS: WS-08
Context: review catches that are only fixed come back.
Decision: no High or Medium finding closes without a regression test, an architecture test or a type that makes it unrepresentable. Recorded in `AGENTS.md`; the review skill enforces the loop.
Why: a catch you do not pin is a bug you rent.
Consequences: reviewer rounds repeat until no High or Medium remains and each has a pin.
Who: owner
Status: accepted
Promote: no

## D-011 — Fable architecture critique: accepted, with two challenges — 2026-11-25
WS: WS-02
Context: an external review of the S0 plan (see `docs/ai-journal.md`).
Decision: accepted nearly all of it (Dapper deferral was later overridden, D-004); kept `SagaId` alongside W3C trace context; kept runbooks.
Why: the critique was mostly right; the exceptions concern what the owner needs to demonstrate.
Consequences: ADR-005 keeps three correlation layers.
Who: review, owner accepted with two challenges
Status: accepted
Promote: no

## D-010 — OrbStack as the container runtime — 2026-11-25
WS: WS-02
Context: Docker on macOS for Postgres and Testcontainers.
Decision: OrbStack.
Why: least setup friction; fastest in the comparison.
Consequences: Testcontainers needs `DOCKER_HOST` pointed at the OrbStack socket (runbook 03).
Who: AI proposed, owner accepted
Status: accepted
Promote: no

## D-009 — `MapStatus` is the owner's — 2026-11-11
WS: WS-02, WS-09
Context: the ErrorType-to-HTTP-status mapping is the one policy call worth the owner's own hands.
Decision: the method stayed the owner's call. Resolved 2026-11-25: the owner asked for it and approved the AI-written exhaustive switch (commit 4e8ee4e); `ErrorType.Unavailable` -> 503 added.
Why: ownership of the decision is the point.
Consequences: no known-red tests remain; every named ErrorType must be mapped (build breaks otherwise).
Who: owner
Status: accepted
Promote: no

## D-008 — Name: Fugue — 2026-11-11
WS: WS-01
Context: a project name.
Decision: "Fugue" (independent voices over one subject).
Why: brand-only theming; domain types keep plain names.
Consequences: the name appears in namespaces and docs, not in domain types.
Who: AI proposed, owner accepted
Status: accepted
Promote: no

## D-007 — Clean-room rewrite after the template audit — 2026-11-11
WS: WS-01
Context: six parallel Haiku agents audited a previous template; three claims were spot-checked by hand and all were confirmed.
Decision: copy patterns, not files; the old code had committed secrets and a foreign domain.
Why: agent claims are cheap; trust is earned by checking.
Consequences: the spot-check habit is recorded in `docs/ai-journal.md`.
Who: AI audit, owner spot-checked
Status: accepted
Promote: no

## D-006 — MVC controllers over `ApiControllerBase`; Mediator not MediatR — 2026-11-11
WS: WS-01
Context: controller style and mediator library.
Decision: MVC controllers with an abstract `ApiControllerBase` (no generic CRUD controller, no FastEndpoints); source-generated `Mediator` (MIT) because MediatR v13+ is commercial. See ADR-002.
Why: the project exists to practise the plumbing, so keep it visible; avoid licence risk.
Consequences: actions are one-liners; logic lives in handlers.
Who: AI proposed, owner accepted
Status: accepted
Promote: yes (ADR-002)

## D-005 — Shared database with `tenant_id`, no RLS — 2026-11-11
WS: WS-01
Context: the owner overrode the AI's row-level-security proposal. Superseded by D-002 (tenancy becomes a toggle, ADR-003).
Decision: shared database with a `tenant_id` column only (ADR-001).
Why: RLS adds operational weight the project does not need.
Consequences: ADR-001 is superseded, not deleted.
Supersedes: none. Superseded by: D-002.
Who: owner (overrode AI's RLS proposal)
Status: superseded by D-002
Promote: yes (ADR-001)

## D-004 — EF Core reads, Dapper writes, one shared transaction — 2026-11-25
WS: WS-04
Context: an external review suggested deferring Dapper; the owner wants the full toolbox practised.
Decision: EF for reads and xmin-checked updates; Dapper for inserts, upserts and set-based writes; Dapper always on EF's connection and current transaction via `IUnitOfWork`; audit stamped on both paths (ADR-006).
Why: each tool where it is strongest, without losing optimistic concurrency or atomicity.
Consequences: the S4 outbox writes through the unit of work; Dapper never reads aggregates.
Who: owner (overrode a Fable review)
Status: accepted
Promote: yes (ADR-006)

## D-003 — Three-layer correlation: trace, correlation id, saga id — 2026-11-25
WS: WS-03
Context: need to debug across logs, responses and later outbound webhooks; a review suggested dropping the saga id.
Decision: W3C `Activity` trace per hop, `X-Correlation-Id` per request chain, `X-Saga-Id` per business process (ADR-005); validated inbound, echoed outbound, in logs and Activity tags. Behaviors require FluentResults responses (`ResultBase, new()`).
Why: each id answers a different question; validation blocks header and log injection.
Consequences: S4 outbox and webhooks must carry the ids as headers.
Who: owner, from a review suggestion challenged
Status: accepted
Promote: yes (ADR-005)

## D-002 — Retarget .NET 8, adopt FluentResults, single-tenant default — 2026-11-25
WS: WS-02
Context: scaffold targeted .NET 10 with a hand-rolled Result type and multi-tenant e-commerce framing; an external review and the owner's interview context changed the goals.
Decision: target .NET 8 (EOL check disabled on purpose, NuGet audit kept); FluentResults with our `ErrorType` kept (ADR-004); single-tenant by default with a `Tenancy:Enabled` toggle (ADR-003); add an Application layer; `MapStatus` stays the owner's.
Why: CV/interview alignment, ecosystem familiarity, domain-neutral template; reasons in the ADRs and `docs/ai-journal.md`.
Consequences: ADR-001 is superseded; Application must not reference ASP.NET Core (arch test).
Supersedes: ADR-001 (not a D-entry).
Who: owner (overrode AI on .NET version and Result type)
Status: accepted
Promote: yes (ADR-003)

## D-001 — Adopt the doc spine — 2026-11-11
WS: WS-01
Context: AI sessions lose context between runs; hand-written notes drift.
Decision: keep project memory in split files by lifecycle (now, board, why, open, journal, handoff), joined by a thread key and checked by a script.
Why: each file has one job and every link can be machine-checked.
Consequences: every wrap updates the spine in the order in `hygiene.md`; pushes run `scripts/spine/ledger-check.sh`.
Who: owner+AI
Status: accepted
Promote: no
