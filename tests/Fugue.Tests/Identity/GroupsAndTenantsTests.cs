using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Groups;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fugue.Tests.Identity;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class GroupsAndTenantsTests(PostgresFixture db)
{
    private static string Email() => $"{Guid.NewGuid():N}@example.test";

    private async Task<(Guid Subject, string Action, string Actor, string? Correlation)[]> Audits(UserId target)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT subject_id, action, actor, correlation_id FROM grant_audit WHERE target_user_id = @t AND subject_type = 'group' ORDER BY occurred_at, id", connection);
        command.Parameters.AddWithValue("t", target.Value);
        var rows = new List<(Guid, string, string, string?)>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return [.. rows];
    }

    [Fact]
    public async Task Admin_lists_groups_with_roles_and_member_counts()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        GroupDto[] groups = (await admin.GetFromJsonAsync<GroupDto[]>("/api/v1/groups"))!;

        GroupDto support = groups.Single(g => g.Code == "SUPPORT");
        Assert.Equal([RoleCodes.Staff], support.Roles);
        Assert.True(support.MemberCount >= 1); // support@fugue.test
    }

    [Fact]
    public async Task Adding_and_removing_a_member_changes_effective_access_and_writes_grant_audit_rows()
    {
        (string email, UserId user) = await db.NewUserAsync();
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        using HttpClient member = db.ClientAs(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/customers")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/v1/groups/support/members/{user}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/v1/groups/SUPPORT/members/{user}", null)).StatusCode); // idempotent
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/customers?limit=1")).StatusCode); // revocation and grant are immediate

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/groups/SUPPORT/members/{user}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/customers")).StatusCode);

        (Guid Subject, string Action, string Actor, string? Correlation)[] audits = await Audits(user);
        Assert.Equal(["granted", "revoked"], audits.Select(a => a.Action)); // the idempotent second add wrote nothing
        Assert.All(audits, a => Assert.False(string.IsNullOrEmpty(a.Correlation)));
        Assert.All(audits, a => Assert.NotEqual("system", a.Actor));
    }

    [Fact]
    public async Task Group_changes_need_manage_and_report_unknown_groups_users_and_non_members()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsync($"/api/v1/groups/SUPPORT/members/{user}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/api/v1/groups/NOPE/members/{user}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/api/v1/groups/SUPPORT/members/{Guid.NewGuid()}", null)).StatusCode);
        HttpResponseMessage notMember = await admin.DeleteAsync($"/api/v1/groups/SUPPORT/members/{user}");
        Assert.Equal(HttpStatusCode.NotFound, notMember.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await notMember.Content.ReadAsStringAsync());
        Assert.Equal("group.member_not_found", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_grant_and_its_audit_row_roll_back_together()
    {
        (_, UserId user) = await db.NewUserAsync();
        Guid group = IdentityCatalog.SupportGroup.Value;

        await using (AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope())
        {
            FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
            IGrantAuditWriter audit = scope.ServiceProvider.GetRequiredService<IGrantAuditWriter>();
            await using IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(default);
            context.GroupMembers.Add(new GroupMember { GroupId = IdentityCatalog.SupportGroup, UserId = user });
            audit.Record("group", group, user.Value, "granted", "will be rolled back");
            await context.SaveChangesAsync();
            // disposed without CommitAsync: everything above is rolled back
        }

        await using AsyncServiceScope check = db.Factory.Services.CreateAsyncScope();
        FugueDbContext verify = check.ServiceProvider.GetRequiredService<FugueDbContext>();
        Assert.False(await verify.GroupMembers.AnyAsync(m => m.UserId == user));
        Assert.Empty(await Audits(user));
    }

    [Fact]
    public async Task A_users_tenant_reaches_the_current_user_but_filters_nothing()
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

        using HttpClient tenantUser = db.ClientAs(email);
        HttpResponseMessage miss = await tenantUser.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");
        using JsonDocument json = JsonDocument.Parse(await miss.Content.ReadAsStringAsync());
        Assert.Equal(tenantId.Value.ToString(), json.RootElement.GetProperty("tenantId").GetString()); // verified claim, not a header

        // ADR-003: no query filter. A tenant user is scoped by ownership and permission only, never by tenant.
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/customers", new { name = "Other tenant", email = Email() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tenantUser.GetAsync("/api/v1/customers?limit=1")).StatusCode);
    }
}
