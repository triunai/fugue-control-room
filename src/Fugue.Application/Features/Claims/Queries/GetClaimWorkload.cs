using FluentResults;
using Fugue.Domain.Abstractions;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>Contract. Per officer: open claims by status, the oldest open age, decisions in the last 7 and 30 days and the median days to decide; plus the unassigned count and the ageing bands.</summary>
public sealed record GetClaimWorkloadQuery : IQuery<Result<ClaimWorkloadDto>>;

public sealed class GetClaimWorkloadHandler(IClaimReports reports, IAuditStamp stamp) : IQueryHandler<GetClaimWorkloadQuery, Result<ClaimWorkloadDto>>
{
    public async ValueTask<Result<ClaimWorkloadDto>> Handle(GetClaimWorkloadQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await reports.GetWorkloadAsync(stamp.Capture().Now, cancellationToken));
}
