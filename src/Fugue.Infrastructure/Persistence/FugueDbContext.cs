using Fugue.Domain.Customers;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence.Ids;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence;

public sealed class FugueDbContext(DbContextOptions<FugueDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Module> Modules => Set<Module>();

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<PermissionAction> Actions => Set<PermissionAction>();

    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();

    public DbSet<RoleGrant> RolePermissions => Set<RoleGrant>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupRole> GroupRoles => Set<GroupRole>();

    public DbSet<UserPermissionOverride> Overrides => Set<UserPermissionOverride>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<GrantAudit> GrantAudits => Set<GrantAudit>();

    public DbSet<AccessAudit> AccessAudits => Set<AccessAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FugueDbContext).Assembly);
        IdentityCatalog.HasData(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        TypedIds.ConfigureEfConventions(configurationBuilder);
}
