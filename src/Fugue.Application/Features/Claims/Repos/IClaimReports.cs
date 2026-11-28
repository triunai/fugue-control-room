using Fugue.Application.Features.Claims.Models;

namespace Fugue.Application.Features.Claims.Repos;

/// <summary>Read-only roll-ups over the live claim rows (EF, never Dapper): the workload board and the exposure table.</summary>
public interface IClaimReports
{
    Task<ClaimWorkloadDto> GetWorkloadAsync(DateTimeOffset asOf, CancellationToken ct);

    Task<ClaimExposureDto> GetExposureAsync(DateTimeOffset asOf, CancellationToken ct);
}
