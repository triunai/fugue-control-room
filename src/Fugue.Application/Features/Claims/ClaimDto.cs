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

/// <summary>
/// One question put to the claimant and its answer, as listed by <c>GET /claims/{id}/info-requests</c>. <see cref="AskedBy"/> reads as "Claims team"
/// to a claimant (no officer names leave the building), like the timeline. <see cref="Answer"/> and <see cref="AnsweredAt"/> are null while open.
/// </summary>
public sealed record ClaimInfoRequestEntryDto(
    ClaimInfoRequestId Id, string Question, string AskedBy, DateTimeOffset AskedAt, string? Answer, DateTimeOffset? AnsweredAt);

/// <summary>Claims per status under the caller's scope and the list's filters (status excluded). Every status is present, zero included.</summary>
public sealed record ClaimCountsDto(int Total, IReadOnlyList<ClaimStatusCountDto> ByStatus);

/// <summary>The number of claims in one status.</summary>
public sealed record ClaimStatusCountDto(ClaimStatus Status, int Count);
