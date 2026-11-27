# Lane readability-files status

Branch `rd-files`, base ce403cb. Three commits, all with `Refs: W-007, D-055`.

## Commits
1. 78f8fa9 `refactor(api): list filters bind one query record`: `AuditListFilter` (new `Contracts/AuditRequests.cs`), `CustomerListFilter` (`CustomerRequests.cs`), `WebhookDeliveryListFilter` (`WebhookRequests.cs`); actions are one-liners with `filter.ToQuery()`. Backlog #005 done.
2. ac4d231 `refactor(domain): typed ids and small aggregate types in one file per concept`: 23 ids into `Ids/Ids.cs`; `ClaimStatus`, `LineOfBusiness`, `ClaimNumber`, `Market`/`Markets` into `Claims/ClaimTypes.cs`.
3. f01fadd `refactor(application): ports grouped by concern`: `AuditPorts.cs` (access + grant audit writers), `MessagingPorts.cs` (outbox, idempotency store), `RuntimePorts.cs` (unit of work, correlation context, module config).

## Counts (src, excluding Migrations)
- Files: 288 before,      260 after.
- Files under 20 lines: 62 before, 53 after (script count; includes the new grouped files, none under 20).
- Tests: Fugue.Tests 618 passed, Fugue.ArchTests 37 passed, 0 failed. Build 0 warnings, 0 errors. `dotnet format --verify-no-changes` clean. `git diff --exit-code docs/api/openapi.v1.json` clean after `npm run api:openapi` (SNAPSHOT-IDENTICAL).

## Rule for small aggregate types (item 2)
When an aggregate folder holds two or more small enums or value types, they share `<Aggregate>Types.cs`; a lone one (`CustomerStatus`) stays in its own file. Applied to Claims only; Customers has just one.

## Deviations and notes
- The filter properties carry `[FromQuery(Name = "...")]` with the old camelCase names. Without it, binding a record yields PascalCase parameter names (the existing `ClaimListFilter` shows `Limit` in the snapshot) and the snapshot would change. Claims was left as is.
- Property doc comments were not needed: the old inline parameters had no descriptions, so none were added.
- Ports: IAuthPorts, Audit.cs and feature-level ports (`IGroupAdmin`, `IMeReader`, ...) were not touched; the feature ones belong to their slices.
- No arch test encodes one-type-per-file.
- `CLAUDE.md` "Adding a strongly-typed ID" says to copy `Ids/CustomerId.cs`; that file no longer exists, so the lead should point it at `Ids/Ids.cs` (append a struct). Also the "one type per file" convention line needs the exceptions (ids, `*Types.cs`, `*Ports.cs`).
- The pre-commit `dotnet-format` hook failed spuriously on the first attempt twice (0.0s) and passed on retry. Untracked `pnpm-lock.yaml` and `pnpm-workspace.yaml` appeared in the worktree and are not mine; left uncommitted.

## Not verified
Bruno suite (`npm run api:test`) not run (no running API); the OpenAPI snapshot test plus 618 integration tests cover binding.
