using FluentResults;
using Fugue.Domain.Abstractions;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. Open count, outstanding reserve and approved-not-settled amount per market, line and currency, with totals per currency only.</summary>
public sealed record GetClaimExposureQuery : IQuery<Result<ClaimExposureDto>>;

public sealed class GetClaimExposureHandler(IClaimReports reports, IAuditStamp stamp) : IQueryHandler<GetClaimExposureQuery, Result<ClaimExposureDto>>
{
    public async ValueTask<Result<ClaimExposureDto>> Handle(GetClaimExposureQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await reports.GetExposureAsync(stamp.Capture().Now, cancellationToken));
}
