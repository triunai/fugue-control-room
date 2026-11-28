using FluentResults;
using Fugue.Application.Features.Users.Models;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Users.Repos;

/// <summary>The new state of an existing user: the role codes known and active. The email and the password are not editable here.</summary>
public sealed record UserUpdate(UserId Id, string DisplayName, bool IsActive, IReadOnlyList<string> RoleCodes);

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

    /// <summary>
    /// Replaces the display name, the active flag and the direct roles under a row lock, if the row's xmin still equals
    /// <paramref name="expectedVersion"/>. Each role added or removed writes a <c>grant_audit</c> row in the same transaction.
    /// Fails <c>user.not_found</c> or <c>user.version_mismatch</c> (412).
    /// </summary>
    Task<Result> UpdateAsync(UserUpdate user, long expectedVersion, CancellationToken ct);
}
