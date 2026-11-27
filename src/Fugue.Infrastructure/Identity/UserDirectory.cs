using Fugue.Application.Common;
using Fugue.Application.Features.Users;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Identity;

/// <summary>The admin console's user list: AsNoTracking projections, keyset paged. The row projection never selects <c>password_hash</c> into the DTO.</summary>
internal sealed class UserDirectory(FugueDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search, KeysetCursor? after, int take, CancellationToken ct)
    {
        IQueryable<User> query = after is { } cursor
            // Row-value comparison is not expressible in LINQ; the rest of the query composes on top of this.
            ? db.Users.FromSqlRaw(SqlScripts.Get("identity/list-users-after"), new NpgsqlParameter("createdAt", cursor.CreatedAt), new NpgsqlParameter("id", cursor.Id))
            : db.Users;

        query = query.AsNoTracking().Where(u => !u.IsDeleted);
        if (!string.IsNullOrEmpty(search))
        {
            string pattern = $"%{EscapeLike(search)}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern, "\\") || EF.Functions.ILike(u.DisplayName, pattern, "\\"));
        }

        var page = await query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id).Take(take)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.IsActive, u.CreatedAt })
            .ToListAsync(ct);
        UserId[] ids = [.. page.Select(u => u.Id)];

        // Two follow-up reads for the page only (never per row): direct roles, then group memberships.
        var roles = await db.UserRoles.AsNoTracking().Where(ur => ids.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Code })
            .ToListAsync(ct);
        var groups = await db.GroupMembers.AsNoTracking().Where(m => ids.Contains(m.UserId))
            .Join(db.Groups, m => m.GroupId, g => g.Id, (m, g) => new { m.UserId, g.Code })
            .ToListAsync(ct);

        return page.Select(u => new UserDto(
            u.Id, u.Email, u.DisplayName, u.IsActive,
            [.. roles.Where(r => r.UserId == u.Id).Select(r => r.Code).Order(StringComparer.Ordinal)],
            [.. groups.Where(g => g.UserId == u.Id).Select(g => g.Code).Order(StringComparer.Ordinal)],
            u.CreatedAt)).ToList();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
