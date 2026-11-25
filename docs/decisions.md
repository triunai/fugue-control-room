# Fugue — decisions (why it is this way)

Latest first. Add an entry when a call settles. To change a decision, add a new entry that supersedes it. Never delete.

Heading form: `## D-NNN — <title> — <YYYY-MM-DD>`. NNN is three digits. Only the orchestrator mints numbers.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)` for decisions older than the rule. Never guess.
Then: Context (one or two lines) · Decision · Why · Consequences · Supersedes (if any).

## D-004 — EF Core reads, Dapper writes, one shared transaction — 2026-11-25
WS: WS-04
Context: an external review suggested deferring Dapper; the owner wants the full toolbox practised.
Decision: EF for reads and xmin-checked updates; Dapper for inserts, upserts and set-based writes; Dapper always on EF's connection and current transaction via `IUnitOfWork`; audit stamped on both paths (ADR-006).
Why: each tool where it is strongest, without losing optimistic concurrency or atomicity.
Consequences: the S4 outbox writes through the unit of work; Dapper never reads aggregates.

## D-003 — Three-layer correlation: trace, correlation id, saga id — 2026-11-25
WS: WS-03
Context: need to debug across logs, responses and later outbound webhooks; a review suggested dropping the saga id.
Decision: W3C `Activity` trace per hop, `X-Correlation-Id` per request chain, `X-Saga-Id` per business process (ADR-005); validated inbound, echoed outbound, in logs and Activity tags. Behaviors require FluentResults responses (`ResultBase, new()`).
Why: each id answers a different question; validation blocks header and log injection.
Consequences: S4 outbox and webhooks must carry the ids as headers.

## D-002 — Retarget .NET 8, adopt FluentResults, single-tenant default — 2026-11-25
WS: WS-02
Context: scaffold targeted .NET 10 with a hand-rolled Result type and multi-tenant e-commerce framing; an external review and the owner's interview context changed the goals.
Decision: target .NET 8 (EOL check disabled on purpose, NuGet audit kept); FluentResults with our `ErrorType` kept (ADR-004); single-tenant by default with a `Tenancy:Enabled` toggle (ADR-003); add an Application layer; `MapStatus` stays the owner's.
Why: CV/interview alignment, ecosystem familiarity, domain-neutral template; reasons in the ADRs and `docs/ai-journal.md`.
Consequences: ADR-001 is superseded; Application must not reference ASP.NET Core (arch test).
Supersedes: ADR-001 (not a D-entry).

## D-001 — Adopt the doc spine — 2026-11-11
WS: WS-01
Context: AI sessions lose context between runs; hand-written notes drift.
Decision: keep project memory in split files by lifecycle (now, board, why, open, journal, handoff), joined by a thread key and checked by a script.
Why: each file has one job and every link can be machine-checked.
Consequences: every wrap updates the spine in the order in `hygiene.md`; pushes run `scripts/spine/ledger-check.sh`.
