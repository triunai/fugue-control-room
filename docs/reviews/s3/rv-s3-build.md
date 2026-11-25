# S3 build review: dispositions (2026-11-25)

Two independent reads of `580100e^..HEAD`: a Sonnet reviewer (dual-gate Gate A, static read) and Codex (`docs/reviews/codex/2026-11-25-s3-build.md`, read-only). Gate B (maintainability rubric) was applied by the Sonnet reviewer; no giant files or layering leaks were found beyond L4 and L10 below. Author and reviewers are separate passes.

| Id | Source | Sev | Finding | Disposition | Pin |
|---|---|---|---|---|---|
| C1 | Codex | HIGH | A family revoke can miss a token inserted by an in-flight rotation (READ COMMITTED) | FIXED: rotation and revocation take the same transaction-scoped advisory lock on the family | `Family_revocation_waits_for_an_in_flight_rotation_holding_the_family_lock` |
| C5 / L | Codex+Sonnet | LOW | `perm` claims inside a token survive | FIXED: stripped before adding the database set | `Permission_claims_smuggled_into_a_token_are_discarded` |
| C2 | Codex | MED | Concurrent override writes: EF skips unchanged columns, audit can disagree with the row | FIXED: last writer wins, all three columns marked modified | covered by `Setting_again_replaces...`; no deterministic race test |
| S-M2 | Sonnet | MED | Override expiry with a non-UTC offset (suspected 500) | FIXED: normalised to UTC | `An_expiry_with_a_non_utc_offset_is_accepted` |
| S-M4 | Sonnet | MED | Unknown owner id on create is a 500 (FK 23503) | FIXED: 400 validation on `userId` | `An_unknown_owner_id_on_create_is_a_400_not_a_500` |
| C3 / S-M1 | both | MED | Create exposes a global email-existence oracle through the unique index (409 vs 201) for restricted callers | FIXED in W-013 (D-034, owner call): email unique per owner, migration `CustomerEmailPerOwner`; the 409 only fires inside the target owner's scope | `CustomerEmailScopeTests`, `CustomerEmailIndexMigrationTests` |
| S-M3 | Sonnet | MED | Per-email limiter charges successful logins, so an attacker can keep a victim at 429 | FIXED in W-013 (D-035, owner call): strict (IP, email) bucket plus a loose per-email ceiling that only failures charge | `LoginLimiterTests` |
| C4 | Codex | MED | Successful login/refresh can lack an audit row (best-effort writer) | ACCEPTED, documented in ADR-008 (access_audit is best effort by design; grant_audit is transactional); revisit with the rich `audit_log` | n/a |
| S-L1..L10 | Sonnet | LOW | group self-target guard; 23505 races in group/override set; reason_code truncation; HMAC key reuse and fallback; anonymous audit growth; module switch parses only "false" and SYSTEM can be config-disabled; no audit on module PATCH; raw user GUIDs in `createdBy`; `tenants.is_active` unused; refresh placeholder smell | FIXED in W-013 (D-036): L1 group self-target, L2 23505 races, L3 reason_code clip, L4 HMAC subkey, L5 anonymous growth RECORDED in `deferred.md`, L6 fail-closed module config and SYSTEM, L7 module PATCH already audited by W-012 (pinned), L8 raw user ids in `createdBy` RECORDED (stamp format by design), L9 tenant `is_active` enforced, L10 `RotationRequest`. Extra: `OverrideAdmin` If-Match (the W-012 gap) | `AdminHardeningTests`, `OverridesTests` |

No finding was refuted. Not verified by the reviewers: migration SQL (verified by the author on a throwaway container), Bruno, and Npgsql offset behaviour (the author's test passes).
