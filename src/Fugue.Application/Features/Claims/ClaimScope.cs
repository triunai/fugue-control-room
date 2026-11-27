using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Claims;

/// <summary>
/// Which claims the caller may touch. <see cref="All"/> (effective <c>CLAIMS_ALL</c>) means every claim; otherwise only the claims the caller submitted
/// (<see cref="ClaimantId"/>). A caller with neither sees nothing. A claim outside the scope is indistinguishable from an absent one.
/// </summary>
public readonly record struct ClaimScope(bool All, UserId? ClaimantId)
{
    public static ClaimScope From(ICurrentUser user) =>
        new(user.HasPermission(Permissions.ClaimsAll), user.UserId is { } id ? UserId.From(id) : null);

    /// <summary>The caller's own claims only, whatever else they hold: for acts only the claimant may do (answering a question).</summary>
    public static ClaimScope Own(ICurrentUser user) => From(user) with { All = false };
}
