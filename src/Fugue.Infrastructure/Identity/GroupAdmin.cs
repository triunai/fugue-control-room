using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Groups;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Identity;

internal sealed class GroupAdmin(FugueDbContext db, IGrantAuditWriter audit) : IGroupAdmin
{
    public async Task<IReadOnlyList<GroupDto>> ListAsync(CancellationToken ct)
    {
        var groups = await db.Groups.AsNoTracking().OrderBy(g => g.Code)
            .Select(g => new
            {
                g.Code,
                g.Name,
                g.IsActive,
                Roles = db.GroupRoles.Where(r => r.GroupId == g.Id).Join(db.Roles, r => r.RoleId, role => role.Id, (_, role) => role.Code).ToList(),
                Members = db.GroupMembers.Count(m => m.GroupId == g.Id),
            })
            .ToListAsync(ct);
        return groups.Select(g => new GroupDto(g.Code, g.Name, g.IsActive, g.Roles, g.Members)).ToList();
    }

    public async Task<Result> AddMemberAsync(string groupCode, UserId userId, CancellationToken ct)
    {
        Group? group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Code == groupCode, ct);
        if (group is null)
        {
            return Result.Fail(GroupErrors.NotFound(groupCode));
        }

        if (!await db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted, ct))
        {
            return Result.Fail(GroupErrors.UserNotFound());
        }

        if (await db.GroupMembers.AnyAsync(m => m.GroupId == group.Id && m.UserId == userId, ct))
        {
            return Result.Ok();
        }

        db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId });
        audit.Record("group", group.Id.Value, userId.Value, "granted", null, afterJson: $"{{\"group\":\"{group.Code}\"}}");
        try
        {
            await db.SaveChangesAsync(ct); // membership and its grant_audit row commit together
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Result.Ok(); // a concurrent add of the same member won: same end state, and that request wrote the audit row
        }

        return Result.Ok();
    }

    public async Task<Result> RemoveMemberAsync(string groupCode, UserId userId, CancellationToken ct)
    {
        Group? group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Code == groupCode, ct);
        if (group is null)
        {
            return Result.Fail(GroupErrors.NotFound(groupCode));
        }

        GroupMember? member = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == userId, ct);
        if (member is null)
        {
            return Result.Fail(GroupErrors.MemberNotFound());
        }

        db.GroupMembers.Remove(member);
        audit.Record("group", group.Id.Value, userId.Value, "revoked", null, beforeJson: $"{{\"group\":\"{group.Code}\"}}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(GroupErrors.MemberNotFound()); // a concurrent remove won
        }

        return Result.Ok();
    }
}
