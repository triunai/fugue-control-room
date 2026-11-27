# How we use AI on Fugue

An honest account of the operating model, with evidence. Every practice is tagged **LIVE in Fugue** (a script, hook, test or file in this repo enforces or contains it) or **practiced in my other repos** (I run it there; Fugue does not yet carry the machinery). Nothing here is claimed beyond that.

## 1. Operating model

| Role | Model | Used for | Status |
|---|---|---|---|
| Lead and integrator | Claude Opus | contracts, dispatch, reconciling lanes, landing, critical seams | LIVE in Fugue (this repo was built by a lead session) |
| Builder | Claude Sonnet | one lane per worktree, standard slices | LIVE in Fugue (W-018 audit import, idempotency, the claims backend, the claims console and the review fix rounds; briefs in `docs/ai/briefs/`, lane reports in `docs/ai/lanes/`) |
| Sweeps | Claude Haiku | bounded mechanical work and audits; the template audit used six Haiku agents | LIVE in Fugue (audit, [journal](../ai-journal.md)) |
| Adversarial architecture review | Fable | plan critique before code | LIVE in Fugue ([journal](../ai-journal.md): "Fable architecture critique") |
| Cross-family reviewer | Codex | seam diffs and plans, read-only | LIVE in Fugue: ten reviews in `docs/reviews/codex/`, each with a written disposition; findings are verified against the code before they are folded (D-056 accepted three of five and declined two with reasons) |

Scale, from the 30 days before this was written: about 2,400 commits in my multi-tenant SaaS repo (Opus co-authored 1,645, Sonnet 459, Haiku 23), and 489 commits, 460 of them AI co-authored, in my mobile app repo. Those numbers are the sibling repos' history, not Fugue's. Fugue's own history is in `git log` (about 130 commits by the claims window, most AI co-authored and each carrying a `Refs:` trailer).

Model choice is routing, not loyalty: expensive models where a mistake is costly (seams, integration, adversarial review), cheap ones where output is checked mechanically. A proposal to make one expensive model the only reviewer was rejected on cost in the SaaS repo.

## 2. The lifecycle

hydrate, capture, plan, contract, lane, gates, review, land, wrap.

1. **Hydrate**: read `docs/hot-state.md` (the resume point), then the handoff and the board. LIVE.
2. **Capture**: every ask becomes a `W-NNN` row on `docs/workstreams.md` before any work; every call becomes a `D-NNN` the same turn, promoted to an ADR when it changes architecture or a convention (`hygiene.md`, `docs/adr/_template.md`; `adr-check` enforces the link). LIVE.
3. **Plan**: critical seams (migrations, auth, `ApiControllerBase`) get a written plan and an independent read before code (`AGENTS.md` review gate matrix). LIVE as a rule; exercised in S2 (ADR-006).
4. **Contract**: ten-part brief with non-goals, literal commands, stop conditions, and a mandatory "verify the premises" and "What I could NOT verify" (`.claude/skills/agent-task-contract`). LIVE: every Fugue lane was dispatched with one (`docs/ai/briefs/`).
5. **Lane**: one git worktree per write lane from an explicit sha, tracked in `docs/branch-ledger.md`. LIVE (`.worktrees/claims` carried the backend lane, then the console lane and a fix round side by side, each owning disjoint paths and committing by pathspec).
6. **Gates**: secrets scan, `dotnet format`, commit-msg gates, build and tests at push, fitness functions, advisory Codex gate, the PreToolUse guard. LIVE.
7. **Review**: `dual-gate-review` (cross-family correctness plus a strict maintainability rubric), rounds until no HIGH or MED; `migration-retrofit-qa` for EF Core. LIVE as skills; round counts are from my other repos.
8. **Land**: the lead re-runs build, tests and Bruno itself, reconciles each lane against git rather than its report, merges, and runs the gates on the combined tree. LIVE.
9. **Wrap**: `wrap-session`: close finished threads into the project log and CHANGELOG with `close-workstream.mjs`, overwrite hot-state, write the handoff and seed prompt. LIVE.

Doc flow: W row, then one project-log entry copied verbatim into CHANGELOG, row deleted, old entries archived to a cold folder with an index. `commit-link-check` verifies that commits are linked. LIVE.

## 3. Failure to rule

