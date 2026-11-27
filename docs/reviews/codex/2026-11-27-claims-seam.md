# Codex review: claims seam (state machine, visibility, permissions), 2026-11-27

`codex exec -s read-only -C .worktrees/claims` (gpt family) on `main..claims-core` at 506fc47; prompt asked for state machine, visibility, permissions, concurrency + idempotency, atomicity, money. Read-only; Codex inspected tests but did not run them (the lead ran them: ArchTests 37 + Tests 616, Bruno claims folder 25/25 requests, 29/29 tests).

## Disposition (lead, D-056)
| # | Finding | Verdict | Why |
|---|---|---|---|
| 1 | MEDIUM: same-value reserve skips the xmin check | **Accepted, fixed in the fix round** | Verified: `FindForUpdateAsync` takes no row lock, and an unchanged property means EF sends no UPDATE, so a write that lands between load and save is not detected. Same class as the W-013 no-op override PUT. Fix: every claim command forces the guarded UPDATE; pinned by a two-context test. |
| 2 | MEDIUM: handlers do not check operation permissions | **Declined (by design)** | Authorization is enforced at the HTTP edge for every action and pinned by `Fitness_Auth_every_controller_action_declares_HasPermission_Authorize_or_AllowAnonymous`; handlers use permissions only for data scope (`ClaimScope`, `OwnerScope`). Codex itself found no HTTP bypass. A second transport (a Kafka consumer) would need its own authorizer: noted as a "more time" item. |
| 3 | MEDIUM: info event names differ from plan decision H | **Accepted as a doc fix** | The code's dotted names (`claim.info.requested.v1`, `claim.info.provided.v1`) are forced by the event-name fitness rule (no underscores); plan H amended to match. |
| 4 | LOW: 403 (not 404) for a claimant on staff command routes | **Declined** | The 403 comes from lacking the permission and is identical for every id, existing or not, so it reveals nothing (Codex agrees). "404, never 403" is the rule for callers who hold the permission but not the claim. |
| 5 | LOW: Idempotency-Key optional on submit | **Accepted, fixed in the fix round** | A duplicate claim is real harm (double handling, double payment risk). Claim submit requires the key (400 `idempotency.key_required`); customers and webhook creates stay optional per ADR-013. |

Codex's note on assignment policy (reserve, ask, decide need the assigned officer or a `CLAIMS_SETTLE` holder; settle needs no assignment) is documented in `docs/CLAIMS.md`.

## Codex output (verbatim)

Reviewed `main..claims-core` at `506fc47`, read-only. Tests were inspected, not executed. No illegal aggregate transition found; five findings:

1. **MEDIUM — An unchanged reserve bypasses the database concurrency check.** [ClaimRepository.cs:210](src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs:210)  
   A loads reserve `100`, version X. B changes it to `200` and commits. A sets reserve to `100`, its originally loaded value: EF detects no modification, emits no claim UPDATE, and therefore never checks `xmin`. A returns 200 with B’s value instead of 412.  
   **Minimal fix:** explicitly mark `ReserveMinor` modified for reserve commands, including equal-value writes. Pin with a two-context concurrency test.

2. **MEDIUM — Handlers do not enforce their operation permissions.** [SettleClaim.cs:28](src/Fugue.Application/Features/Claims/Commands/SettleClaim.cs:28), [GetClaimExposure.cs:12](src/Fugue.Application/Features/Claims/Queries/GetClaimExposure.cs:12)  
   Dispatching `SettleClaimCommand(ownApprovedClaim, currentVersion)` through Mediator as SELF_SERVICE settles it despite lacking `CLAIMS_SETTLE`. Exposure/workload handlers likewise return global reports without checking grants. Controller attributes prevent these calls over the current HTTP routes; this is an application-boundary gap, not a demonstrated HTTP bypass.  
   **Minimal fix:** enforce each operation’s permission in handlers or an authorization behavior, retaining owner scoping.

3. **MEDIUM — Info event names differ from decision H.** [ClaimEvents.cs:35](src/Fugue.Domain/Claims/ClaimEvents.cs:35)  
   A valid subscription to `claim.info_requested.v1` receives nothing: the producer emits `claim.info.requested.v1`. The same mismatch exists for `info_provided`. I verified webhook matching uses exact names unless a wildcard is supplied.  
   **Minimal fix:** align producer names with H, or explicitly revise the contract and subscription examples together.

4. **LOW — Invisible claims return 403 on staff command routes.** [ClaimsController.cs:75](src/Fugue.Api/Controllers/ClaimsController.cs:75)  
   SELF_SERVICE calling another claimant’s `/pickup`, `/reserve`, `/info-requests`, `/decision`, or `/settle` gets 403 from authorization before visibility runs, contrary to the requested “every endpoint → 404” contract. Nonexistent IDs also return 403, so this does **not** expose claim existence.  
   **Minimal fix:** use resource-aware authorization that returns 404 for invisible claims before denying the operation.

5. **LOW — Submit’s idempotency key is optional.** [ClaimsController.cs:26](src/Fugue.Api/Controllers/ClaimsController.cs:26)  
   Repeating an identical POST without `Idempotency-Key` creates two claims. `[Idempotent]` explicitly passes requests without the header through, leaving decision C’s stated submit contract unenforced.  
   **Minimal fix:** require the header specifically for claim submission.

Assignment policy deserves explicit documentation: reserve/request/decide require the assigned officer **or anyone holding `CLAIMS_SETTLE`**; settlement requires no assignment match. B–J do not specify this manager override or an assigned-only rule, so I have not classified it as a defect.

Checked and found correct:

- Pickup assigns the caller; info-response round trips, approval bounds, settlement, and terminal-state guards hold.
- Suspected cross-owner read leaks turned out fine: detail, timeline, filtered/keyset lists, and responses apply owner scope; responses force own scope even for admins. Reserve values are redacted.
- Controller permission codes and all four seeded role sets match G.
- Supplied-key replay is actor-scoped; same key with different bytes returns 422.
- One open request is enforced by a partial unique index; claim numbers use an atomic sequence plus a unique index.
- Transition writes share a transaction with timeline, audit, and outbox; timeline triggers reject UPDATE, DELETE, and TRUNCATE.
- Money uses bigint minor units; exposure groups and totals retain currency separation. Raw SQL inputs are parameterized; no applicable single-tenant visibility leak found.