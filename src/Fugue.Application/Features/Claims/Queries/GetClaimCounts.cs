using FluentResults;
using FluentValidation;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>
/// Contract. How many claims sit in each status, under the caller's scope (a claimant counts their own, staff count all) and the same filters as the list
/// except <c>status</c>: <paramref name="Assigned"/> (<c>me</c>, <c>none</c>, <c>any</c>), <paramref name="Market"/>, <paramref name="Line"/> and <paramref name="Search"/>.
/// </summary>
public sealed record GetClaimCountsQuery(string? Assigned, string? Market, string? Line, string? Search) : IQuery<Result<ClaimCountsDto>>;

public sealed class GetClaimCountsValidator : AbstractValidator<GetClaimCountsQuery>
{
    public GetClaimCountsValidator()
    {
        RuleFor(x => x.Assigned)
            .Must(a => Enum.TryParse(a, ignoreCase: true, out ClaimAssignment v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Assigned))
            .WithMessage("Assigned must be 'me', 'none' or 'any'.");
        RuleFor(x => x.Market)
            .Must(m => Enum.TryParse(m, ignoreCase: true, out Market v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Market))
            .WithMessage("Market must be one of SG, MY, TH, HK, ID, PH.");
        RuleFor(x => x.Line)
            .Must(l => Enum.TryParse(l, ignoreCase: true, out LineOfBusiness v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Line))
            .WithMessage("Line must be 'Motor' or 'Property'.");
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class GetClaimCountsHandler(IClaimRepository claims, ICurrentUser currentUser) : IQueryHandler<GetClaimCountsQuery, Result<ClaimCountsDto>>
{
    public async ValueTask<Result<ClaimCountsDto>> Handle(GetClaimCountsQuery query, CancellationToken cancellationToken)
    {
        var filter = new ClaimFilter(
            [],
            Enum.TryParse(query.Assigned, ignoreCase: true, out ClaimAssignment assigned) ? assigned : ClaimAssignment.Any,
            Enum.TryParse(query.Market, ignoreCase: true, out Market market) ? market : null,
            Enum.TryParse(query.Line, ignoreCase: true, out LineOfBusiness line) ? line : null,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim());

        IReadOnlyDictionary<ClaimStatus, int> counts = await claims.CountByStatusAsync(filter, ClaimScope.From(currentUser), cancellationToken);
        ClaimStatusCountDto[] byStatus = [.. Enum.GetValues<ClaimStatus>().Select(s => new ClaimStatusCountDto(s, counts.GetValueOrDefault(s)))];
        return Result.Ok(new ClaimCountsDto(byStatus.Sum(c => c.Count), byStatus));
    }
}
