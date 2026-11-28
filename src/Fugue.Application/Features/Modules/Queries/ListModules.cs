using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Modules.Models;
using Fugue.Application.Features.Modules.Repos;
using Mediator;

namespace Fugue.Application.Features.Modules.Queries;

/// <summary>Contract.</summary>
public sealed record ListModulesQuery : IQuery<Result<IReadOnlyList<ModuleDto>>>;

public sealed class ListModulesValidator : AbstractValidator<ListModulesQuery>;

public sealed class ListModulesHandler(IModuleRegistry registry) : IQueryHandler<ListModulesQuery, Result<IReadOnlyList<ModuleDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ModuleDto>>> Handle(ListModulesQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await registry.ListAsync(cancellationToken));
}
