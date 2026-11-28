using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. Visible to the claimant who submitted it and to <c>CLAIMS_ALL</c> holders; anything else is 404.</summary>
public sealed record GetClaimByIdQuery(ClaimId Id) : IQuery<Result<ClaimDto>>;

public sealed class GetClaimByIdValidator : AbstractValidator<GetClaimByIdQuery>
{
    public GetClaimByIdValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetClaimByIdHandler(IClaimRepository claims, ICurrentUser currentUser) : IQueryHandler<GetClaimByIdQuery, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(GetClaimByIdQuery query, CancellationToken cancellationToken) =>
        await claims.GetAsync(query.Id, ClaimScope.From(currentUser), cancellationToken) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<ClaimDto>(ClaimErrors.NotFound(query.Id));
}
