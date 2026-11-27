using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Me.Repos;

/// <summary>The profile part of <c>/me</c> that is not a permission: who the user is and which roles and groups they hold.</summary>
public sealed record MeProfile(string Email, string DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups);

/// <summary>Reads the caller's own profile. Effective permissions come from <c>IUserAccessReader</c>, the same query the authorization layer uses.</summary>
public interface IMeReader
{
    /// <summary>Null when the user is unknown, inactive or deleted.</summary>
    Task<MeProfile?> GetProfileAsync(UserId userId, CancellationToken ct);
}
