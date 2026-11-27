# Lane nth-refactor status (Refs D-064, backlog #010 item 1, overnight item 12)

Uniform slice layout in `src/Fugue.Application/Features/<Feature>/`: `Commands/`, `Queries/`, `Models/`, `Repos/`,
`Helpers/`. Moves + namespace/using lines only; zero behaviour change.

## Commits
- slice 1: Claims (this commit)

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- ArchTests: Passed 37, Failed 0, Total 37
- OpenApiContractTests: Passed 1/1 (snapshot unchanged; `git status docs` clean)
- Claims + Letters tests: Passed 192, Failed 0

## Shared files changed (exactly)
- `using` lines only in Api/Infrastructure/tests consumers of the Claims types; `src/Fugue.Infrastructure/DependencyInjection.cs`
  two fully qualified `Fugue.Application.Features.Claims.IClaimSearch` names gain `.Repos`;
  `src/Fugue.Infrastructure/Search/MeilisearchClaimSearch.cs` one `using` alias gains `.Repos`.

## Deviations
- none yet (see the final report for the classification rule).

## Next slice
- 2: Auth, Users, Roles, Groups, Overrides, Me.
