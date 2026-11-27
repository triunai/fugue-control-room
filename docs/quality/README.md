# Quality evidence

Three kinds of evidence beyond the unit and integration suites, each reproducible from this repo: mutation testing (below), pairwise case generation (`tests/pict/`, section "Pairwise"), and load tests (`perf/k6/`, section "Load"). Facts and numbers here are copied from the tool output of the run named.

## Mutation testing (Stryker.NET 4.8.1)

Mutation testing changes the production code one small step at a time (flip a comparison, blank a string, drop a block) and runs the tests: a mutant the tests do not notice is a behaviour nothing pins down.

**Setup.** `dotnet-stryker` 4.8.1 is a local tool (`.config/dotnet-tools.json`; run `dotnet tool restore`). Version 5.1.0 is not usable with the .NET 8 SDK pinned in `global.json` (the package has no `DotnetToolSettings.xml` for it: install fails), so the pin is 4.8.1. The runs use `tests/Fugue.MutationTests/`, a test project that is NOT in `Fugue.sln` (adding it would run its tests twice in `dotnet test`). It compiles, by link, only the pure unit-test files (`ClaimTests`, `ClaimNarrativeTests`, `AuditRedactorTests`, `AuditRedactorBoundaryTests`, `IdempotencyKeyHeaderTests`, `IdempotencyKeyHeaderBoundaryTests`), so the mutation loop never starts a Testcontainers database, and Stryker gets the direct project references it requires. Its assembly name is `Fugue.Tests` because `Fugue.Infrastructure` exposes its internals (`AuditRedactor` is `internal`) to that name only.

**Run** (about 20 to 30 seconds each, `--concurrency 2` from `tests/Fugue.MutationTests/stryker-config.json`):

```
npm run mutation:claim          # src/Fugue.Domain/Claims/Claim.cs
npm run mutation:redactor       # src/Fugue.Infrastructure/Audit/AuditRedactor.cs
npm run mutation:idempotency    # src/Fugue.Api/Idempotency/IdempotencyKeyHeader.cs
```

Reports land in `artifacts/mutation/<target>/reports/` (ignored); the committed copies, from the "after" runs, are in `docs/quality/mutation/<target>/mutation-report.{html,md}`. The markdown summary lists every file of the mutated project; only the target's row has numbers (the rest were filtered out by `--mutate`).

### Scores

Score = killed / (killed + survived + timeout + no coverage), as Stryker prints it ("The final mutation score is ..."). Compile-error mutants are not counted.

| Target | Before | After | Tests added |
|---|---|---|---|
| `Claim` aggregate | 70.42 % (50 killed, 21 survived) | 94.37 % (67 killed, 4 survived) | `ClaimNarrativeTests` (12 test cases) |
| `AuditRedactor` | 68.89 % (62 killed, 18 survived, 10 no coverage) | 98.89 % (89 killed, 1 survived) | `AuditRedactorBoundaryTests` (15 test cases) |
| `IdempotencyKeyHeader` | 92.16 % (47 killed, 3 survived, 1 no coverage) | 98.04 % (49 killed + 1 timeout, 1 survived) | `IdempotencyKeyHeaderBoundaryTests` (5 test cases) |

### What the survivors were

- **Claim (21 before).** The state machine was already pinned. What was not: the words it writes (the claimant-visible timeline notes, the refusal messages), the `amountMinor` field name on amount errors, and "blank location becomes null". All covered now.
- **AuditRedactor (28 undetected before).** Happy paths only: no test pinned the 1024-character cap boundary, the exact byte budget, the partial-mask length rule (five versus six characters), configured partial-mask and excluded-entity lists, or `TryRedact` (the outbox-payload entry point had no direct test).
- **IdempotencyKeyHeader (4 before).** The one-character minimum, the `~` end of the printable range, the `Absent` result's shape, and a null header line.

### Survivors left on purpose (equivalent mutants, no test can tell them apart)

- `Claim.cs` lines 30 to 33: the private constructor's `string.Empty` defaults for `Number`, `Currency`, `PolicyNumber` and `IncidentDescription` are always overwritten by `Claim.Submit`; the constructor exists for EF.
- `AuditRedactor.cs` line 86: the local `truncated` in `TryRedact` is passed to `Redact` and never read (a dead flag, harmless).
- `IdempotencyKeyHeader.cs` line 35: `?? string.Empty` on a null header line; replacing the empty fallback with any other text is still rejected by the grammar that follows.

No surviving mutant pointed at a production bug, so no `src/` change was needed or made.

## Pairwise (PICT 3.7.4)

The model `tests/pict/claims-actions.pict` has five factors: role (3: claimant, officer, manager), claim status (6), action (7: pickup, reserve, request info, approve, reject, settle, respond), assignment (3: none, the caller, another officer) and If-Match (3: current, stale, missing). The full product is 1134 combinations before constraints; three constraints (a claim has an officer exactly once it is picked up; a claimant is never the assigned officer) remove the impossible ones. PICT generated **42 cases** covering every pair of values of any two factors:

```
brew install pict                                              # PICT 3.7.4 was installed this way (not vendored)
pict tests/pict/claims-actions.pict > tests/pict/claims-actions.cases.tsv
```

The generated table is committed (`tests/pict/claims-actions.cases.tsv`, deterministic: no random seed) and copied next to the test assembly by `Fugue.Tests.csproj`; the test needs no PICT installation. `tests/Fugue.Tests/Claims/ClaimActionPairwiseTests.cs` runs each case through the real command handler (so `ClaimFlow`) and the real `Claim` aggregate, with an in-memory repository, and checks the outcome against a small oracle (documented in the class comment: policy, then If-Match 428, then 412, then the assigned-officer rule, then the state machine). The oracle reads the permission each action needs from the `[HasPermission]` attribute on `ClaimsController`, so it is not a copy of the implementation's own table.

Result of the 42 cases by outcome: success, policy refusal (403), 428, 412, `claim.not_assigned_to_you` and `claim.invalid_transition` all occur (the test `The_generated_table_exercises_success_and_every_kind_of_refusal` fails if one stops occurring). As a check that the cases can fail, inverting the assigned-officer comparison in `ClaimFlow.RequireAssignedOrManager` (a temporary local edit, reverted, never committed) made 4 of the 45 tests in the class fail.

Limits: pairwise is not exhaustive (three-way interactions can be missed; run `pict ... /o:3` for more cases); the HTTP layer itself (the 403 and the headers) is covered by `ClaimsWorkflowTests` and the Bruno suite, not by these cases.
