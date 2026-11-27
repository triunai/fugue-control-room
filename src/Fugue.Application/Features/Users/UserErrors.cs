using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Users;

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
}
