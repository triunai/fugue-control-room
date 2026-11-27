using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Fugue.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fugue.Infrastructure.Identity;

public interface IIdentitySeeder
{
    /// <summary>
    /// Idempotently creates the development users (admin, staff, manager, support, two self-service) with <paramref name="password"/>,
    /// and gives the two self-service users one seeded customer each. Development only; never run in other environments.
    /// </summary>
    Task SeedDevelopmentUsersAsync(string password, CancellationToken ct);
}

internal sealed class IdentitySeeder(FugueDbContext db, PasswordVerifier hasher, SqlExecutor sql, IAuditStamp stamp, IGrantAuditWriter audit, IAuditTrail trail)
    : IIdentitySeeder
{
    public async Task SeedDevelopmentUsersAsync(string password, CancellationToken ct)
    {
        await EnsureAsync(DevUsers.Admin, "Dev Admin", RoleCodes.Admin, null, ct);
        await EnsureAsync(DevUsers.Staff, "Dev Staff", RoleCodes.Staff, null, ct);
        await EnsureAsync(DevUsers.Manager, "Dev Manager", RoleCodes.Manager, null, ct);
        await EnsureAsync(DevUsers.Support, "Dev Support (group only)", null, IdentityCatalog.SupportGroup, ct);
        UserId self = await EnsureAsync(DevUsers.Self, "Dev Self-Service One", RoleCodes.SelfService, null, ct);
        UserId self2 = await EnsureAsync(DevUsers.Self2, "Dev Self-Service Two", RoleCodes.SelfService, null, ct);

        // Ownership demo: self owns seeded customer 01, self2 owns 02 (only when the customer seeder has run).
        await AssignOwnerAsync(self, CustomerSeeder.IdFor(1), ct);
        await AssignOwnerAsync(self2, CustomerSeeder.IdFor(2), ct);

        async Task AssignOwnerAsync(UserId owner, Guid customerId, CancellationToken token)
        {
            // The Dapper update and its audit row commit together (a direct SQL write the change tracker cannot see).
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(token);
            AuditStampValue now = stamp.Capture();
            int changed = await sql.ExecuteAsync(
                SqlScripts.Get("identity/assign-customer-owner"),
                new { UserId = owner, Id = CustomerId.From(customerId), now.Now, now.Actor }, token);
            if (changed > 0)
            {
                await trail.RecordAsync(
                    new AuditEntry(
                        nameof(Domain.Customers.Customer), customerId.ToString(),
                        new Dictionary<string, AuditFieldChange> { ["UserId"] = AuditFieldChange.Changed(null, owner) },
                        typeof(Domain.Customers.Customer), "customers.assign_owner"),
                    token);
            }

            await tx.CommitAsync(token);
        }

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
