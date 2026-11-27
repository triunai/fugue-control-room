# Lane readability-files (Sonnet builder): one file per concept and filter records (D-055, backlog #005)

## Objective
The source tree shows the shape of the system in fewer, fuller files, and every list endpoint binds one filter record, with behaviour and the public contract provably unchanged.

## Lane facts
Given by the lead in the dispatch prompt: worktree path, branch, base sha, and which other lane runs in parallel.

## Current state (verify; docs rot)
- `src/Fugue.Domain/Ids/` holds about 27 one-struct files (each typed id is a `readonly record struct` with `New()`/`From()`).
- Backlog #005 (`docs/state-backlog.md`): `AuditController.List` stacks nine `[FromQuery]` parameters; `CustomersController.List` and the webhook deliveries list repeat `[FromQuery]` per parameter. The claims list already uses a `ClaimListFilter` record: copy its style.
- Many files under 20 lines (count them: `find src -name '*.cs' -not -path '*/Migrations/*' -not -path '*/obj/*' | xargs wc -l | sort -n | head -60`).

## Desired final state
1. All typed ids in `src/Fugue.Domain/Ids/Ids.cs` (same namespace, same members, same attributes and converters).
2. An aggregate keeps its small enums and value types in its own file (e.g. `ClaimStatus`, `LineOfBusiness` next to or inside `Claim.cs`'s folder file `ClaimTypes.cs`, or in `Claim.cs` itself: pick one rule, apply it everywhere, state it in the report).
3. Application ports grouped by concern (`Abstractions/IdentityPorts.cs`, `AuditPorts.cs`, `MessagingPorts.cs`, ...) where today they are one interface per file and under ~30 lines each.
4. Backlog #005: `AuditListFilter`, `CustomerListFilter`, `WebhookDeliveryListFilter` records in `src/Fugue.Api/Contracts/` bound with one `[FromQuery]`, with `ToQuery()` mapping to the existing Application query; actions become one-liners.
5. A slice (command + validator + handler) stays one file. Do not merge unrelated slices.
6. Namespaces unchanged (moving types between files only), so no `using` churn outside the moved files.

## Allowed files
`src/Fugue.Domain/**`, `src/Fugue.Application/**`, `src/Fugue.Api/**`, tests that only need a path or name update. NOT `src/Fugue.Infrastructure/**` (the readability-sql lane owns it), NOT `Migrations/**`, NOT `web/**`.

## Architecture rules
Arch tests must stay green without being weakened; if a rule encodes "one type per file", report it to the lead instead of deleting it. Do not rename public types or members. `git mv` where a file keeps most of its content, so `git log --follow` traces it.

## Acceptance (binary)
1. `dotnet build` 0 warnings; `dotnet test` all green (counts copied from output).
2. `git diff --exit-code docs/api/openapi.v1.json` (byte-identical contract; regenerate the snapshot with the repo's command and prove no diff).
3. Before/after file counts and the count of files under 20 lines, in the report.
4. `dotnet format --verify-no-changes` clean.

## Validation commands
```bash
dotnet build -c Debug 2>&1 | tail -3
dotnet test --no-build 2>&1 | grep -E "Passed!|Failed!"
git diff --exit-code docs/api/openapi.v1.json && echo SNAPSHOT-IDENTICAL
dotnet format --verify-no-changes && echo FORMAT-CLEAN
```

## Non-goals
Behaviour changes, renames, new abstractions, Infrastructure SQL, the web console, rule files (CLAUDE.md/AGENTS.md: the lead edits them), the doc spine.

## Stop conditions
The OpenAPI snapshot changes and the cause is not obvious in two minutes: revert that step and report. An arch test fails on a move: report with the rule text.

## Commits (pathspec only; trailers `Refs: W-007, D-055` + `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`)
1. `refactor(api): list filters bind one query record`
2. `refactor(domain): typed ids and small aggregate types in one file per concept`
3. `refactor(application): ports grouped by concern`

## Verify the premises
Before acting on any claim here, check it against the repo. If false, say what is true (`file:line`) and proceed on the fact.

## Report
Write `docs/ai/lanes/readability-files-status.md` (commits, counts, before/after file counts, the rule you chose for item 2, deviations, what you could NOT verify), commit it by pathspec, reply ONE line.

## Recite
Fewer, fuller files by concept; one filter record per list; namespaces, behaviour and the OpenAPI snapshot unchanged; Domain/Application/Api only.
