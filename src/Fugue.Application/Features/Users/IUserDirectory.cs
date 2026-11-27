using Fugue.Application.Common;

namespace Fugue.Application.Features.Users;

/// <summary>Read-only user directory for the admin console (EF, never Dapper): live users only, soft-deleted ones never appear.</summary>
public interface IUserDirectory
{
    /// <summary>
    /// Keyset page ordered by <c>(created_at, id)</c> ascending, up to <paramref name="take"/> rows after <paramref name="after"/>; the caller asks for one extra
    /// to learn whether another page exists. <paramref name="search"/> matches the email or the display name, case-insensitively.
    /// </summary>
    Task<IReadOnlyList<UserDto>> ListAsync(string? search, KeysetCursor? after, int take, CancellationToken ct);
}
