using Fugue.Application.Features.Me;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Identity;

/// <summary>
/// The caller's own profile: email, display name, active roles (direct or through an active group) and active groups.
/// A user who is inactive or deleted has no profile.
/// </summary>
internal sealed class MeReader(FugueDbContext db) : IMeReader
{
    public async Task<MeProfile?> GetProfileAsync(UserId userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive && !u.IsDeleted)
            .Select(u => new { u.Email, u.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (user is null)
        {
            return null;
        }

        List<string> groups = await db.GroupMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Groups.Where(g => g.IsActive), m => m.GroupId, g => g.Id, (_, g) => g.Code)
            .ToListAsync(ct);

        List<string> direct = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles.Where(r => r.IsActive), ur => ur.RoleId, r => r.Id, (_, r) => r.Code)
            .ToListAsync(ct);

        List<string> viaGroups = await db.GroupMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Groups.Where(g => g.IsActive), m => m.GroupId, g => g.Id, (_, g) => g.Id)
            .Join(db.GroupRoles, gid => gid, gr => gr.GroupId, (_, gr) => gr.RoleId)
            .Join(db.Roles.Where(r => r.IsActive), rid => rid, r => r.Id, (_, r) => r.Code)
            .ToListAsync(ct);

        return new MeProfile(
            user.Email, user.DisplayName,
            [.. direct.Concat(viaGroups).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. groups.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }
}
