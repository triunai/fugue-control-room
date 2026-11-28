using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Identity;

/// <summary>
/// Creates users through EF so the SaveChanges interceptor stamps and audits the row (the hash is <c>[AuditMask]</c>ed); each role assignment
/// stages a <c>grant_audit</c> row that commits in the same <c>SaveChanges</c>.
/// </summary>
internal sealed class UserAdmin(FugueDbContext db, IGrantAuditWriter audit, ICurrentUser currentUser, TimeProvider time) : IUserAdmin
{
    public async Task<Result<UserDto>> CreateAsync(NewUser user, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == user.Email && !u.IsDeleted, ct))
        {
            return Result.Fail<UserDto>(UserErrors.EmailTaken());
        }

        var roles = await db.Roles.AsNoTracking().Where(r => user.RoleCodes.Contains(r.Code)).Select(r => new { r.Id, r.Code }).ToListAsync(ct);
        DateTimeOffset now = time.GetUtcNow();
        UserId? assignedBy = currentUser.UserId is { } caller ? UserId.From(caller) : null;
        var entity = new User
        {
            Id = UserId.New(),
            DisplayName = user.DisplayName,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            PasswordLastChangedAt = now,
        };
        db.Users.Add(entity);
        foreach (var role in roles)
        {
            db.UserRoles.Add(new UserRole { Id = UserRoleId.New(), UserId = entity.Id, RoleId = role.Id, AssignedByUserId = assignedBy, AssignedAt = now });
            audit.Record("role", role.Id.Value, entity.Id.Value, "granted", "user created", afterJson: $"{{\"role\":\"{role.Code}\"}}");
        }

        try
        {
            await db.SaveChangesAsync(ct); // the user, its roles and their grant_audit rows commit together
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "ux_users_email_active" })
        {
            return Result.Fail<UserDto>(UserErrors.EmailTaken()); // a concurrent create of the same email won
        }

        return Result.Ok(new UserDto(
            entity.Id, entity.Email, entity.DisplayName, entity.IsActive, [.. roles.Select(r => r.Code).Order(StringComparer.Ordinal)], [], entity.CreatedAt));
    }
}
