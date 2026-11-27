using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Claims;

/// <summary>The stable error codes of the Claims slice. The transition codes (<c>claim.invalid_transition</c>, <c>claim.reserve_required</c>, <c>claim.amount_invalid</c>, <c>claim.info_request_not_found</c>) come from the aggregate.</summary>
public static class ClaimErrors
{
    /// <summary>Also the answer for a claim the caller may not see: 404, never 403, so a claim number cannot be probed.</summary>
    public static FugueError NotFound(ClaimId id) =>
        FugueErrors.NotFound("claim.not_found", $"Claim {id} was not found.");

    public static FugueError VersionMismatch() =>
        FugueErrors.PreconditionFailed(
            "claim.version_mismatch", "The claim changed since it was read. Re-read it and retry with the new ETag.");

    public static FugueError IfMatchRequired() =>
        FugueErrors.PreconditionRequired("claim.if_match_required", "This operation requires an If-Match header with the current ETag.");

    public static FugueError NotAssignedToYou() =>
        FugueErrors.Conflict("claim.not_assigned_to_you", "This claim is assigned to another officer. Only the assigned officer, or a manager, can act on it.");

    public static FugueError Unauthenticated() =>
        FugueErrors.Unauthorized("claim.unauthenticated", "Sign in to continue.");
}