| What went wrong | Rule it produced | Where | Status |
|---|---|---|---|
| A brief's "fact" was already false | Verify-the-premises clause | `agent-task-contract` | LIVE |
| A lane branched far behind a live fix | Explicit base sha at dispatch | `agent-task-contract`, branch ledger | LIVE (rule) |
| Agents in one tree saw half-written files | One worktree per write lane | `agent-task-contract` | LIVE (rule) |
| Codex exited 0 after crashing; a tool exited 0 and did nothing | Read the output, never the exit code | `codex-gate.mjs`, failure mode 9 | LIVE |
| A concurrent lane's staged files rode into my commit | Scope commits by pathspec | `AGENTS.md` | LIVE (norm) |
| `bd init` rewrote hooks and committed its config | `.beads/` ignored, init flags, post-init checks | failure mode 8, D-013 | LIVE (it happened here too) |
| A reviewer ran attack payloads in the main checkout | Payloads only as test input | review skill | practiced in my other repos |
| A landing pushed mid-rebase | Landing guard: clean tree, no rebase | landing rules | practiced in my other repos |
| A Haiku lane deleted a call to action while removing a banned word | Humans review mechanical sweeps; a guarded fix replaced it | audit habit | practiced in my other repos |
| A review catch fixed but not pinned came back | Pin every catch (D-012) | `AGENTS.md`, fitness tests | LIVE |
| Controller-level `[Produces]` clobbered `problem+json`, twice | Pinned content-type tests, failure mode 1 | tests | LIVE |
| Host Postgres shadowed the container; `xmin` cast; Serilog reload; `.slnx`; Mediator generator; EOL warnings | Failure modes 2 to 7, each with a defusing move | `docs/failure-modes.md` | LIVE |
| A rule held only by the prompt got skipped | Make it a gate | commit-msg, guard, fitness tests | LIVE |

## 4. Honest gaps

- **TDD is mostly "pin every catch".** Tests are usually written after a review catch to pin it. RED-first is used for SQL logic and state machines (see `migration-retrofit-qa`), not as a blanket rule. Unit tests are low priority here by owner decision; the script tests for the gates are deliberately small.
- **The Codex gate is advisory and often overridden on quota** in my SaaS repo; overrides are recorded in the commit or handoff. In Fugue the gate exists with seam detection, dedupe and the daily cap under test, but its Codex invocation was taken from `--help` and not executed end to end. Treat it as unproven until a first real seam push; the reviews in `docs/reviews/codex/` were run by hand (`codex exec -s read-only`, failure mode 27).
- **CI is written but has never run.** `.github/workflows/ci.yml` exists; the repo has no remote yet, so it is unproven. Until then hooks are client-side and `--no-verify` skips them (the guard blocks that flag for agents).
- **Fleets here are small.** Fugue ran two or three lanes at a time with one lead; large fleets and review-round statistics are practiced in my other repos, not evidenced here.

## 5. How Fugue closes the sibling repos' gaps

| Gap in the siblings | Fugue |
|---|---|
| No secrets scanner in one repo, none in the other | `scripts/spine/secrets-scan.sh` at pre-commit. LIVE |
| No ADR files; unnumbered decision sections | ADRs from commit one, `D-NNN` index linked to ADR files, `adr-check`. LIVE |
| No `AGENTS.md` | `AGENTS.md` with gates, review matrix and the commit-body norm. LIVE |
| Architecture rules as ad hoc tests | Fitness functions with shrink-only baselines (`FitnessTests.cs`). LIVE |
| Test deletion and skips slip through | `check-test-integrity.mjs` (advisory) plus a "no `Skip =` without `RED:`" rule. LIVE |
| Agent commits not linked to anything resolvable | `Refs:` gate on `W-`/`D-`/`ADR-` ids, plus `commit-link-check`. LIVE |
| Fix commits without tests | `check-fix-has-test.mjs`. LIVE |
| Local tracker ids leaking into history | beads stays local; trailers cite committed ids only. LIVE |

## 6. Fugue's own evidence

Read [`docs/ai-journal.md`](../ai-journal.md). Highlights, all in this repo:

- **Haiku audit with hand spot-checks.** Six parallel Haiku agents audited a previous template; I checked three headline claims by hand (a tenant middleware that ran before authentication, an audit loop that never exits, an unfiltered read) and all three were true. Result: a clean-room rewrite. ("Audit of a previous template.")
- **Fable critique, accepted and challenged.** Most of the plan critique was taken; I kept the saga id and the runbooks against it. ("Fable architecture critique", D-011.)
- **Owner overrides of the AI.** Tenancy: the AI proposed row-level security, I chose a `tenant_id` column, later a toggle (D-005, D-002). Runtime: the AI proposed .NET 10, I kept .NET 8 to match the interview context. Result type: a review said keep the hand-rolled one, I chose FluentResults (ADR-004). Dapper: a review said defer it, I kept it, with two conditions the AI added (ADR-006).
- **The AI's own catch.** An agent found that a controller-level `[Produces]` overwrote the problem content type, and it was pinned by a test.
- **H1 itself.** The AI proposed the harness port; I fixed the ledger shape, kept the review gate advisory, and the AI recorded what it could not verify. ("H1: porting my AI engineering harness.")
