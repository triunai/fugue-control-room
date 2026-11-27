using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Claims;

/// <summary>
/// The shape every state-changing claim command shares: require If-Match (428), load within the caller's scope (404), run the aggregate's guarded
/// move (409/400), save with the version check (412), and answer with the claim as it now stands.
/// </summary>
internal static class ClaimFlow
{
    public static async ValueTask<Result<ClaimDto>> ApplyAsync(
        IClaimRepository claims, ClaimId id, long? expectedVersion, ClaimScope scope, Func<Claim, Result> move, CancellationToken ct)
    {
        if (expectedVersion is not { } expected)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.IfMatchRequired());
        }

        Claim? claim = await claims.FindForUpdateAsync(id, scope, ct);
        if (claim is null)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.NotFound(id));
        }

        Result moved = move(claim);
        if (moved.IsFailed)
        {
            return Result.Fail<ClaimDto>(moved.Errors);
        }

        Result saved = await claims.SaveAsync(claim, expected, ct);
        if (saved.IsFailed)
        {
            return Result.Fail<ClaimDto>(saved.Errors);
        }

        return await claims.GetAsync(id, scope, ct) is { } dto ? Result.Ok(dto) : Result.Fail<ClaimDto>(ClaimErrors.NotFound(id));
    }

    /// <summary>
    /// Only the assigned officer may work an assigned claim, unless the caller also holds <c>CLAIMS_SETTLE</c> (the manager override). An unassigned claim
    /// passes: the aggregate then refuses the move itself (<c>claim.invalid_transition</c>).
    /// </summary>
    public static Result RequireAssignedOrManager(Claim claim, ICurrentUser user)
    {
        bool allowed = claim.AssignedOfficerId is not { } assigned
            || (user.UserId is { } me && assigned.Value == me)
            || user.HasPermission(Permissions.ClaimsSettle);
        return allowed ? Result.Ok() : Result.Fail(ClaimErrors.NotAssignedToYou());
    }

    /// <summary>The caller's id and the name written on the timeline row.</summary>
    public static async ValueTask<(UserId Id, string Display)?> ActorAsync(ICurrentUser user, IActorDirectory actors, CancellationToken ct)
    {
        if (user.UserId is not { } id)
        {
            return null;
        }

        return (UserId.From(id), await actors.GetDisplayNameAsync(id, ct) ?? "Unknown user");
    }
}
