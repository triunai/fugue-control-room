# Lane nth-refactor status (Refs D-064, backlog #010 item 1, overnight item 12)

Uniform slice layout in `src/Fugue.Application/Features/<Feature>/`: `Commands/`, `Queries/`, `Models/`, `Repos/`,
`Helpers/`. Moves + namespace/using lines only; zero behaviour change.

## Commits
- `ce87b25` refactor(application): Claims slice into Models, Repos and Helpers folders
- `01cdd6b` refactor(application): identity slices into Models and Repos folders
- `8280750` refactor(application): remaining slices into Models, Repos and Helpers folders
- slice 4: Features/README.md slice layout text (this commit)

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- ArchTests: Passed 37, Failed 0, Total 37
- OpenApiContractTests: Passed 1/1 (snapshot unchanged; `git status docs` clean)
- slice 1: Claims + Letters tests: Passed 192, Failed 0
- slice 2: build 0/0, format exit 0, ArchTests 37/37, OpenApiContractTests 1/1; Identity + Audit + Api tests: Passed 302, Failed 0
- slice 3: build 0/0, format exit 0, ArchTests 37/37, OpenApiContractTests 1/1; Audit + Webhooks + Import + Letters +
  Application + Api tests: Passed 316, Failed 0

## Shared files changed (exactly)
- `using` lines only in Api/Infrastructure/tests consumers of the Claims types; `src/Fugue.Infrastructure/DependencyInjection.cs`
  two fully qualified `Fugue.Application.Features.Claims.IClaimSearch` names gain `.Repos`;
  `src/Fugue.Infrastructure/Search/MeilisearchClaimSearch.cs` one `using` alias gains `.Repos`.
- slice 2: `src/Fugue.Infrastructure/DependencyInjection.cs` six fully qualified port names gain `.Repos`;
  `tests/Fugue.Tests/Audit/AuditBehaviorTests.cs` fully qualified `TokenDto`/`AuthErrors`/`GroupErrors` gain `.Models`.

- slice 3: `src/Fugue.Infrastructure/DependencyInjection.cs` eleven fully qualified port names gain `.Repos`;
  `src/Fugue.Infrastructure/Search/MeilisearchAuditSearch.cs` one `using` alias gains `.Repos`;
  `src/Fugue.Application/Features/Me/Queries/GetMe.cs` `Modules.IModuleRegistry` -> `Modules.Repos.IModuleRegistry`;
  `src/Fugue.Api/OpenApi/DescriptionText.cs` the example full name in its doc comment gains `.Models`.

## Deviations
- Auth ports stay in `Application/Abstractions/IAuthPorts.cs`: `AuditPorts.cs` (ClientInfo), Me and Users use them;
  moving them into `Features/Auth/Repos` would make Abstractions depend on a feature slice.

## Next slice
- full suite under the lock; report. Web hood paths (7 `*.hood.ts`) need the patch in
  `.worktrees/_briefs/reports/nth-refactor-web-paths.patch` at merge (web is outside this lane).
- `Import/Parsing/` and `Webhooks/Sink/` were extra sub-folders: parsers to `Import/Helpers`, `ImportTypes.cs` to
  `Import/Models`; the dev sink's requests (`DevWebhookSinkAdmin.cs`, `ReceiveDevWebhook.cs`) to `Webhooks/Commands`
  (they are `IRequest`, never audited; the admin file also holds the sink list read, kept whole per D-055) and
  `DevWebhookSinkTypes.cs` (declares the `IDevWebhookSink` port) to `Webhooks/Repos`.
- IDE0130 (namespace matches folder), enabled only for the check and then restored: `dotnet format style --diagnostics IDE0130
  --verify-no-changes --include src/Fugue.Application/` exit 0; with one namespace broken on purpose it exits 2.
