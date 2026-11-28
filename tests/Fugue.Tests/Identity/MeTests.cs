using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Me.Models;
using Fugue.Application.Features.Modules.Models;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary><c>GET /api/v1/me</c>: the console's navigation and permission source. It must report exactly what the API enforces, for the caller only.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class MeTests(PostgresFixture db)
{
    private static readonly string[] AllModules = ["AUDIT", "CLAIMS", "CUSTOMERS", "IDENTITY", "SYSTEM", "WEBHOOKS"];

    private static string[] Sorted(IEnumerable<string> codes) => [.. codes.Order(StringComparer.Ordinal)];

    private static async Task<MeDto> Me(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeDto>())!;
    }

    [Fact]
    public async Task Admin_has_every_permission_every_module_and_the_admin_role()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        MeDto me = await Me(admin);

        Assert.Equal(DevUsers.Admin, me.Email);
        Assert.Equal("Dev Admin", me.DisplayName);
        Assert.Equal([RoleCodes.Admin], me.Roles);
        Assert.Empty(me.Groups);
        Assert.Null(me.TenantId);
        Assert.Equal(Sorted(Permissions.All), me.Permissions);
        Assert.Equal(AllModules, me.Modules.Select(m => m.Code));
        Assert.All(me.Modules, m => Assert.False(string.IsNullOrWhiteSpace(m.Name)));
    }

    [Fact]
    public async Task Staff_has_the_customers_and_claims_staff_permissions_and_sees_every_enabled_module()
    {
        using HttpClient staff = db.ClientAs(DevUsers.Staff);

        MeDto me = await Me(staff);

        Assert.Equal([RoleCodes.Staff], me.Roles);
        Assert.Equal(
            Sorted(Permissions.All.Where(p => p.StartsWith("CUSTOMERS_", StringComparison.Ordinal))
                .Concat([Permissions.ClaimsRead, Permissions.ClaimsAll, Permissions.ClaimsHandle, Permissions.ClaimsWorkload])),
            me.Permissions);
        Assert.Equal(AllModules, me.Modules.Select(m => m.Code)); // modules are what is switched on, not what this user may use
    }

    [Fact]
    public async Task Self_service_has_customer_view_and_edit_and_the_claimant_claims_set_and_nothing_else()
    {
        using HttpClient self = db.ClientAs(DevUsers.Self);

        MeDto me = await Me(self);

        Assert.Equal(DevUsers.Self, me.Email);
        Assert.Equal([RoleCodes.SelfService], me.Roles);
        Assert.Equal(
            [Permissions.ClaimsRead, Permissions.ClaimsRespond, Permissions.ClaimsSubmit, Permissions.CustomersEdit, Permissions.CustomersView],
            me.Permissions);
    }

    [Fact]
    public async Task A_group_only_user_gets_the_groups_role_and_the_group_is_listed()
    {
        using HttpClient support = db.ClientAs(DevUsers.Support);

        MeDto me = await Me(support);

        Assert.Equal(["SUPPORT"], me.Groups);
        Assert.Equal([RoleCodes.Staff], me.Roles); // through the group, not assigned directly
        Assert.Contains(Permissions.CustomersView, me.Permissions);
    }

    [Fact]
    public async Task Overrides_are_applied_deny_beats_allow_beats_role_and_an_expired_one_is_ignored()
    {
        DateTimeOffset now = db.Clock.GetUtcNow();
        (string email, _) = await db.NewUserAsync(
            [RoleCodes.SelfService],
            [
                (Permissions.CustomersEdit, false, null), // deny the role's edit
                (Permissions.AuditRead, true, null), // allow what the role lacks
                (Permissions.WebhooksManage, true, now.AddMinutes(-5)), // expired allow: ignored
                (Permissions.CustomersDelete, false, now.AddMinutes(-5)), // expired deny: ignored (the role does not grant it anyway)
            ]);
        using HttpClient client = db.ClientAs(email);

        MeDto me = await Me(client);

        Assert.Equal(
            [Permissions.AuditRead, Permissions.ClaimsRead, Permissions.ClaimsRespond, Permissions.ClaimsSubmit, Permissions.CustomersView],
            me.Permissions);
    }

    [Fact]
    public async Task A_disabled_module_leaves_the_enabled_list_and_takes_its_permissions_with_it()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        ModuleDto before = (await admin.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!.Single(m => m.Code == ModuleCodes.Customers);
        HttpResponseMessage off = await admin.SendAsync(Patch(false, before.Version));
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        long version = long.Parse(off.Headers.ETag!.Tag.Trim('"'), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            MeDto me = await Me(staff);

            Assert.DoesNotContain(me.Modules, m => m.Code == ModuleCodes.Customers);
            Assert.DoesNotContain(me.Permissions, p => p.StartsWith("CUSTOMERS_", StringComparison.Ordinal)); // the customers permissions go with the module
            Assert.Contains(Permissions.ClaimsRead, me.Permissions); // other modules are untouched
            Assert.Equal([RoleCodes.Staff], me.Roles); // the role is still held; it just grants nothing now
        }
        finally
        {
            HttpResponseMessage on = await admin.SendAsync(Patch(true, version));
            Assert.Equal(HttpStatusCode.NoContent, on.StatusCode);
        }

        Assert.Contains((await Me(staff)).Modules, m => m.Code == ModuleCodes.Customers);

        static HttpRequestMessage Patch(bool active, long version)
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/modules/CUSTOMERS") { Content = JsonContent.Create(new { isActive = active }) };
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
            return request;
        }
    }

    [Fact]
    public async Task A_cached_me_is_fresh_after_an_override_or_group_write_and_never_shows_another_users_entry()
    {
        (string email, UserId user) = await db.NewUserAsync([RoleCodes.SelfService]);
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        using HttpClient client = db.ClientAs(email);
        using HttpClient other = db.ClientAs(DevUsers.Self);

        MeDto before = await Me(client); // cached now
        Assert.DoesNotContain(Permissions.AuditRead, before.Permissions);
        MeDto otherMe = await Me(other);
        Assert.Equal(DevUsers.Self, otherMe.Email); // scoped by the caller: never the entry cached a moment ago for someone else
        Assert.Equal(user, (await Me(client)).UserId);

        HttpResponseMessage set = await admin.PutAsJsonAsync($"/api/v1/overrides/{user}/{Permissions.AuditRead}", new { isAllowed = true });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Contains(Permissions.AuditRead, (await Me(client)).Permissions); // the override write invalidated perm:user:{id}

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/v1/groups/SUPPORT/members/{user}", null)).StatusCode);
        MeDto grouped = await Me(client);
        Assert.Equal(["SUPPORT"], grouped.Groups); // the group write invalidated it too
        Assert.Contains(RoleCodes.Staff, grouped.Roles);
        Assert.DoesNotContain(Permissions.AuditRead, (await Me(other)).Permissions); // the other user's entry was untouched and stays theirs
    }

    [Fact]
    public async Task A_users_tenant_comes_from_the_verified_claim_source()
    {
        var tenantId = TenantId.New();
        await using (AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope())
        {
            FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
            context.Tenants.Add(new Tenant { Id = tenantId, Code = "T" + Guid.NewGuid().ToString("N")[..10], Name = "Acme" });
            await context.SaveChangesAsync();
        }

        (string email, UserId user) = await db.NewUserAsync([RoleCodes.Staff]);
        await using (NpgsqlConnection connection = new(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlCommand command = new("UPDATE users SET tenant_id = @t WHERE id = @u", connection);
            command.Parameters.AddWithValue("t", tenantId.Value);
            command.Parameters.AddWithValue("u", user.Value);
            await command.ExecuteNonQueryAsync();
        }

        using HttpClient client = db.ClientAs(email);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", Guid.NewGuid().ToString()); // a header must never choose the tenant

        MeDto me = await Me(client);

        Assert.Equal(tenantId, me.TenantId);
        Assert.Equal(user, me.UserId);
    }

    [Fact]
    public async Task It_requires_a_token_is_never_cached_and_exposes_only_the_callers_own_fields()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync("/api/v1/me")).StatusCode);
        using HttpClient self = db.ClientAs(DevUsers.Self);

        HttpResponseMessage response = await self.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string[] fields = [.. json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["displayName", "email", "groups", "modules", "permissions", "roles", "tenantId", "userId"], fields); // no hash, no ids of others
    }

    [Fact]
    public async Task A_user_deactivated_after_the_token_was_issued_gets_401_not_a_profile()
    {
        (string email, UserId user) = await db.NewUserAsync([RoleCodes.Staff]);
        using HttpClient client = db.ClientAs(email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        await using (NpgsqlConnection connection = new(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlCommand command = new("UPDATE users SET is_active = false WHERE id = @u", connection);
            command.Parameters.AddWithValue("u", user.Value);
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }
}
