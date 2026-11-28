using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Roles.Models;
using Fugue.Application.Features.Users.Models;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Identity;

/// <summary>
/// <c>GET /users</c> and <c>GET /roles</c> (USERS_VIEW, W-019) over HTTP against real Postgres: keyset paging, search, no credential leaves the API,
/// deleted users stay hidden, and the role by permission matrix reads from the seed. The fixture database is shared, so each test isolates its own rows.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class UsersAndRolesTests(PostgresFixture db)
{
    private static async Task<List<UserDto>> WalkAsync(HttpClient client, string query)
    {
        List<UserDto> all = [];
        string url = $"/api/v1/users?{query}";
        for (int guard = 0; guard < 200; guard++)
        {
            HttpResponseMessage page = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            all.AddRange((await page.Content.ReadFromJsonAsync<UserDto[]>())!);
            if (!page.Headers.TryGetValues("X-Next-Cursor", out IEnumerable<string>? next))
            {
                return all;
            }

            url = $"/api/v1/users?{query}&cursor={Uri.EscapeDataString(next.Single())}";
        }

        throw new InvalidOperationException("The cursor never ended.");
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Admin_pages_every_user_exactly_once_oldest_first_with_roles_and_groups_and_no_credential()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        (string email, _) = await db.NewUserAsync([RoleCodes.Staff, RoleCodes.Manager]);

        List<UserDto> walked = await WalkAsync(admin, "limit=2");

        Assert.Equal(walked.Count, walked.Select(u => u.Id).Distinct().Count()); // no row twice, none skipped across pages
        Assert.Equal(walked.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id.Value).Select(u => u.Id), walked.Select(u => u.Id));
        UserDto created = walked.Single(u => u.Email == email);
        Assert.Equal(["MANAGER", "STAFF"], created.Roles);
        Assert.Empty(created.Groups);
        Assert.True(created.IsActive);

        UserDto support = walked.Single(u => u.Email == DevUsers.Support); // no role of its own, only the SUPPORT group
        Assert.Empty(support.Roles);
        Assert.Equal(["SUPPORT"], support.Groups);

        string raw = await admin.GetStringAsync("/api/v1/users?limit=100");
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PBKDF2", raw, StringComparison.OrdinalIgnoreCase);
        using JsonDocument json = JsonDocument.Parse(raw);
        Assert.All(json.RootElement.EnumerateArray(), row =>
            Assert.Equal(
                ["createdAt", "displayName", "email", "groups", "id", "isActive", "roles", "version"], // version: the ETag an edit sends back
                row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Search_matches_the_email_or_the_display_name_and_treats_wildcards_as_text()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        (string email, Fugue.Domain.Ids.UserId id) = await db.NewUserAsync();
        string name = "Zed" + Guid.NewGuid().ToString("N")[..10];
        await ExecuteAsync("UPDATE users SET display_name = @n WHERE id = @id", ("n", name), ("id", id.Value));

        UserDto[] byEmail = (await admin.GetFromJsonAsync<UserDto[]>($"/api/v1/users?q={email[..12].ToUpperInvariant()}"))!;
        UserDto[] byName = (await admin.GetFromJsonAsync<UserDto[]>($"/api/v1/users?q={name.ToLowerInvariant()}"))!;
        UserDto[] wildcard = (await admin.GetFromJsonAsync<UserDto[]>("/api/v1/users?q=%25"))!;
        UserDto[] none = (await admin.GetFromJsonAsync<UserDto[]>($"/api/v1/users?q={Guid.NewGuid():N}"))!;

        Assert.Equal(email, byEmail.Single().Email);
        Assert.Equal(name, byName.Single().DisplayName);
        Assert.Empty(wildcard); // a literal percent sign, not "everything"
        Assert.Empty(none);
    }

    [Fact]
    public async Task A_deleted_user_never_appears_and_a_bad_cursor_or_limit_is_400()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        (string email, Fugue.Domain.Ids.UserId id) = await db.NewUserAsync();
        await ExecuteAsync("UPDATE users SET is_deleted = true, deleted_at = now() WHERE id = @id", ("id", id.Value));

        Assert.Empty((await admin.GetFromJsonAsync<UserDto[]>($"/api/v1/users?q={email}"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/users?cursor=not-a-cursor")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/users?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/users?limit=101")).StatusCode);
    }

    [Fact]
    public async Task Users_and_roles_need_users_view_which_staff_and_self_service_do_not_hold()
    {
        using HttpClient anonymous = db.Anonymous();
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        using HttpClient self = db.ClientAs(DevUsers.Self);
        (string email, _) = await db.NewUserAsync(overrides: [(Permissions.UsersView, true, null)]);
        using HttpClient granted = db.ClientAs(email); // the permission, not the role, opens the door

        foreach (string path in new[] { "/api/v1/users", "/api/v1/roles" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await self.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await granted.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task Roles_list_every_seeded_role_with_its_permissions_and_their_modules()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        RoleDto[] roles = (await admin.GetFromJsonAsync<RoleDto[]>("/api/v1/roles"))!;

        Assert.Equal(["ADMIN", "MANAGER", "SELF_SERVICE", "STAFF"], roles.Select(r => r.Code).Where(c => c is "ADMIN" or "MANAGER" or "SELF_SERVICE" or "STAFF"));
        Assert.Equal(roles.Select(r => r.Code).Order(StringComparer.Ordinal), roles.Select(r => r.Code));
        RoleDto adminRole = roles.Single(r => r.Code == RoleCodes.Admin);
        Assert.Equal(Permissions.All.Order(StringComparer.Ordinal), adminRole.Permissions.Select(p => p.Code).Order(StringComparer.Ordinal));
        Assert.Contains(new RolePermissionDto(Permissions.UsersView, ModuleCodes.Identity), adminRole.Permissions);
        Assert.Contains(new RolePermissionDto(Permissions.ClaimsRead, ModuleCodes.Claims), adminRole.Permissions);
        Assert.Contains(new RolePermissionDto(Permissions.AuditRead, ModuleCodes.Audit), adminRole.Permissions);

        RoleDto staff = roles.Single(r => r.Code == RoleCodes.Staff);
        Assert.DoesNotContain(staff.Permissions, p => p.Code == Permissions.UsersView);
        Assert.Contains(staff.Permissions, p => p.Code == Permissions.ClaimsHandle);
        Assert.Equal(
            new[] { Permissions.CustomersView, Permissions.CustomersEdit, Permissions.ClaimsSubmit, Permissions.ClaimsRead, Permissions.ClaimsRespond }.Order(StringComparer.Ordinal),
            roles.Single(r => r.Code == RoleCodes.SelfService).Permissions.Select(p => p.Code).Order(StringComparer.Ordinal));
        Assert.All(roles, r => Assert.Equal(
            r.Permissions.OrderBy(p => p.Module, StringComparer.Ordinal).ThenBy(p => p.Code, StringComparer.Ordinal),
            r.Permissions)); // module, then code: the order a matrix wants
    }
}

/// <summary>The rollback of <c>AddUsersViewPermission</c> runs and the migration applies again (retrofit QA step 3).</summary>
public sealed class UsersViewMigrationRollbackTests
{
    [Fact]
    public async Task Down_removes_the_resource_the_permission_and_the_admin_grant_and_up_applies_again()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.True(await ScalarAsync(container, "SELECT count(*) FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE p.code = 'USERS_VIEW'") == 1);

        await migrator.MigrateAsync("AddClaims");
        Assert.Equal(0, await ScalarAsync(container, "SELECT count(*) FROM permissions WHERE code = 'USERS_VIEW'"));
        Assert.Equal(0, await ScalarAsync(container, "SELECT count(*) FROM resources WHERE code = 'USERS'"));
        Assert.Equal(0, await ScalarAsync(container, "SELECT count(*) FROM role_permissions rp WHERE NOT EXISTS (SELECT 1 FROM permissions p WHERE p.id = rp.permission_id)"));

        await migrator.MigrateAsync();
        Assert.Equal(1, await ScalarAsync(container, "SELECT count(*) FROM permissions WHERE code = 'USERS_VIEW'"));
    }

    private static async Task<long> ScalarAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
