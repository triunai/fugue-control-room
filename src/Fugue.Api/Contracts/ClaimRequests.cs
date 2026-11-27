using Fugue.Application.Features.Claims.Models;
using Fugue.Domain.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Contracts;

/// <summary>Body of a claim submission. The claimant is the caller; the currency follows from <c>Market</c>.</summary>
public sealed record SubmitClaimRequest(
    Market Market, LineOfBusiness Line, string PolicyNumber, DateOnly IncidentDate, string IncidentDescription, string? IncidentLocation = null);

/// <summary>Body of a reserve update: bigint minor units in the claim's currency (for example 125000 is 1,250.00 in a two-decimal currency).</summary>
public sealed record SetClaimReserveRequest(long AmountMinor);

/// <summary>Body of an information request: the question the claimant must answer.</summary>
public sealed record RequestClaimInfoRequest(string Question);

/// <summary>Body of the claimant's answer to an open information request.</summary>
public sealed record AnswerClaimInfoRequest(string Answer);

/// <summary>Body of a decision. <c>AmountMinor</c> is required to approve (0 &lt; amount &lt;= reserve) and must be absent to reject.</summary>
public sealed record DecideClaimRequest(ClaimOutcome Outcome, string Reason, long? AmountMinor = null);

/// <summary>Query parameters of the claims list, bound as one object (they stay flat in the URL and in OpenAPI). <c>Status</c> repeats or is comma separated.</summary>
public sealed record ClaimListFilter
{
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    [FromQuery(Name = "status")]
    public string[]? Status { get; init; }

    [FromQuery(Name = "assigned")]
    public string? Assigned { get; init; }

    [FromQuery(Name = "market")]
    public string? Market { get; init; }

    [FromQuery(Name = "line")]
    public string? Line { get; init; }

    [FromQuery(Name = "q")]
    public string? Q { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    /// <summary>The assigned officer's user id (staff only).</summary>
    [FromQuery(Name = "officerId")]
    public Guid? OfficerId { get; init; }

    /// <summary>At least this many whole days since submission (UTC), inclusive.</summary>
    [FromQuery(Name = "minAgeDays")]
    public int? MinAgeDays { get; init; }

    /// <summary>At most this many whole days since submission (UTC), inclusive.</summary>
    [FromQuery(Name = "maxAgeDays")]
    public int? MaxAgeDays { get; init; }

    public Fugue.Application.Features.Claims.Queries.ListClaimsQuery ToQuery() => new(Limit, Status, Assigned, Market, Line, Q, Cursor, OfficerId, MinAgeDays, MaxAgeDays);
}

/// <summary>Query parameters of the claim counts: the list's filters without <c>status</c> and paging.</summary>
public sealed record ClaimCountsFilter
{
    [FromQuery(Name = "assigned")]
    public string? Assigned { get; init; }

    [FromQuery(Name = "market")]
    public string? Market { get; init; }

    [FromQuery(Name = "line")]
    public string? Line { get; init; }

    [FromQuery(Name = "q")]
    public string? Q { get; init; }

    /// <summary>The assigned officer's user id (staff only).</summary>
    [FromQuery(Name = "officerId")]
    public Guid? OfficerId { get; init; }

    /// <summary>At least this many whole days since submission (UTC), inclusive.</summary>
    [FromQuery(Name = "minAgeDays")]
    public int? MinAgeDays { get; init; }

    /// <summary>At most this many whole days since submission (UTC), inclusive.</summary>
    [FromQuery(Name = "maxAgeDays")]
    public int? MaxAgeDays { get; init; }

    public Fugue.Application.Features.Claims.Queries.GetClaimCountsQuery ToQuery() => new(Assigned, Market, Line, Q, OfficerId, MinAgeDays, MaxAgeDays);
}
