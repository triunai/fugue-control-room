# W-012 plan: rich append-only audit log

Critical seam (migration, pipeline, authorization surface): plan first, cross-family review after.

## Goal
One `audit_log` table answers who did what, to which row, with which field-level change, under which request, and whether it worked. It is append-only (trigger), written in the same transaction as the change, and readable through one permissioned keyset-paged endpoint.

## Decisions (D rows, promoted to ADR-010)
| Id | Decision |
|---|---|
| A | Table `audit_log`: UUIDv7 id, `occurred_at` + actor (not `IAuditable`; added to the append-only allowlist). Columns for actor, action, entity, outcome, `changes` jsonb, request context, `metadata` jsonb; CHECK constraints on `actor_type` and `outcome`. |
| B | Successes are captured by an EF `SaveChangesInterceptor` from the ChangeTracker (Added: new values, Modified: only changed fields as `{old,new}`, Deleted: old values) and added to the SAME SaveChanges. Dapper writes call `IAuditTrail` inside the same transaction (Customer create; seeders). Stamp columns and the `xmin` token never appear in a diff. |
| C | Failures: `AuditBehavior` (pipeline order Logging, Audit, Validation) sets an ambient `IAuditScope` (action, command name) and, when a command returns a failure or throws, writes ONE failure row with `error_code` through one insert on its own connection (the EF tracker may be dirty after a failed save, and a surrounding transaction may be aborted). Successes are never written by the behavior, so there are no double rows. Auth commands opt out with `[NoAudit]` (they belong to `access_audit`; a login flood must not flood `audit_log`). |
| D | Action = `<feature>.<verb>` from the message type (`UpdateCustomerCommand` becomes `customers.update`); without a command (seeders) it falls back to `<table>.<create|update|delete>`. Permission = the `[HasPermission]` codes on the endpoint. Actor display is the user's display name captured once per command. `actor_type`: `user` (authenticated), `system`; `service` is reserved. |
| E | Safety (`Audit:` options): built-in masks PasswordHash, TokenHash, SigningKey plus a configurable list and `[AuditMask]`/`[NoAudit]` attributes; partial masking (Email, Phone, ContactNumber by default); `MaxChangesBytes` truncation with `"_truncated": true`; `CaptureClientIp`; per-entity exclusions (built in: the three audit tables and refresh tokens). Masking happens at write time, so the table and the API can never un-mask. Never sampled. `Enabled=false` fails startup outside Development. |
| F | Indexes: (entity_type, entity_id, occurred_at DESC, id DESC), (actor_user_id, ...), (action, ...), (correlation_id), plus (occurred_at DESC, id DESC) for the unfiltered keyset. `id DESC` is appended so the keyset tiebreak is index-ordered. No BRIN: ordered top-N needs a btree, and a BRIN only pays for huge range scans (revisit with partitioning when retention is built). |
| G | Trigger raises on UPDATE, DELETE (row) and TRUNCATE (statement). Limit: the table owner or a superuser can disable it; production should run the API as a non-owner role (deferred). |
| H | Read API `GET /api/v1/audit`: `[Module("AUDIT")]`, `[HasPermission("AUDIT_READ")]`, seeded and granted to ADMIN only (an audit row shows other users' data). Filters entityType, entityId, actorUserId, action, correlationId, from (inclusive), to (exclusive); newest first; cursor (occurred_at, id). Exact-match filters, parameterized. |
| I | `grant_audit` (privilege changes) and `access_audit` (authN/authZ events) stay as specialised tables: their shapes differ (hashed email, subject/target, best-effort writes) and tests pin them. `audit_log` excludes them to avoid double rows. |
| J | Retention and archival: out of scope, `docs/archive/deferred.md`. |

## Slices (commits, each green)
1. Schema: `AuditLog` entity and id, EF mapping, migration (table, indexes, trigger, AUDIT module/resource/READ action/permission seed), catalog row, pin tests (trigger blocks UPDATE/DELETE/TRUNCATE; schema facts).
2. Capture: `AuditOptions`, redactor, row factory, interceptor, `IAuditTrail` (Dapper), `AuditBehavior`, Customer Dapper path and seeders; tests (rollback, diff, masking, Dapper row, 409 failure row, truncation); fitness rules.
3. Read API, Bruno, ADR-010, runbook 06, failure modes, journal, README, deferred.

## Non-goals
Retention, partitioning, export, tenant filtering of reads (toggle is off), auditing queries, changing the two existing audit tables.

## Stop conditions
A premise is false (for example a Dapper write path I did not find), a gate fails twice, or an existing test needs weakening.
