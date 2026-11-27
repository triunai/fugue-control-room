# Lane nth-refactor status (Refs D-064, backlog #010 item 1, overnight item 12)

Uniform slice layout in `src/Fugue.Application/Features/<Feature>/`: `Commands/`, `Queries/`, `Models/`, `Repos/`,
`Helpers/`. Moves + namespace/using lines only; zero behaviour change.

## Commits
- `ce87b25` refactor(application): Claims slice into Models, Repos and Helpers folders
- slice 2: Auth, Users, Roles, Groups, Overrides, Me (this commit)

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- ArchTests: Passed 37, Failed 0, Total 37
- OpenApiContractTests: Passed 1/1 (snapshot unchanged; `git status docs` clean)
- slice 1: Claims + Letters tests: Passed 192, Failed 0
- slice 2: build 0/0, format exit 0, ArchTests 37/37, OpenApiContractTests 1/1; Identity + Audit + Api tests: Passed 302, Failed 0

## Shared files changed (exactly)
- `using` lines only in Api/Infrastructure/tests consumers of the Claims types; `src/Fugue.Infrastructure/DependencyInjection.cs`
  two fully qualified `Fugue.Application.Features.Claims.IClaimSearch` names gain `.Repos`;
  `src/Fugue.Infrastructure/Search/MeilisearchClaimSearch.cs` one `using` alias gains `.Repos`.
- slice 2: `src/Fugue.Infrastructure/DependencyInjection.cs` six fully qualified port names gain `.Repos`;
  `tests/Fugue.Tests/Audit/AuditBehaviorTests.cs` fully qualified `TokenDto`/`AuthErrors`/`GroupErrors` gain `.Models`.

## Deviations
- Auth ports stay in `Application/Abstractions/IAuthPorts.cs`: `AuditPorts.cs` (ClientInfo), Me and Users use them;
  moving them into `Features/Auth/Repos` would make Abstractions depend on a feature slice.

## Next slice
- 3: Audit, Modules, Webhooks, Customers, Diagnostics, Import, Letters.
