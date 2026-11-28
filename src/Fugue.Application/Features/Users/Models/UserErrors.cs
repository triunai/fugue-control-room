using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Users.Models;

/// <summary>The stable error codes of the Users slice.</summary>
public static class UserErrors
{
    public static FugueError NotFound(UserId id) => FugueErrors.NotFound("user.not_found", $"User {id} was not found.");

    public static FugueError EmailTaken() => FugueErrors.Conflict("user.email_taken", "A user with this email already exists.");

    public static FugueError UnknownRoles(IEnumerable<string> codes) =>
        FugueErrors.Validation(new Dictionary<string, string[]> { ["roles"] = [$"Unknown or inactive role: {string.Join(", ", codes)}."] });

    /// <summary>The caller asked for a role that grants a permission they do not hold themselves (no escalation through user creation).</summary>
    public static FugueError RoleBeyondCaller(IEnumerable<string> codes) =>
        FugueErrors.Forbidden("user.role_escalation", $"You cannot assign a role that grants more than you hold: {string.Join(", ", codes)}.");

    public static FugueError VersionMismatch() =>
        FugueErrors.PreconditionFailed("user.version_mismatch", "The user changed since it was read. Re-read it and retry with the new ETag.");

    public static FugueError IfMatchRequired() =>
        FugueErrors.PreconditionRequired("user.if_match_required", "This operation requires an If-Match header with the current ETag.");

    /// <summary>An administrator editing themselves cannot deactivate the account or drop the last direct role that lets them manage users.</summary>
    public static FugueError SelfLockout() =>
        FugueErrors.Conflict("user.self_lockout", "You cannot deactivate yourself or remove your own last role that grants USERS_MANAGE.");

    /// <summary>An edit that would leave the user with no direct role and no active group.</summary>
    public static FugueError NoAccess() =>
        FugueErrors.Validation(new Dictionary<string, string[]> { ["roles"] = ["Pick at least one role: this user has no active group to give them access."] });
}
