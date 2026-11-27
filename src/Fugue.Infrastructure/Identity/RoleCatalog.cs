using Fugue.Application.Features.Roles;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Identity;

/// <summary>Roles with the permissions they grant and each permission's module (permission to resource to module), in two reads.</summary>
internal sealed class RoleCatalog(FugueDbContext db) : IRoleCatalog
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Code).Select(r => new { r.Id, r.Code, r.Name, r.IsActive }).ToListAsync(ct);
        var grants = await (
                from g in db.RolePermissions.AsNoTracking()
                join p in db.Permissions on g.PermissionId equals p.Id
                join res in db.Resources on p.ResourceId equals res.Id
                join m in db.Modules on res.ModuleId equals m.Id
                select new { g.RoleId, PermissionCode = p.Code, Module = m.Code })
            .ToListAsync(ct);

        return roles.Select(r => new RoleDto(
            r.Code, r.Name, r.IsActive,
            [.. grants.Where(g => g.RoleId == r.Id).OrderBy(g => g.Module, StringComparer.Ordinal).ThenBy(g => g.PermissionCode, StringComparer.Ordinal)
                .Select(g => new RolePermissionDto(g.PermissionCode, g.Module))])).ToList();
    }
}
