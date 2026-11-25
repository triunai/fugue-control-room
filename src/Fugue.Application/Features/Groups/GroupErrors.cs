using Fugue.Domain.Results;

namespace Fugue.Application.Features.Groups;

public static class GroupErrors
{
    public static FugueError NotFound(string code) => FugueErrors.NotFound("group.not_found", $"Group {code} was not found.");

    public static FugueError UserNotFound() => FugueErrors.NotFound("user.not_found", "The user was not found.");

    /// <summary>An administrator cannot add or remove themselves (no self-escalation through a role-bearing group).</summary>
    public static FugueError SelfTarget() => FugueErrors.Conflict("group.self_target", "You cannot change your own group membership.");

    public static FugueError MemberNotFound() => FugueErrors.NotFound("group.member_not_found", "The user is not a member of the group.");
}
