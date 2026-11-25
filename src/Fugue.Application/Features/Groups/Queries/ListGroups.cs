using FluentResults;
using FluentValidation;
using Mediator;

namespace Fugue.Application.Features.Groups.Queries;

/// <summary>Contract.</summary>
public sealed record ListGroupsQuery : IQuery<Result<IReadOnlyList<GroupDto>>>;

public sealed class ListGroupsValidator : AbstractValidator<ListGroupsQuery>;

public sealed class ListGroupsHandler(IGroupAdmin groups) : IQueryHandler<ListGroupsQuery, Result<IReadOnlyList<GroupDto>>>
{
    public async ValueTask<Result<IReadOnlyList<GroupDto>>> Handle(ListGroupsQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await groups.ListAsync(cancellationToken));
}
