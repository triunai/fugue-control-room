using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Roles.Models;
using Fugue.Application.Features.Roles.Repos;
using Mediator;

namespace Fugue.Application.Features.Roles.Queries;

/// <summary>Contract.</summary>
public sealed record ListRolesQuery : IQuery<Result<IReadOnlyList<RoleDto>>>;

public sealed class ListRolesValidator : AbstractValidator<ListRolesQuery>;

public sealed class ListRolesHandler(IRoleCatalog roles) : IQueryHandler<ListRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async ValueTask<Result<IReadOnlyList<RoleDto>>> Handle(ListRolesQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await roles.ListAsync(cancellationToken));
}
