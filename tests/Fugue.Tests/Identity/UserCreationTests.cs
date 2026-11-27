using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Auth;
using Fugue.Application.Features.Users;
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
/// <c>POST /users</c> (USERS_MANAGE, D-064) over HTTP against real Postgres: the created user signs in with the initial password and their roles
/// take effect, the email is unique among live users, bad input is a 400 with field errors, nobody hands out a role broader than their own,
/// and the password never leaves the request (not in the response, not in an audit row).
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class UserCreationTests(PostgresFixture db)
{
    private const string Password = "initial-Passw0rd-x"; // secrets-scan:ignore (a test fixture value)

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string NewEmail() => $"c{Guid.NewGuid():N}@example.test";

    private static object Body(string email, params string[] roles) => new { email, displayName = "Created User", password = Password, roles }; // secrets-scan:ignore

    private async Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<JsonDocument> ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        return json;
    }

    [Fact]
    public async Task Admin_creates_a_staff_user_who_signs_in_with_the_initial_password_and_reads_the_claims_queue()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        string email = NewEmail();

        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/v1/users", Body("  " + email.ToUpperInvariant() + " ", "staff"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string raw = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        UserDto user = JsonSerializer.Deserialize<UserDto>(raw, Web)!;
        Assert.Equal(email, user.Email); // canonical: trimmed, lower-case
        Assert.Equal("Created User", user.DisplayName);
        Assert.Equal([RoleCodes.Staff], user.Roles);
        Assert.True(user.IsActive);
        Assert.Equal(db.Clock.GetUtcNow(), user.CreatedAt);
        Assert.Equal($"/api/v1/users/{user.Id}", created.Headers.Location?.AbsolutePath);
        UserDto read = (await admin.GetFromJsonAsync<UserDto>(created.Headers.Location))!;
        Assert.Equal((user.Id, user.Email, user.CreatedAt), (read.Id, read.Email, read.CreatedAt));
        Assert.Equal(user.Roles, read.Roles);

        using HttpClient plain = db.Anonymous();
        HttpResponseMessage signIn = await plain.PostAsJsonAsync("/api/v1/auth/token", new { email, password = Password }); // secrets-scan:ignore
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        string token = (await signIn.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;

        plain.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await plain.GetAsync("/api/v1/claims")).StatusCode); // STAFF holds CLAIMS_READ + CLAIMS_ALL
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/api/v1/users")).StatusCode); // and not USERS_VIEW

        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM grant_audit WHERE target_user_id = @id AND subject_type = 'role' AND action = 'granted'", ("id", user.Id.Value)));
        Assert.True(await CountAsync(
            "SELECT count(*) FROM audit_log WHERE action = 'users.create' AND entity_id = @id AND outcome = 'success'", ("id", user.Id.Value.ToString())) >= 1);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM audit_log a WHERE a::text LIKE @p", ("p", $"%{Password}%")));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM users WHERE id = @id AND password_hash LIKE 'AQAAAA%'", ("id", user.Id.Value))); // PasswordHasher v3
    }

    [Fact]
    public async Task A_second_live_user_with_the_same_email_is_409_in_any_case_and_a_deleted_one_frees_the_address()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        string email = NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/users", Body(email, RoleCodes.SelfService))).StatusCode);

        using JsonDocument _ = await ProblemAsync(
            await admin.PostAsJsonAsync("/api/v1/users", Body(email.ToUpperInvariant(), RoleCodes.SelfService)), HttpStatusCode.Conflict, "user.email_taken");
        await using (var connection = new NpgsqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE users SET is_deleted = true, deleted_at = now() WHERE email = @e", connection);
            command.Parameters.AddWithValue("e", email);
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/users", Body(email, RoleCodes.SelfService))).StatusCode);
    }

    [Fact]
    public async Task Bad_input_is_400_with_an_error_on_each_field_and_an_unknown_role_is_400_on_roles()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        using JsonDocument bad = await ProblemAsync(
            await admin.PostAsJsonAsync("/api/v1/users", new { email = "not-an-email", displayName = "", password = "short", roles = Array.Empty<string>() }), // secrets-scan:ignore
            HttpStatusCode.BadRequest, "validation.failed");
        JsonElement errors = bad.RootElement.GetProperty("errors");
        Assert.Equal(["displayName", "email", "password", "roles"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

        string email = NewEmail();
        using JsonDocument unknown = await ProblemAsync(
            await admin.PostAsJsonAsync("/api/v1/users", Body(email, RoleCodes.Staff, "NO_SUCH_ROLE")), HttpStatusCode.BadRequest, "validation.failed");
        Assert.Contains("NO_SUCH_ROLE", unknown.RootElement.GetProperty("errors").GetProperty("roles")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM users WHERE email = @e", ("e", email))); // nothing half-created
    }

    [Fact]
    public async Task Creating_users_needs_users_manage_which_staff_and_self_service_do_not_hold()
    {
        using HttpClient anonymous = db.Anonymous();
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        using HttpClient self = db.ClientAs(DevUsers.Self);
        using HttpClient manager = db.ClientAs(DevUsers.Manager);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/users", Body(NewEmail(), RoleCodes.Staff))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/users", Body(NewEmail(), RoleCodes.Staff))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await self.PostAsJsonAsync("/api/v1/users", Body(NewEmail(), RoleCodes.SelfService))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/v1/users", Body(NewEmail(), RoleCodes.Staff))).StatusCode);
    }

    [Fact]
    public async Task A_users_manage_holder_can_hand_out_only_roles_within_their_own_permissions()
    {
        (string email, _) = await db.NewUserAsync([RoleCodes.Staff], overrides: [(Permissions.UsersManage, true, null)]);
        using HttpClient delegated = db.ClientAs(email);

        Assert.Equal(HttpStatusCode.Created, (await delegated.PostAsJsonAsync("/api/v1/users", Body(NewEmail(), RoleCodes.Staff))).StatusCode);

        string target = NewEmail();
        using JsonDocument escalation = await ProblemAsync(
            await delegated.PostAsJsonAsync("/api/v1/users", Body(target, RoleCodes.Staff, RoleCodes.Admin)), HttpStatusCode.Forbidden, "user.role_escalation");
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM users WHERE email = @e", ("e", target)));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM audit_log WHERE action = 'users.create' AND outcome = 'failure' AND error_code = 'user.role_escalation' AND actor_user_id = (SELECT id FROM users WHERE email = @e)",
            ("e", email)));
    }

    [Fact]
    public async Task An_unknown_user_id_is_404()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        using JsonDocument _ = await ProblemAsync(await admin.GetAsync($"/api/v1/users/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "user.not_found");
    }
}

