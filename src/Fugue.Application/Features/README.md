# Features

One folder per feature (a vertical slice). Application code never references ASP.NET Core.

```
Features/<Feature>/
  Commands/<Verb><Noun>.cs         state-changing use case
  Queries/<Verb><Noun>.cs          read-only use case
  Models/                          DTOs, outcomes, errors (<Feature>Dto.cs, <Feature>Errors.cs)
  Repos/                           the slice's ports: I<Feature>Repository, I*Store, I*Reader, I*Admin (with the records they take or return)
  Helpers/                         pure rules and parsers (ClaimFlow, ClaimScope, <Feature>Rules)
```

A folder exists only when the slice has something for it. Namespaces follow folders
(`Fugue.Application.Features.Claims.Models`). Ports used by several slices or by Abstractions stay in
`Application/Abstractions/` (for example `IAuthPorts.cs`). There is no per-slice DI extension: port
implementations are registered by Infrastructure and Mediator handlers are source-generated.

Each `Commands/` or `Queries/` file holds three types:

```csharp
public sealed record CreateWidgetCommand(string Name) : ICommand<Result<WidgetDto>>;   // contract

public sealed class CreateWidgetValidator : AbstractValidator<CreateWidgetCommand> { /* rules */ }

public sealed class CreateWidgetHandler(IWidgetRepository widgets)
    : ICommandHandler<CreateWidgetCommand, Result<WidgetDto>>
{
    // business logic; call the feature repository; return Result.Ok(...) or Result.Fail<T>(FugueErrors.X(...))
}
```

Rules:

- Handlers return FluentResults. Fail with a `FugueError` (`FugueErrors.NotFound`, `Validation`, `Conflict`, ...).
- Repository implementations live in `Fugue.Infrastructure/Persistence/<Feature>/`, not here.
- Controllers in `Fugue.Api/Controllers/` stay one-liners: `Send(new GetWidget(id), ct)`.
- Cross-cutting pipeline behaviors (validation, logging) live in `Common/Behaviors/`.
- `Diagnostics/` (a ping query) proves the pipeline. `Customers/` is the domain-neutral rehearsal of a stateful aggregate (ETag/If-Match, keyset paging, guarded transitions, Dapper insert + EF update split, ADR-006): copy its shape for the next feature.
