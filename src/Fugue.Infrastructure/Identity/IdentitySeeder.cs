using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Fugue.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Identity;

public interface IIdentitySeeder
{
    /// <summary>
    /// Idempotently creates the development users (admin, staff, support, two self-service) with <paramref name="password"/>,
    /// and gives the two self-service users one seeded customer each. Development only; never run in other environments.
    /// </summary>
    Task SeedDevelopmentUsersAsync(string password, CancellationToken ct);
}

internal sealed class IdentitySeeder(FugueDbContext db, PasswordVerifier hasher, SqlExecutor sql, IAuditStamp stamp, IGrantAuditWriter audit)
    : IIdentitySeeder
{
    public async Task SeedDevelopmentUsersAsync(string password, CancellationToken ct)
    {
        await EnsureAsync(DevUsers.Admin, "Dev Admin", RoleCodes.Admin, null, ct);
        await EnsureAsync(DevUsers.Staff, "Dev Staff", RoleCodes.Staff, null, ct);
        await EnsureAsync(DevUsers.Support, "Dev Support (group only)", null, IdentityCatalog.SupportGroup, ct);
        UserId self = await EnsureAsync(DevUsers.Self, "Dev Self-Service One", RoleCodes.SelfService, null, ct);
        UserId self2 = await EnsureAsync(DevUsers.Self2, "Dev Self-Service Two", RoleCodes.SelfService, null, ct);

        // Ownership demo: self owns seeded customer 01, self2 owns 02 (only when the customer seeder has run).
        await sql.ExecuteAsync(
            "UPDATE customers SET user_id = @UserId, updated_at = @Now, updated_by = @Actor WHERE id = @Id AND user_id IS NULL",
            new { UserId = self, Id = CustomerId.From(CustomerSeeder.IdFor(1)), stamp.Capture().Now, stamp.Capture().Actor }, ct);
        await sql.ExecuteAsync(
            "UPDATE customers SET user_id = @UserId, updated_at = @Now, updated_by = @Actor WHERE id = @Id AND user_id IS NULL",
            new { UserId = self2, Id = CustomerId.From(CustomerSeeder.IdFor(2)), stamp.Capture().Now, stamp.Capture().Actor }, ct);

        async Task<UserId> EnsureAsync(string email, string name, string? role, GroupId? group, CancellationToken token)
        {
            string key = EmailKey.Canonicalize(email);
            User? existing = await db.Users.FirstOrDefaultAsync(u => u.Email == key && !u.IsDeleted, token);
            if (existing is not null)
            {
                return existing.Id;
            }

            var user = new User
            {
                Id = UserId.New(),
                DisplayName = name,
                Email = key,
                PasswordHash = hasher.Hash(password),
                PasswordLastChangedAt = stamp.Capture().Now,
            };
            db.Users.Add(user);
            if (role is not null)
            {
                db.UserRoles.Add(new UserRole { Id = UserRoleId.New(), UserId = user.Id, RoleId = IdentityCatalog.RoleIdFor(role), AssignedAt = stamp.Capture().Now });
                audit.Record("role", IdentityCatalog.RoleIdFor(role).Value, user.Id.Value, "granted", "development seed");
            }

            if (group is { } g)
            {
                db.GroupMembers.Add(new GroupMember { GroupId = g, UserId = user.Id });
                audit.Record("group", g.Value, user.Id.Value, "granted", "development seed");
            }

            await db.SaveChangesAsync(token);
            return user.Id;
        }
    }
}
