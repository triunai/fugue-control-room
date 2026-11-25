using Fugue.Domain.Results;

namespace Fugue.Application.Features.Overrides;

public static class OverrideErrors
{
    /// <summary>An administrator cannot grant or remove their own permissions (no self-escalation, no self-lockout by accident).</summary>
    public static FugueError SelfTarget() => FugueErrors.Conflict("override.self_target", "You cannot change your own permission overrides.");

    public static FugueError UserNotFound() => FugueErrors.NotFound("user.not_found", "The user was not found.");

    public static FugueError PermissionNotFound(string code) => FugueErrors.NotFound("permission.not_found", $"Permission {code} was not found.");

    public static FugueError NotFound() => FugueErrors.NotFound("override.not_found", "The override was not found.");

    public static FugueError ExpiryInPast() =>
        FugueErrors.Validation(new Dictionary<string, string[]> { ["expiresAt"] = ["The expiry must be in the future."] });
}
