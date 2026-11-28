using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        long version = await CurrentVersionAsync(entity.Id, ct) ?? 0;
        return Result.Ok(new UserDto(
            entity.Id, entity.Email, entity.DisplayName, entity.IsActive, [.. roles.Select(r => r.Code).Order(StringComparer.Ordinal)], [], entity.CreatedAt, version));
    }

    public async Task<Result> UpdateAsync(UserUpdate user, long expectedVersion, CancellationToken ct)
    {
        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // The row lock makes check-then-write atomic: a concurrent edit waits here, then sees the new xmin and gets its 412.
            long? version = await CurrentVersionAsync(user.Id, ct);
            if (version is null)
            {
                return Result.Fail(UserErrors.NotFound(user.Id));
            }

            if (version != expectedVersion)
            {
                return Result.Fail(UserErrors.VersionMismatch());
            }

            User entity = await db.Users.SingleAsync(u => u.Id == user.Id, ct);
            entity.DisplayName = user.DisplayName;
            entity.IsActive = user.IsActive;

            var held = await db.UserRoles.Where(ur => ur.UserId == user.Id)
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { Assignment = ur, r.Code })
                .ToListAsync(ct);
            string[] added = [.. user.RoleCodes.Except(held.Select(h => h.Code), StringComparer.Ordinal)];
            var addedRoles = await db.Roles.AsNoTracking().Where(r => added.Contains(r.Code)).Select(r => new { r.Id, r.Code }).ToListAsync(ct);
            DateTimeOffset now = time.GetUtcNow();
            UserId? assignedBy = currentUser.UserId is { } caller ? UserId.From(caller) : null;
            foreach (var role in addedRoles)
            {
                db.UserRoles.Add(new UserRole { Id = UserRoleId.New(), UserId = user.Id, RoleId = role.Id, AssignedByUserId = assignedBy, AssignedAt = now });
                audit.Record("role", role.Id.Value, user.Id.Value, "granted", "user updated", afterJson: $"{{\"role\":\"{role.Code}\"}}");
            }

            foreach (var gone in held.Where(h => !user.RoleCodes.Contains(h.Code, StringComparer.Ordinal)))
            {
                db.UserRoles.Remove(gone.Assignment);
                audit.Record("role", gone.Assignment.RoleId.Value, user.Id.Value, "revoked", "user updated", beforeJson: $"{{\"role\":\"{gone.Code}\"}}");
            }

            // A roles-only edit still touches the user row, so its xmin (the ETag) moves and the stale If-Match of the other tab fails.
            db.Entry(entity).Property(u => u.UpdatedAt).IsModified = true;
            await db.SaveChangesAsync(ct); // the user row, its role changes, their grant_audit rows and the audit_log row commit together
            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return Result.Ok();
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync();
            }
        }
    }

    private async Task<long?> CurrentVersionAsync(UserId id, CancellationToken ct) =>
        (await db.Database.SqlQueryRaw<long>(SqlScripts.Get("identity/lock-user"), new NpgsqlParameter("id", id.Value)).ToListAsync(ct)) is [var v] ? v : null;
}
