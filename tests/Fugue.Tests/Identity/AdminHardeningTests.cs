using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Auth;
using Fugue.Application.Features.Modules;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Audit;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>W-013 hardening of the S3 admin surface: self-target, 23505 races, access_audit fit, tenants, module config, module audit.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AdminHardeningTests(PostgresFixture db)
{
    private async Task SqlAsync(string sql, params (string Name, object Value)[] parameters)
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

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> Code(HttpResponseMessage response) =>
        System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString()!;

    // ---- group self-target (S-L1)

    [Fact]
    public async Task Nobody_can_add_or_remove_themselves_from_a_group()
    {
        (string email, UserId me) = await db.NewUserAsync([RoleCodes.Admin]);
        using HttpClient admin = db.ClientAs(email);

        HttpResponseMessage add = await admin.PutAsync($"/api/v1/groups/SUPPORT/members/{me}", null);
        HttpResponseMessage remove = await admin.DeleteAsync($"/api/v1/groups/SUPPORT/members/{me}");

        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Equal("group.self_target", await Code(add));
        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM group_members WHERE user_id = @u", ("u", me.Value)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM grant_audit WHERE target_user_id = @u", ("u", me.Value)));
    }

    // ---- 23505 and concurrency races (S-L2), made deterministic by running the competing write between the check and the save

    private HttpClient RacingClient(Action<string> onRecord, string email)
    {
        var factory = db.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IGrantAuditWriter>();
            s.AddScoped<IGrantAuditWriter>(sp => new RacingWriter(ActivatorUtilities.CreateInstance<GrantAuditWriter>(sp), onRecord));
        }));
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", db.TokenAsync(email).GetAwaiter().GetResult());
        return client;
    }

    [Fact]
    public async Task A_concurrent_add_of_the_same_member_is_a_success_not_a_500()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient admin = RacingClient(
            _ => SqlAsync("INSERT INTO group_members (group_id, user_id, created_at, created_by, updated_at, updated_by) VALUES (@g, @u, now(), 'race', now(), 'race')",
                ("g", IdentityCatalog.SupportGroup.Value), ("u", user.Value)).GetAwaiter().GetResult(),
            DevUsers.Admin);

        HttpResponseMessage response = await admin.PutAsync($"/api/v1/groups/SUPPORT/members/{user}", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM group_members WHERE user_id = @u", ("u", user.Value)));
    }

    [Fact]
    public async Task A_concurrent_remove_of_the_same_member_is_a_404_not_a_500()
    {
        (_, UserId user) = await db.NewUserAsync();
        await SqlAsync("INSERT INTO group_members (group_id, user_id, created_at, created_by, updated_at, updated_by) VALUES (@g, @u, now(), 'seed', now(), 'seed')",
            ("g", IdentityCatalog.SupportGroup.Value), ("u", user.Value));
        using HttpClient admin = RacingClient(
            _ => SqlAsync("DELETE FROM group_members WHERE user_id = @u", ("u", user.Value)).GetAwaiter().GetResult(),
            DevUsers.Admin);

        HttpResponseMessage response = await admin.DeleteAsync($"/api/v1/groups/SUPPORT/members/{user}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("group.member_not_found", await Code(response));
    }

    [Fact]
    public async Task A_concurrent_create_of_the_same_override_is_a_412_not_a_500()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient admin = RacingClient(
            _ => SqlAsync("INSERT INTO user_permission_overrides (id, user_id, permission_id, is_allowed, created_at, created_by, updated_at, updated_by) VALUES (@i, @u, @p, true, now(), 'race', now(), 'race')",
                ("i", Guid.NewGuid()), ("u", user.Value), ("p", IdentityCatalog.PermissionIdFor(Permissions.GroupsView).Value)).GetAwaiter().GetResult(),
            DevUsers.Admin);

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/v1/overrides/{user}/GROUPS_VIEW", new { isAllowed = false });

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("override.version_mismatch", await Code(response));
        Assert.True(await ScalarAsync<bool>("SELECT is_allowed FROM user_permission_overrides WHERE user_id = @u", ("u", user.Value))); // the winner's row is untouched
    }

    [Fact]
    public async Task An_override_changed_between_the_read_and_the_save_is_refused_not_overwritten()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient plain = db.ClientAs(DevUsers.Admin);
        HttpResponseMessage created = await plain.PutAsJsonAsync($"/api/v1/overrides/{user}/GROUPS_VIEW", new { isAllowed = true, reason = "mine" });
        string tag = created.Headers.ETag!.Tag;
        using HttpClient admin = RacingClient(
            _ => SqlAsync("UPDATE user_permission_overrides SET reason = 'theirs' WHERE user_id = @u", ("u", user.Value)).GetAwaiter().GetResult(),
            DevUsers.Admin);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/overrides/{user}/GROUPS_VIEW") { Content = JsonContent.Create(new { isAllowed = false, reason = "stale" }) };
        request.Headers.TryAddWithoutValidation("If-Match", tag);

        HttpResponseMessage response = await admin.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("theirs", await ScalarAsync<string>("SELECT reason FROM user_permission_overrides WHERE user_id = @u", ("u", user.Value)));
    }

    [Fact]
    public async Task An_identical_put_still_checks_the_version_so_a_racing_change_is_a_412_not_a_stale_success()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient plain = db.ClientAs(DevUsers.Admin);
        HttpResponseMessage created = await plain.PutAsJsonAsync($"/api/v1/overrides/{user}/GROUPS_VIEW", new { isAllowed = true, reason = "same" });
        string tag = created.Headers.ETag!.Tag;
        using HttpClient admin = RacingClient(
            _ => SqlAsync("UPDATE user_permission_overrides SET is_allowed = false WHERE user_id = @u", ("u", user.Value)).GetAwaiter().GetResult(),
            DevUsers.Admin);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/overrides/{user}/GROUPS_VIEW") { Content = JsonContent.Create(new { isAllowed = true, reason = "same" }) };
        request.Headers.TryAddWithoutValidation("If-Match", tag);

        HttpResponseMessage response = await admin.SendAsync(request); // every field equals the stored row, the racing write lands before the save

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("override.version_mismatch", await Code(response));
        Assert.False(await ScalarAsync<bool>("SELECT is_allowed FROM user_permission_overrides WHERE user_id = @u", ("u", user.Value))); // the winner's row stands
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM grant_audit WHERE target_user_id = @u AND action = 'granted'", ("u", user.Value))); // no audit row for the refused write
    }

    private sealed class RacingWriter(IGrantAuditWriter inner, Action<string> onRecord) : IGrantAuditWriter
    {
        public void Record(string subjectType, Guid subjectId, Guid? targetUserId, string action, string? reason, string? beforeJson = null, string? afterJson = null)
        {
            onRecord(subjectType); // runs after the handler's reads and before its SaveChanges
            inner.Record(subjectType, subjectId, targetUserId, action, reason, beforeJson, afterJson);
        }
    }

    // ---- access_audit fit and key hygiene (S-L3, S-L4)

    [Fact]
    public async Task An_over_long_reason_user_agent_or_ip_is_clipped_not_lost()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        IAccessAuditWriter writer = scope.ServiceProvider.GetRequiredService<IAccessAuditWriter>();
        string reason = "perm-" + new string('x', 300);

        await writer.WriteAsync(new AccessAuditEntry("forbidden", "denied", reason, null, null, new ClientInfo(new string('1', 200), new string('u', 600))), default);

        string stored = await ScalarAsync<string>("SELECT reason_code FROM access_audit WHERE reason_code LIKE 'perm-xxx%' ORDER BY occurred_at DESC LIMIT 1");
        Assert.Equal(reason[..64], stored);
        Assert.Equal(64, await ScalarAsync<int>("SELECT length(ip) FROM access_audit WHERE reason_code = @r", ("r", stored)));
        Assert.Equal(256, await ScalarAsync<int>("SELECT length(user_agent) FROM access_audit WHERE reason_code = @r", ("r", stored)));
    }

    [Fact]
    public async Task The_email_hash_uses_a_derived_subkey_not_the_signing_key()
    {
        string email = $"Hash-{Guid.NewGuid():N}@Example.Test";
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        IAccessAuditWriter writer = scope.ServiceProvider.GetRequiredService<IAccessAuditWriter>();
        string key = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Auth:SigningKey"]!;
        byte[] keyBytes = Convert.FromBase64String(key);
        byte[] subkey = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(AccessAuditWriter.SubkeyLabel));
        string canonical = EmailKey.Canonicalize(email);
        string expected = Convert.ToHexString(HMACSHA256.HashData(subkey, Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        string underRawKey = Convert.ToHexString(HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        await writer.WriteAsync(new AccessAuditEntry("login", "failure", "hash_probe", null, email, null), default);

        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM access_audit WHERE email_hash = @h", ("h", expected)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM access_audit WHERE email_hash = @h", ("h", underRawKey)));
    }

    [Fact]
    public async Task Without_a_usable_signing_key_no_email_hash_is_stored()
    {
        string email = $"nohash-{Guid.NewGuid():N}@example.test";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:SigningKey"] = "not base64 !!" }).Build();
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        var writer = new AccessAuditWriter(
            scope.ServiceProvider.GetRequiredService<SqlExecutor>(), scope.ServiceProvider.GetRequiredService<Fugue.Domain.Abstractions.IAuditStamp>(),
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>(), config, scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AccessAuditWriter>>());

        await writer.WriteAsync(new AccessAuditEntry("login", "failure", "no_key_probe", null, email, null), default);

        Assert.True(await ScalarAsync<bool>("SELECT email_hash IS NULL FROM access_audit WHERE reason_code = 'no_key_probe' ORDER BY occurred_at DESC LIMIT 1"));
    }

    // ---- tenants.is_active (S-L9)

    private async Task<(string Email, UserId Id, Guid Tenant)> TenantUserAsync()
    {
        (string email, UserId id) = await db.NewUserAsync([RoleCodes.Staff]);
        Guid tenant = Guid.NewGuid();
        await SqlAsync("INSERT INTO tenants (id, code, name, is_active, created_at, created_by, updated_at, updated_by) VALUES (@i, @c, 'T', true, now(), 'test', now(), 'test')",
            ("i", tenant), ("c", "T" + tenant.ToString("N")[..12]));
        await SqlAsync("UPDATE users SET tenant_id = @t WHERE id = @u", ("t", tenant), ("u", id.Value));
        return (email, id, tenant);
    }

    [Fact]
    public async Task Deactivating_a_tenant_locks_its_users_out_at_once_and_reactivating_restores_them()
    {
        (string email, UserId _, Guid tenant) = await TenantUserAsync();
        using HttpClient user = db.ClientAs(email);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/customers?limit=1")).StatusCode);
        using HttpClient anon = db.Anonymous();

        await SqlAsync("UPDATE tenants SET is_active = false WHERE id = @t", ("t", tenant));

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/v1/customers?limit=1")).StatusCode); // the live token stops working
        HttpResponseMessage login = await anon.PostAsJsonAsync("/api/v1/auth/token", new { email, password = TestAuth.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("auth.invalid_credentials", await Code(login)); // the same uniform failure: no tenant oracle

        await SqlAsync("UPDATE tenants SET is_active = true WHERE id = @t", ("t", tenant));
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/v1/auth/token", new { email, password = TestAuth.Password })).StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_of_a_deactivated_tenants_user_is_refused()
    {
        (string email, UserId _, Guid tenant) = await TenantUserAsync();
        using HttpClient anon = db.Anonymous();
        TokenDto tokens = (await (await anon.PostAsJsonAsync("/api/v1/auth/token", new { email, password = TestAuth.Password })).Content.ReadFromJsonAsync<TokenDto>())!;
        await SqlAsync("UPDATE tenants SET is_active = false WHERE id = @t", ("t", tenant));

        HttpResponseMessage refresh = await anon.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // ---- module switch parsing and SYSTEM (S-L6), module PATCH audit (S-L5)

    private static ConfigModuleSwitch Switch(params (string Key, string? Value)[] settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value)).Build());

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("off")]
    [InlineData("flase")] // a typo must not leave a kill switch open
    [InlineData("disabled")]
    public void Anything_that_is_not_clearly_on_switches_a_module_off(string value)
    {
        Assert.Contains("CUSTOMERS", Switch(("Modules:CUSTOMERS:Enabled", value)).DisabledCodes());
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("")]
    [InlineData(null)]
    public void Explicit_on_blank_or_absent_leaves_a_module_enabled(string? value)
    {
        Assert.Empty(Switch(("Modules:CUSTOMERS:Enabled", value)).DisabledCodes());
    }

    [Fact]
    public async Task The_system_module_cannot_be_switched_off_by_configuration()
    {
        Assert.Empty(Switch(("Modules:SYSTEM:Enabled", "false")).DisabledCodes());

        using var factory = db.Factory.WithWebHostBuilder(b => b.UseSetting("Modules:SYSTEM:Enabled", "false"));
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));
        ModuleDto[] modules = (await client.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!; // still reachable: management did not lock itself out
        Assert.True(modules.Single(m => m.Code == ModuleCodes.System).Effective);
    }

    [Fact]
    public async Task A_module_patch_writes_an_audit_log_row_with_the_change_and_the_actor()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        ModuleDto before = (await admin.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!.Single(m => m.Code == ModuleCodes.Customers);
        Guid moduleId = await ScalarAsync<Guid>("SELECT id FROM modules WHERE code = 'CUSTOMERS'");
        var off = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/modules/CUSTOMERS") { Content = JsonContent.Create(new { isActive = false }) };
        off.Headers.TryAddWithoutValidation("If-Match", $"\"{before.Version}\"");
        HttpResponseMessage disabled = await admin.SendAsync(off);
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        var on = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/modules/CUSTOMERS") { Content = JsonContent.Create(new { isActive = true }) };
        on.Headers.TryAddWithoutValidation("If-Match", disabled.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(on)).StatusCode);

        List<AuditRow> rows = await AuditProbe.ByEntityAsync(db, "Module", moduleId.ToString());

        Assert.True(rows.Count >= 2); // other module tests toggle the same module: only the last two rows are this test's
        rows = rows[^2..];
        Assert.All(rows, r => Assert.Equal("success", r.Outcome));
        Assert.Contains("isActive", rows[0].ChangedFields);
        Assert.False(rows[0].Change("isActive").GetProperty("new").GetBoolean());
        Assert.True(rows[1].Change("isActive").GetProperty("new").GetBoolean());
        Assert.All(rows, r => Assert.NotNull(r.ActorUserId));
    }
}

/// <summary>The <c>OverrideConcurrencyToken</c> migration is a snapshot-only change: no DDL, rolls back and applies again (retrofit QA steps 3 and 4).</summary>
public sealed class OverrideConcurrencyMigrationTests
{
    [Fact]
    public async Task It_applies_and_rolls_back_without_creating_a_real_xmin_column()
    {
        await using Testcontainers.PostgreSql.PostgreSqlContainer container = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        Microsoft.EntityFrameworkCore.Migrations.IMigrator migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(context);

        await migrator.MigrateAsync();
        Assert.True(await Scalar(container, "SELECT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE migration_id LIKE '%OverrideConcurrencyToken')"));
        Assert.False(await Scalar(container, "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'user_permission_overrides' AND column_name = 'xmin')"));

        await migrator.MigrateAsync("CustomerEmailPerOwner");
        Assert.False(await Scalar(container, "SELECT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE migration_id LIKE '%OverrideConcurrencyToken')"));

        await migrator.MigrateAsync();
        Assert.True(await Scalar(container, "SELECT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE migration_id LIKE '%OverrideConcurrencyToken')"));
    }

    private static async Task<bool> Scalar(Testcontainers.PostgreSql.PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
