using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Claims;

/// <summary>
/// The claim as returned to clients; <see cref="Version"/> is also sent as the <c>ETag</c>. Money is bigint minor units in <see cref="Currency"/>.
/// The last group is staff-only (a caller without <c>CLAIMS_ALL</c> gets nulls): the internal reserve in particular is never shown to a claimant.
/// <see cref="OpenInfoRequest"/> is filled when one claim is read, not in list rows.
/// </summary>
public sealed record ClaimDto(
    ClaimId Id,
    string Number,
    Market Market,
    string Currency,
    LineOfBusiness Line,
    ClaimStatus Status,
    string PolicyNumber,
    DateOnly IncidentDate,
    string IncidentDescription,
    string? IncidentLocation,
    DateTimeOffset SubmittedAt,
    long? ApprovedAmountMinor,
    string? DecisionReason,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? SettledAt,
    DateTimeOffset UpdatedAt,
    long Version,
    ClaimInfoRequestDto? OpenInfoRequest,
    UserId? ClaimantUserId = null,
    string? ClaimantName = null,
    UserId? AssignedOfficerId = null,
    string? AssignedOfficerName = null,
    DateTimeOffset? AssignedAt = null,
    long? ReserveMinor = null) : IVersioned;

/// <summary>The question the claimant is asked to answer.</summary>
public sealed record ClaimInfoRequestDto(ClaimInfoRequestId Id, string Question, DateTimeOffset RequestedAt);

/// <summary>
/// One row of a claim's history. A claimant sees the real name only for their own actions; staff actions read as "Claims team".
/// The note is written for the claimant and never carries the reserve.
/// </summary>
public sealed record ClaimTimelineEntryDto(
    ClaimTimelineEntryId Id,
    DateTimeOffset OccurredAt,
    ClaimStatus? FromStatus,
    ClaimStatus ToStatus,
    string ActorDisplay,
    string Note);
