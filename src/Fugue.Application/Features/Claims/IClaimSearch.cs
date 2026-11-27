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

/// <summary>
/// One hit, shaped like a queue row. <paramref name="ClaimantName"/> is staff-only (null for a claimant, who only ever finds their own claims).
/// <paramref name="Highlights"/> says why the claim matched: field name (<c>number</c>, <c>policyNumber</c>, <c>claimantName</c>, <c>marketName</c>,
/// <c>market</c>, <c>line</c>, <c>incidentLocation</c>, <c>incidentDescription</c>) to its text with the matched words wrapped in <c>&lt;em&gt;</c> (the
/// description cropped around the match). Only matched fields appear; the rest of the text is NOT HTML-escaped, so render it as text.
/// </summary>
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
    string? ClaimantName,
    IReadOnlyDictionary<string, string>? Highlights = null);

/// <summary>
/// <paramref name="EstimatedTotalHits"/> is the engine's estimate of all matches, not the page size. <paramref name="Facets"/> counts every match (not only
/// this page) by <c>status</c>, <c>market</c> and <c>line</c>, under the same scope as the hits.
/// </summary>
public sealed record ClaimSearchResultDto(
    IReadOnlyList<ClaimSearchHitDto> Hits,
    int EstimatedTotalHits,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? Facets = null);
