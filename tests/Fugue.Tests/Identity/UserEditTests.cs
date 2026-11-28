using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Me.Models;
using Fugue.Application.Features.Users.Models;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>
/// <c>PUT /users/{id}</c> (USERS_MANAGE, If-Match) over HTTP against real Postgres: the edit lands with a new ETag, the role change is in
/// grant_audit, a stale or missing If-Match changes nothing, and an added role still cannot exceed the caller's own permissions.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class UserEditTests(PostgresFixture db)
{
    private const string Password = "initial-Passw0rd-x"; // secrets-scan:ignore (a test fixture value)

    private static string NewEmail() => $"e{Guid.NewGuid():N}@example.test";

    private async Task<long> CountAsync(string sql, Guid userId)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("u", userId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static HttpRequestMessage Put(Guid id, string? etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/users/{id}") { Content = JsonContent.Create(body) };
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString()!;

    [Fact]
    public async Task A_role_edit_makes_the_edited_users_next_me_fresh()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        (string email, Fugue.Domain.Ids.UserId id) = await db.NewUserAsync([RoleCodes.Staff]); // signs in with the dev password
        UserDto user = (await admin.GetFromJsonAsync<UserDto>($"/api/v1/users/{id}"))!;
        using HttpClient client = db.ClientAs(email);
        Assert.Equal([RoleCodes.Staff], (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Roles); // cached now

        HttpResponseMessage edited = await admin.SendAsync(Put(user.Id.Value, (await admin.GetAsync($"/api/v1/users/{user.Id}")).Headers.ETag!.Tag, new { displayName = user.DisplayName, isActive = true, roles = new[] { RoleCodes.Manager } }));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        Assert.Equal([RoleCodes.Manager], (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Roles);
    }

    [Fact]
    public async Task Admin_edits_name_and_roles_with_if_match_and_a_stale_or_missing_etag_changes_nothing()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        HttpResponseMessage created = await admin.PostAsJsonAsync(
            "/api/v1/users", new { email = NewEmail(), displayName = "Before", password = Password, roles = new[] { RoleCodes.Staff } }); // secrets-scan:ignore
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        UserDto user = (await created.Content.ReadFromJsonAsync<UserDto>())!;

        HttpResponseMessage read = await admin.GetAsync($"/api/v1/users/{user.Id}");
        string etag = read.Headers.ETag!.Tag;
        Assert.Equal($"\"{user.Version}\"", etag); // the create response and the read agree on the version

        HttpResponseMessage missing = await admin.SendAsync(Put(user.Id.Value, null, new { displayName = "X", isActive = true, roles = new[] { RoleCodes.Staff } }));
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        HttpResponseMessage edited = await admin.SendAsync(Put(user.Id.Value, etag, new { displayName = " After ", isActive = true, roles = new[] { RoleCodes.Manager } }));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        UserDto after = (await edited.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(("After", user.Email), (after.DisplayName, after.Email));
        Assert.Equal([RoleCodes.Manager], after.Roles);
        Assert.NotEqual(etag, edited.Headers.ETag!.Tag);
        Assert.Equal($"\"{after.Version}\"", edited.Headers.ETag!.Tag);

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM grant_audit WHERE target_user_id = @u AND action = 'granted' AND reason = 'user updated'", user.Id.Value));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM grant_audit WHERE target_user_id = @u AND action = 'revoked' AND reason = 'user updated'", user.Id.Value));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit_log WHERE action = 'users.update' AND outcome = 'success' AND entity_id = @u::text", user.Id.Value));

        // The first ETag is stale now: 412, and the name stays "After".
        HttpResponseMessage stale = await admin.SendAsync(Put(user.Id.Value, etag, new { displayName = "Lost", isActive = true, roles = new[] { RoleCodes.Manager } }));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("user.version_mismatch", await CodeAsync(stale));
        Assert.Equal("After", (await admin.GetFromJsonAsync<UserDto>($"/api/v1/users/{user.Id}"))!.DisplayName);
    }

    [Fact]
    public async Task A_delegated_users_manager_cannot_add_a_role_beyond_their_own_permissions()
    {
        (string email, _) = await db.NewUserAsync([RoleCodes.Staff], overrides: [(Permissions.UsersManage, true, null)]);
        using HttpClient delegated = db.ClientAs(email);
        HttpResponseMessage created = await delegated.PostAsJsonAsync(
            "/api/v1/users", new { email = NewEmail(), displayName = "Target", password = Password, roles = new[] { RoleCodes.Staff } }); // secrets-scan:ignore
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        UserDto target = (await created.Content.ReadFromJsonAsync<UserDto>())!;
        string etag = created.Headers.ETag!.Tag;

        HttpResponseMessage escalation = await delegated.SendAsync(
            Put(target.Id.Value, etag, new { displayName = "Target", isActive = true, roles = new[] { RoleCodes.Staff, RoleCodes.Admin } }));

        Assert.Equal(HttpStatusCode.Forbidden, escalation.StatusCode);
        Assert.Equal("user.role_escalation", await CodeAsync(escalation));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM grant_audit WHERE target_user_id = @u AND reason = 'user updated'", target.Id.Value));
    }
}
