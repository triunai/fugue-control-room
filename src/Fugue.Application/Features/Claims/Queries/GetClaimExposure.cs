using FluentResults;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. Open count, outstanding reserve, approved-not-settled amount and paid (settled) amount per market, line and currency, with totals per currency only.</summary>
public sealed record GetClaimExposureQuery : IQuery<Result<ClaimExposureDto>>;

public sealed class GetClaimExposureHandler(IClaimReports reports, IAuditStamp stamp) : IQueryHandler<GetClaimExposureQuery, Result<ClaimExposureDto>>
{
    public async ValueTask<Result<ClaimExposureDto>> Handle(GetClaimExposureQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await reports.GetExposureAsync(stamp.Capture().Now, cancellationToken));
}
