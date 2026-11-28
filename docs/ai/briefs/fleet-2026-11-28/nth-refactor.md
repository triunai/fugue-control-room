# Lane nth-refactor (item 12, backlog #010 part 1) — Opus — worktree .worktrees/nth-refactor, branch nth-refactor, base afc6fe9
Ports: none needed beyond the test suite. Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 8) first.
You are the ONLY lane allowed in src/ and tests/ while you are live. Behaviour must not change at all.

Objective: every vertical slice in `src/Fugue.Application/Features/<Feature>/` has the same internal layout, so a reader finds
any piece of a feature by its role — a pure, mechanical move with namespaces following folders, proven behaviour-neutral.

Owner's spec (backlog #010 item 1, `docs/state-backlog.md`; verify it): inside every slice: `Commands/`, `Queries/`, `Models/`
(DTOs, outcomes, errors), `Repos/` (the slice's ports, whatever their current name: `I*Repository`, `I*Store`, `I*Reader`,
`I*Admin`, `I*Writer`; today auth ports sit in `Abstractions/IAuthPorts.cs`), `Helpers/` (pure rules such as `ClaimFlow`,
`ClaimScope`), and a per-slice DI extension ONLY where the slice has something to register (most wiring is Infrastructure's;
Mediator handlers are source-generated — do not invent empty extensions). Namespaces follow folders. FitnessTests derive audit
names from `Features.<Feature>`, which must survive. Current example: `Features/Claims/` has ClaimDto.cs, ClaimErrors.cs,
ClaimFlow.cs, ClaimOutcome.cs, ClaimReportDtos.cs, ClaimScope.cs, Commands/, IClaimReports.cs, IClaimRepository.cs,
IClaimSearch.cs, Queries/. Slices: Audit, Auth, Claims, Customers, Diagnostics, Groups, Import, Letters, Me, Modules, Overrides,
Roles, Users, Webhooks (verify the list on your base).

Rules:
- Moves + namespace/using updates ONLY. No renames of types, no signature changes, no logic edits, no reformatting beyond what
  the move needs. `git mv` so history follows. Infrastructure/Api keep their layout (only their `using`s change).
- Use tooling, not hand edits, where possible: move files, then fix namespaces with
  `dotnet format analyzers --diagnostics IDE0130 --severity info` (or an IDE0130 code-fix equivalent) and `dotnet build` until 0
  errors / 0 warnings. If IDE0130 is not enabled for the solution, enable it in `.editorconfig` for `src/**` only if that is the
  house convention (check), otherwise fix namespaces with a scripted sed and say so.
- Serialized/persisted names must not change: check `[JsonPropertyName]`, Mediator generated names, audit `action` strings
  (derived from `Features.<Feature>`), SQL caller headers (`-- <what>. Caller: <Class>.` — class names unchanged, fine), and the
  OpenAPI snapshot: it MUST be byte-identical after the move (`npm run api:openapi` passes with no snapshot diff). If schema ids
  derive from namespaces and change, STOP and report.
- ArchTests must stay green; if a rule pins a namespace, update the rule's path only, keeping its meaning, and say so.

Acceptance (binary): `dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean; ArchTests 37/37 (or more); full suite
under the lock green with the SAME test count as base; OpenAPI snapshot unchanged (`git diff --stat` on docs/api shows nothing);
`web` untouched; a tree listing of every slice in the report showing the uniform layout; `git log --follow` works on a moved file.
Commits: one per slice group (e.g. 1 Claims, 2 Auth+Users+Roles+Groups+Overrides+Me, 3 Audit+Modules+Webhooks+Customers+
Diagnostics+Import+Letters), each building green; then 4 ArchTests/doc path updates if any (ARCHITECTURE.md/CLAUDE.md "Slice
shape" text is spine-adjacent: propose the new text in the report, the integrator applies it).
Non-goals: IOptions (item 13), Worker host / building-blocks project (item 14), any renaming, any behaviour, any web change.
Stop if: the OpenAPI snapshot or any persisted/audited name would change; or the suite count drops.

Recite: uniform slice folders, namespaces follow folders, zero behaviour change, OpenAPI identical, suite count identical.
