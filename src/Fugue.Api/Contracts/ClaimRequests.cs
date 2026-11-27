using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;

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
