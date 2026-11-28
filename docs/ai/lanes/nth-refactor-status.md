# Lane nth-refactor status (Refs D-064, backlog #010 item 1, overnight item 12)

Uniform slice layout in `src/Fugue.Application/Features/<Feature>/`: `Commands/`, `Queries/`, `Models/`, `Repos/`,
`Helpers/`. Moves + namespace/using lines only; zero behaviour change.

## Commits
- `ce87b25` refactor(application): Claims slice into Models, Repos and Helpers folders
- `01cdd6b` refactor(application): identity slices into Models and Repos folders
- `8280750` refactor(application): remaining slices into Models, Repos and Helpers folders
- `b7355af` docs(application): slice README describes the uniform folder layout
- `3646d54` fix(web): hood notes follow the moved Helpers paths (OWNERS Addendum 9)
- `d66cc2f` merge of main cc5f135 (no src/tests change on main since afc6fe9)
- final: this status update

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

- slice 5: path strings only in 7 `web/src/app/features/**/*.hood.ts` (17 lines): ClaimFlow, ClaimScope, WebhookSecretFormat -> `Helpers/`.

## Deviations
- Auth ports stay in `Application/Abstractions/IAuthPorts.cs`: `AuditPorts.cs` (ClientInfo), Me and Users use them;
  moving them into `Features/Auth/Repos` would make Abstractions depend on a feature slice.

## Next slice
- none: READY for merge.
- `Import/Parsing/` and `Webhooks/Sink/` were extra sub-folders: parsers to `Import/Helpers`, `ImportTypes.cs` to
  `Import/Models`; the dev sink's requests (`DevWebhookSinkAdmin.cs`, `ReceiveDevWebhook.cs`) to `Webhooks/Commands`
  (they are `IRequest`, never audited; the admin file also holds the sink list read, kept whole per D-055) and
  `DevWebhookSinkTypes.cs` (declares the `IDevWebhookSink` port) to `Webhooks/Repos`.
- IDE0130 (namespace matches folder), enabled only for the check and then restored: `dotnet format style --diagnostics IDE0130
  --verify-no-changes --include src/Fugue.Application/` exit 0; with one namespace broken on purpose it exits 2.
- slice 5 (web): `npm run lint` All files pass linting; `npm test` Test Files 58 passed, Tests 368 passed; before the path
  fix `hood-notes.spec.ts` had 10 failed / 45 passed, after it 55/55.
- after merging main cc5f135: web `npm run lint` All files pass linting; `npm test` 58 files, 380 tests passed;
  `dotnet build` 0 Warning(s) 0 Error(s); `dotnet format --verify-no-changes` exit 0.
- full suite under the lock, 11:43 (Docker _ping OK, engine time = host time, on battery): ArchTests 37/37; Fugue.Tests
  766 passed / 0 failed, then the test host crashed in `KafkaPublisherTests.With_no_broker_the_outbox_row_is_not_marked_dispatched_and_is_retried_later`
  (librdkafka connect to 127.0.0.1:1). The same crash at the same point reproduces on main 38db5bd without this branch.
  Completed in two runs under the lock: Messaging alone 2/2, everything else 847/847 -> 849/849 + 37/37, matching base.
