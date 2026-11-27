using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Overrides.Models;
using Fugue.Application.Features.Overrides.Repos;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Overrides.Queries;

/// <summary>Contract.</summary>
public sealed record ListOverridesQuery(UserId UserId) : IQuery<Result<IReadOnlyList<OverrideDto>>>;

public sealed class ListOverridesValidator : AbstractValidator<ListOverridesQuery>
{
    public ListOverridesValidator() => RuleFor(x => x.UserId).Must(id => id.Value != Guid.Empty);
}

public sealed class ListOverridesHandler(IOverrideAdmin overrides) : IQueryHandler<ListOverridesQuery, Result<IReadOnlyList<OverrideDto>>>
{
    public async ValueTask<Result<IReadOnlyList<OverrideDto>>> Handle(ListOverridesQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await overrides.ListAsync(query.UserId, cancellationToken));
}
