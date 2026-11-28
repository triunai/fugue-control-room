using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Users.Models;

/// <summary>
/// A user as the admin console lists them: who, whether they can sign in, the roles assigned directly and the groups they belong to (by code).
/// The password hash and every other credential field stay out of this type on purpose. <see cref="Version"/> (the row's xmin) is also sent as the
/// <c>ETag</c>; an edit sends it back in <c>If-Match</c>.
/// </summary>
public sealed record UserDto(
    UserId Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups, DateTimeOffset CreatedAt,
    long Version) : IVersioned;
