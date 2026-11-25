# Features

One folder per feature (a vertical slice). Application code never references ASP.NET Core.

```
Features/<Feature>/
  I<Feature>Repository.cs          the persistence contract this feature needs
  Commands/<Verb><Noun>.cs         state-changing use case
  Queries/<Verb><Noun>.cs          read-only use case
```

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
- `Diagnostics/` (a ping query) is the only feature: it proves the pipeline and shows the slice shape. The scaffold carries no domain.
