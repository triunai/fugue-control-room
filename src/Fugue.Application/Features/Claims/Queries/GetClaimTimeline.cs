using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. The claim's history, oldest first, with the same visibility as the claim itself (a claim you cannot see has no timeline: 404).</summary>
public sealed record GetClaimTimelineQuery(ClaimId Id) : IQuery<Result<IReadOnlyList<ClaimTimelineEntryDto>>>;

public sealed class GetClaimTimelineValidator : AbstractValidator<GetClaimTimelineQuery>
{
    public GetClaimTimelineValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetClaimTimelineHandler(IClaimRepository claims, ICurrentUser currentUser)
    : IQueryHandler<GetClaimTimelineQuery, Result<IReadOnlyList<ClaimTimelineEntryDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ClaimTimelineEntryDto>>> Handle(GetClaimTimelineQuery query, CancellationToken cancellationToken) =>
        await claims.GetTimelineAsync(query.Id, ClaimScope.From(currentUser), cancellationToken) is { } entries
            ? Result.Ok(entries)
            : Result.Fail<IReadOnlyList<ClaimTimelineEntryDto>>(ClaimErrors.NotFound(query.Id));
}
