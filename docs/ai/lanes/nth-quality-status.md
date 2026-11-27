# nth-quality lane status

Branch `nth-quality`, base `b875f49`. No `src/**` or `web/**` change (`git diff --stat b875f49 -- src web` is empty). Nothing pushed, nothing stashed, no throwaway container left running (`fugue-perf` project removed with its own `down -v`; the default stack was never touched).

## Commits

| Sha | Subject |
|---|---|
| `c3f0918` | test(domain): mutation testing on three targets |
| `853fab0` | test(application): pairwise claim action cases |
| `5a6b39e` | test(perf): k6 scripts for the claims API |
| `42ee9fc` | docs(docs): how the AI harness runs this repo |
| (this file) | docs(docs): nth-quality lane status |

## Validation (copied from output, worktree at `42ee9fc`)

- `dotnet build`: 0 Warning(s), 0 Error(s).
- `dotnet test --no-build`: `Fugue.ArchTests` Passed 37 of 37; `Fugue.Tests` Passed 700 of 700 (was 623 at the base: +12 `ClaimNarrativeTests` cases, +15 `AuditRedactorBoundaryTests`, +5 `IdempotencyKeyHeaderBoundaryTests`, +45 `ClaimActionPairwiseTests` = +77).
- `dotnet format Fugue.sln --verify-no-changes --no-restore`: clean (no output).
- Mutation project (not in the solution): `cd tests/Fugue.MutationTests && dotnet test` Passed 138 of 138 (the pure unit-test files only, no Testcontainers).
- Stryker.NET 4.8.1, `--concurrency 2`, "The final mutation score is": Claim **70.42 % to 94.37 %**; AuditRedactor **68.89 % to 98.89 %**; IdempotencyKeyHeader **92.16 % to 98.04 %**. Committed reports: `docs/quality/mutation/<target>/mutation-report.{html,md}`. Survivors left (4, 1, 1) are equivalent mutants, listed with line numbers in `docs/quality/README.md`.
- PICT 3.7.4: 5 factors, 3 constraints, **42 generated cases**, all run (theory plus 3 table facts = 45 tests, all passing). A planted fault (assigned-officer comparison inverted in `ClaimFlow`, local and reverted) made 4 of the 45 fail.
- k6 v2.4.0, all thresholds passed: queue on :5081 (45 s, 20 VUs) 6 913 requests, p95 21.7 ms queue read / 15.5 ms claim read, 0.00 % failed; submit on a throwaway stack (30 s, 10 VUs) 16 157 requests, p95 26.9 ms submit / 10.1 ms replay, 0.00 % failed, every replay flagged `Idempotent-Replayed`; pickup race (50 races) 50 winners, 0 double wins, 0 races without a winner, 50 losers answered 412.

## Deviations and findings

1. **A dedicated mutation test project, `tests/Fugue.MutationTests/`, not in `Fugue.sln`.** `dotnet-stryker` 4.8.1 has no test-filter option (`--test-case-filter` is "unrecognized"), and with `Fugue.Tests` as the test project it found no direct reference to a mutable project. The new project links the pure unit-test files and references the four `src` projects. Its assembly name is `Fugue.Tests` because `Fugue.Infrastructure` exposes internals (`AuditRedactor`) to that name only. The brief allowed `Fugue.sln` only "to add a test project"; I did not add it, to avoid running the linked tests twice in `dotnet test`. Consequence: `stack-check` and CI do not build it.
2. **Stryker pinned to 4.8.1.** 5.1.0 does not install with the pinned .NET 8 SDK ("Settings file 'DotnetToolSettings.xml' was not found in the package").
3. **The first commit's body says "20 focused test cases"; the real number is 32** (12 + 15 + 5). Commits are not amended; this file is the correction. `docs/quality/README.md` has the right numbers.
4. **`Fugue.Tests.csproj` was touched** (an `<None Include=...>` copying `tests/pict/*` to the output) so the theory can read the committed PICT table; inside `tests/**`.
5. **`docker-compose.yml` cannot start a second project as is**: it pins `container_name` on every service. The throwaway stack used a scratchpad copy of the file without those lines (recipe in `perf/k6/README.md`). Not changed in the repo (outside this lane's allowed files).
6. **Two repo-doc conflicts found while fact-checking the harness doc** (recorded in `docs/ai/harness.md`, not fixed, spine files are the lead's): the context-reset threshold is about 50 % in `docs/conventions/doc-spine.md` section 7.6 but about 60 % in the brief and the lead handoff; and `.githooks/README.md` tells the reader to set `core.hooksPath`, which `AGENTS.md` and `lefthook.yml` forbid.
7. **No production bug found.** No surviving mutant pointed at one. One dead local in `AuditRedactor.TryRedact` (`bool truncated`, assigned and never read) is noted in `docs/quality/README.md`.

## Contract gaps

- None for the API. The `Claim` guards are exercised through the public handlers (`ClaimFlow` is `internal` to `Fugue.Application`, which exposes no internals to the tests), so the pairwise theory cannot call `ClaimFlow.ApplyAsync` directly; going through the handlers covers it.

## What I could NOT verify

- That the default stack on :5081 runs the same commit as this branch (the queue run used it; its build is not recorded). A rerun of `npm run perf:queue` on the integrated tree would settle it.
- Run-to-run variance of the k6 numbers: one run each.
- That the oracle's role-to-permission table matches the seeded roles in the database: it follows `docs/runbooks/11-claims.md` and the controller attributes, and the permission each action needs is read from the controller; the role table itself (`Claimant`, `Officer`, `Manager`) is hard-coded in the test. A test that reads the seeded role grants would settle it.
- CI: the new `tests/Fugue.MutationTests` project and the PICT copy item have not run in CI (no remote).
