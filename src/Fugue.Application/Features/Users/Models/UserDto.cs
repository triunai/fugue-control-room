using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Users.Models;

/// <summary>
/// A user as the admin console lists them: who, whether they can sign in, the roles assigned directly and the groups they belong to (by code).
/// The password hash and every other credential field stay out of this type on purpose.
/// </summary>
public sealed record UserDto(
    UserId Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups, DateTimeOffset CreatedAt);
