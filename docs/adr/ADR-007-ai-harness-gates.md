# ADR-007 — AI harness gates: local beads, commit gates, advisory cross-model review, safety guard

- Status: accepted
- Date: 2026-11-25
- Decision entry: D-013, D-014, D-015 in `docs/decisions.md`

## Context
AI agents write most commits here. Rules held only by prompts get skipped, so each rule is a machine check at a juncture, and the checks must not depend on tools a reader of the public repo cannot see.

## Decision
- Beads (`bd`) is a LOCAL task queue: `.beads/` is gitignored and no commit references a bead id. `core.hooksPath` stays unset; lefthook is the only hook orchestrator.
- `commit-msg` gates: agent commits (a `Co-Authored-By: Claude` trailer) end with a resolvable `Refs: W-/D-/ADR-` trailer; a `fix(...)` commit touching `src/` touches `tests/` or says `Tests: none (<reason>)`.
- A project-level Claude Code PreToolUse hook (`scripts/safe-command-guard.mjs`) denies a short list of destructive commands.
- A seam-triggered Codex review runs at pre-push as ADVISORY: deduped by diff hash, capped at 8 per day, detached, never blocking.
- Doc lifecycle and ledgers: `docs/workstreams.md` holds live threads only; closed threads move to `docs/project-log.md`; `CHANGELOG.md` is the permanent commit-linked ledger (`scripts/spine/changelog-sync.mjs`).

## Alternatives considered
- Beads ids in trailers (as in the owner's other repos): rejected, unresolvable for any reader of this public repo.
- Blocking cross-model review: rejected, quota is finite and the owner's override must stay cheap.
- A heavyweight hook framework or `core.hooksPath` shims: rejected, two orchestrators conflict.

## Consequences
Hooks are client-side (`--no-verify` skips them, and the guard blocks that flag for agents); CI remains the real backstop once added. The gates have small `node --test` suites run by `npm run test:scripts`.

## Supersedes
None.
