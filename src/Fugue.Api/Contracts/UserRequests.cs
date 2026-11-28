using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Contracts;

/// <summary>
/// Body of a user create. <c>Password</c> is the initial password the administrator hands over (12 to 128 characters); <c>Roles</c> are role codes,
/// at least one, each granting nothing the caller does not hold.
/// </summary>
public sealed record CreateUserRequest(string Email, string DisplayName, string Password, IReadOnlyList<string> Roles);

/// <summary>
/// Body of a user edit (PUT with <c>If-Match</c>): the display name, the active flag and the full set of direct role codes. The email and the
/// password are not editable here, so an edit can never reset a password.
/// </summary>
public sealed record UpdateUserRequest(string DisplayName, bool IsActive, IReadOnlyList<string> Roles);

/// <summary>Query parameters of the users list, bound as one object. <c>Q</c> matches the email or the display name.</summary>
public sealed record UserListFilter
{
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    [FromQuery(Name = "q")]
    public string? Q { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    public Fugue.Application.Features.Users.Queries.ListUsersQuery ToQuery() => new(Limit, Q, Cursor);
}
