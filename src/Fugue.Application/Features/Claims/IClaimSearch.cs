using FluentResults;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Claims;

/// <summary>
/// Full-text search over claims (D-063). The index is a rebuildable copy of <c>claims</c>, so it is eventually consistent. The caller's scope is
/// part of the filter, not a post-check: <see cref="ClaimSearchFilter.ClaimantUserId"/> is set for every caller without <c>CLAIMS_ALL</c> and the engine
/// only returns that claimant's claims. With no search engine configured every call fails with <c>search.unavailable</c>.
/// </summary>
public interface IClaimSearch
{
    Task<Result<ClaimSearchResultDto>> SearchAsync(ClaimSearchFilter filter, CancellationToken ct);
}

/// <summary>Search text plus the scope. A non-null <paramref name="ClaimantUserId"/> restricts hits to that claimant's own claims.</summary>
public sealed record ClaimSearchFilter(string Query, Guid? ClaimantUserId, int Limit);

/// <summary>One hit, shaped like a queue row. <paramref name="ClaimantName"/> is staff-only (null for a claimant, who only ever finds their own claims).</summary>
public sealed record ClaimSearchHitDto(
    ClaimId Id,
    string Number,
    Market Market,
    string Currency,
    LineOfBusiness Line,
    ClaimStatus Status,
    string PolicyNumber,
    string IncidentDescription,
    string? IncidentLocation,
    DateTimeOffset SubmittedAt,
    string? ClaimantName);

/// <summary><paramref name="EstimatedTotalHits"/> is the engine's estimate of all matches, not the page size.</summary>
public sealed record ClaimSearchResultDto(IReadOnlyList<ClaimSearchHitDto> Hits, int EstimatedTotalHits);
