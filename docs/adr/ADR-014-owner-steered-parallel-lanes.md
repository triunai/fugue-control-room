# ADR-014 — Owner-steered parallel lanes, verified before merge, run overnight under a loop

- Status: accepted
- Date: 2026-11-28
- Decision entry: D-065 in `docs/decisions.md`

## Context
On submission day the owner reviewed the running console screen by screen and sent a stream of asks (shell, forms, charts,
users, onboarding, sign-in, search, a transaction inspector), often several a minute. One agent doing them in sequence would
finish a fraction; agents doing them unsupervised would merge unverified work into a codebase whose promise is "every done is
reconciled against git and a fresh test run". The owner also needed to sleep while the work continued.

## Decision
One lead agent owns `main` and the spine. Each ask becomes a lane: its own git worktree and branch (`nth-<topic>`, nth = nice to
have), a written brief (goal, files it may touch, rules, gates, commit trailer, what to report), and a model sized to the work
(Opus for API, migration and security work; Sonnet for UI; Haiku for web research). Lanes never push or merge. The lead merges a
lane only after it re-runs the web gates, reads the lane's screenshot, and checks the merged result headless in a browser on the
demo stack; the pre-push hook then runs the full .NET suite before anything reaches GitHub. The owner's ideas are weighed before
they are built ("consider, don't add each time"); the lead records each decision as a D-row and each trap as a failure mode.
Overnight, the lead works a written task list (`docs/plans/overnight-2026-11-28.md`) under `/loop`, compacting its context and
resuming from `docs/hot-state.md`, with a feature cut-off and a clean demo stack before the call.

## Alternatives considered
- One agent, sequential: safest merges, but most asks would miss the deadline.
- Many agents merging their own work: fastest, but two agents edit one assumption and nothing proves the merged whole works
  (failure modes 31 to 35 are exactly those losses).
- Stop at the freeze (D-062): a stable demo, but the owner's review found demo-breaking defects (a dead nav, a dead Open claim
  button, native form popups) that a freeze would have shipped.

## Consequences
Easier: many asks land in parallel; every merge has evidence (gates, screenshot, browser check, full suite at push); the journal,
decisions and failure modes show reviewers how AI work was controlled. Harder: the lead's context is the bottleneck (it compacts
and resumes from hot-state); lanes must branch from a commit that already contains the D-row their trailer cites; worktrees
share `web/node_modules` by symlink and must never run `npm install`. Must stay true: no lane pushes; ledger rows exist for live
lanes; `git log main..<lane>` is empty before a lane branch is deleted.

## Supersedes
None. Builds on D-062 (freeze) and D-064 (the asks this ADR governs).
