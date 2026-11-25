using Fugue.Application.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Fugue.Tests.Identity;

/// <summary>
/// The effective-permission table (plan section 2 and 7): role grants, group roles, deny > allow > role, expiry boundary with
/// <see cref="FakeTimeProvider"/> (<c>expires_at &lt;= now</c> is expired), and every way a catalog row can switch a permission off.
/// Each test builds its OWN module/resource/action/permission so the seeded catalog is never mutated.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class EffectivePermissionTests(PostgresFixture db) : IAsyncLifetime
{
    private static readonly DateTimeOffset T = new(2032, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private readonly AsyncServiceScope _scope = db.Factory.Services.CreateAsyncScope();
    private FugueDbContext Ctx => _scope.ServiceProvider.GetRequiredService<FugueDbContext>();

    private sealed record Catalog(Module Module, Resource Resource, PermissionAction Action, PermissionDefinition Permission, Role Role, User User);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _scope.DisposeAsync();

    private async Task<Catalog> Build(bool grantRoleToUser = true)
    {
        string n = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var module = new Module { Id = ModuleId.New(), Code = "M" + n, Name = "m" };
        var resource = new Resource { Id = ResourceId.New(), ModuleId = module.Id, Code = "R" + n, Name = "r" };
        var action = new PermissionAction { Id = ActionId.New(), Code = "A" + n, Name = "a" };
        var permission = new PermissionDefinition { Id = PermissionId.New(), ResourceId = resource.Id, ActionId = action.Id, Code = $"R{n}_A{n}" };
        var role = new Role { Id = RoleId.New(), Code = "ROLE" + n, Name = "role" };
        var user = new User { Id = UserId.New(), DisplayName = "u", Email = $"p{n}@example.test", PasswordHash = "x" };
        Ctx.AddRange(module, resource, action, permission, role, user);
        Ctx.Add(new RoleGrant { RoleId = role.Id, PermissionId = permission.Id });
        if (grantRoleToUser)
        {
            Ctx.Add(new UserRole { Id = UserRoleId.New(), UserId = user.Id, RoleId = role.Id });
        }

        await Ctx.SaveChangesAsync();
        return new Catalog(module, resource, action, permission, role, user);
    }

    private async Task<IReadOnlySet<string>?> Permissions(Catalog c, DateTimeOffset? now = null, params string[] disabledModules)
    {
        var time = new FakeTimeProvider(now ?? T);
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(
            disabledModules.ToDictionary(m => $"Modules:{m}:Enabled", _ => (string?)"false")).Build();
        var reader = new UserAccessReader(Ctx, time, new ConfigModuleSwitch(config));
        UserAccess? access = await reader.GetAsync(c.User.Id, default);
        return access?.Permissions;
    }

    private async Task Override(Catalog c, bool allow, DateTimeOffset? expires)
    {
        Ctx.Add(new UserPermissionOverride { Id = UserPermissionOverrideId.New(), UserId = c.User.Id, PermissionId = c.Permission.Id, IsAllowed = allow, ExpiresAt = expires });
        await Ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task A_role_grants_its_permissions_and_nothing_else()
    {
        Catalog c = await Build();

        IReadOnlySet<string> granted = (await Permissions(c))!;

        Assert.Equal([c.Permission.Code], granted);
    }

    [Fact]
    public async Task A_role_held_through_an_active_group_grants_and_an_inactive_group_does_not()
    {
        Catalog c = await Build(grantRoleToUser: false);
        var group = new Group { Id = GroupId.New(), Code = "G" + c.Role.Code, Name = "g" };
        Ctx.AddRange(group, new GroupMember { GroupId = group.Id, UserId = c.User.Id }, new GroupRole { GroupId = group.Id, RoleId = c.Role.Id });
        await Ctx.SaveChangesAsync();

        Assert.Contains(c.Permission.Code, (await Permissions(c))!);

        group.IsActive = false;
        await Ctx.SaveChangesAsync();
        Assert.DoesNotContain(c.Permission.Code, (await Permissions(c))!);
    }

    [Fact]
    public async Task An_allow_override_adds_a_permission_the_roles_do_not_grant()
    {
        Catalog c = await Build(grantRoleToUser: false);
        Assert.Empty((await Permissions(c))!);

        await Override(c, allow: true, expires: null);

        Assert.Equal([c.Permission.Code], (await Permissions(c))!);
    }

    [Fact]
    public async Task A_deny_override_removes_a_role_grant()
    {
        Catalog c = await Build();

        await Override(c, allow: false, expires: null);

        Assert.Empty((await Permissions(c))!);
    }

    [Fact]
    public async Task An_allow_override_expires_exactly_at_its_expiry_instant()
    {
        Catalog c = await Build(grantRoleToUser: false);
        await Override(c, allow: true, expires: T);

        Assert.Contains(c.Permission.Code, (await Permissions(c, T - TimeSpan.FromSeconds(1)))!);
        Assert.DoesNotContain(c.Permission.Code, (await Permissions(c, T))!); // expires_at <= now is expired
        Assert.DoesNotContain(c.Permission.Code, (await Permissions(c, T + TimeSpan.FromSeconds(1)))!);
    }

    [Fact]
    public async Task A_deny_override_stops_suppressing_exactly_at_its_expiry_instant()
    {
        Catalog c = await Build();
        await Override(c, allow: false, expires: T);

        Assert.Empty((await Permissions(c, T - TimeSpan.FromSeconds(1)))!);
        Assert.Contains(c.Permission.Code, (await Permissions(c, T))!); // the expired deny no longer wins
    }

    [Fact]
    public async Task An_inactive_role_grants_nothing()
    {
        Catalog c = await Build();
        c.Role.IsActive = false;
        await Ctx.SaveChangesAsync();

        Assert.Empty((await Permissions(c))!);
    }

    [Theory]
    [InlineData("module-deleted")]
    [InlineData("module-inactive")]
    [InlineData("resource-deleted")]
    [InlineData("action-deleted")]
    public async Task A_deleted_or_inactive_catalog_level_removes_the_permission(string level)
    {
        Catalog c = await Build();
        Assert.Contains(c.Permission.Code, (await Permissions(c))!);
        switch (level)
        {
            case "module-deleted": c.Module.IsDeleted = true; break;
            case "module-inactive": c.Module.IsActive = false; break;
            case "resource-deleted": c.Resource.IsDeleted = true; break;
            default: c.Action.IsDeleted = true; break;
        }

        await Ctx.SaveChangesAsync();

        Assert.Empty((await Permissions(c))!);
    }

    [Fact]
    public async Task A_module_switched_off_in_configuration_wins_over_an_active_database_row()
    {
        Catalog c = await Build();

        Assert.Empty((await Permissions(c, null, c.Module.Code))!);
        Assert.Contains(c.Permission.Code, (await Permissions(c))!);
    }

    [Fact]
    public async Task An_inactive_or_deleted_user_has_no_access_at_all()
    {
        Catalog c = await Build();
        c.User.IsActive = false;
        await Ctx.SaveChangesAsync();
        Assert.Null(await Permissions(c));

        c.User.IsActive = true;
        c.User.IsDeleted = true;
        await Ctx.SaveChangesAsync();
        Assert.Null(await Permissions(c));
    }
}
