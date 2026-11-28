using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. Every question asked on the claim, newest first, with its answer; same visibility as the claim (a claim you cannot see has no questions: 404).</summary>
public sealed record GetClaimInfoRequestsQuery(ClaimId Id) : IQuery<Result<IReadOnlyList<ClaimInfoRequestEntryDto>>>;

public sealed class GetClaimInfoRequestsValidator : AbstractValidator<GetClaimInfoRequestsQuery>
{
    public GetClaimInfoRequestsValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetClaimInfoRequestsHandler(IClaimRepository claims, ICurrentUser currentUser)
    : IQueryHandler<GetClaimInfoRequestsQuery, Result<IReadOnlyList<ClaimInfoRequestEntryDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ClaimInfoRequestEntryDto>>> Handle(GetClaimInfoRequestsQuery query, CancellationToken cancellationToken) =>
        await claims.GetInfoRequestsAsync(query.Id, ClaimScope.From(currentUser), cancellationToken) is { } entries
            ? Result.Ok(entries)
            : Result.Fail<IReadOnlyList<ClaimInfoRequestEntryDto>>(ClaimErrors.NotFound(query.Id));
}
