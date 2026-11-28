using FluentResults;
using Fugue.Application.Features.Users.Models;

namespace Fugue.Application.Features.Users.Repos;

/// <summary>A user about to be created: the email is already canonical (<c>EmailKey</c>), the password already hashed, the role codes known and active.</summary>
public sealed record NewUser(string Email, string DisplayName, string PasswordHash, IReadOnlyList<string> RoleCodes);

/// <summary>
/// User administration (EF). The user, its role assignments and one <c>grant_audit</c> row per role commit in ONE <c>SaveChanges</c>,
/// so an audit row can never be missing or orphaned.
/// </summary>
public interface IUserAdmin
{
    /// <summary>Creates the user. Fails <c>user.email_taken</c> (Conflict) when a live user already has the email, including a concurrent create.</summary>
    Task<Result<UserDto>> CreateAsync(NewUser user, CancellationToken ct);
}
