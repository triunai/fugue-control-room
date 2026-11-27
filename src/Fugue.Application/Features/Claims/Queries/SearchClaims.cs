using FluentResults;
using FluentValidation;
using Fugue.Domain.Abstractions;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. Full-text search over claims (number, policy, place, description, claimant name), under the caller's scope.</summary>
public sealed record SearchClaimsQuery(string? Q, int? Limit) : IQuery<Result<ClaimSearchResultDto>>;

public sealed class SearchClaimsValidator : AbstractValidator<SearchClaimsQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public SearchClaimsValidator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
    }
}

public sealed class SearchClaimsHandler(IClaimSearch search, ICurrentUser currentUser) : IQueryHandler<SearchClaimsQuery, Result<ClaimSearchResultDto>>
{
    public async ValueTask<Result<ClaimSearchResultDto>> Handle(SearchClaimsQuery query, CancellationToken cancellationToken)
    {
        ClaimScope scope = ClaimScope.From(currentUser);
        if (!scope.All && scope.ClaimantId is null)
        {
            return Result.Ok(new ClaimSearchResultDto([], 0)); // neither staff nor an identified claimant: sees nothing, the engine is not asked
        }

        return await search.SearchAsync(
            new ClaimSearchFilter(query.Q!.Trim(), scope.All ? null : scope.ClaimantId!.Value.Value, query.Limit ?? SearchClaimsValidator.DefaultLimit),
            cancellationToken);
    }
}