/// <summary>The rollback of <c>AddUsersManagePermission</c> runs and the migration applies again (retrofit QA step 3).</summary>
public sealed class UsersManageMigrationRollbackTests
{
    [Fact]
    public async Task Down_removes_the_permission_and_the_admin_grant_and_up_applies_again()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.Equal(1, await ScalarAsync(container, "SELECT count(*) FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE p.code = 'USERS_MANAGE'"));
        Assert.Equal(1, await ScalarAsync(container, "SELECT count(*) FROM permissions p JOIN actions a ON a.id = p.action_id JOIN resources r ON r.id = p.resource_id WHERE p.code = 'USERS_MANAGE' AND a.code = 'MANAGE' AND r.code = 'USERS'"));

        await migrator.MigrateAsync("AddUsersViewPermission");
        Assert.Equal(0, await ScalarAsync(container, "SELECT count(*) FROM permissions WHERE code = 'USERS_MANAGE'"));
        Assert.Equal(1, await ScalarAsync(container, "SELECT count(*) FROM permissions WHERE code = 'USERS_VIEW'")); // the previous step is untouched
        Assert.Equal(0, await ScalarAsync(container, "SELECT count(*) FROM role_permissions rp WHERE NOT EXISTS (SELECT 1 FROM permissions p WHERE p.id = rp.permission_id)"));

        await migrator.MigrateAsync();
        Assert.Equal(1, await ScalarAsync(container, "SELECT count(*) FROM permissions WHERE code = 'USERS_MANAGE'"));
    }

    private static async Task<long> ScalarAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
