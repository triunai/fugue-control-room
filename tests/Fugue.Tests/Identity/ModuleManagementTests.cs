using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Modules;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Identity;

/// <summary>Module management gates (ADR-009): config kill switch wins, database switch, round trip, permissions, If-Match.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ModuleManagementTests(PostgresFixture db)
{
    private static async Task<string> Code(HttpResponseMessage response)
    {
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString()!;
    }

    private static async Task<ModuleDto> Customers(HttpClient admin) =>
        (await admin.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!.Single(m => m.Code == ModuleCodes.Customers);

    private static HttpRequestMessage Patch(string code, bool active, long? version)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/modules/{code}") { Content = JsonContent.Create(new { isActive = active }) };
        if (version is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        }

        return request;
    }

    [Fact]
    public async Task Admin_lists_modules_with_config_db_and_effective_state()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        ModuleDto[] modules = (await admin.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!;

        Assert.Equal(["AUDIT", "CUSTOMERS", "IDENTITY", "SYSTEM", "WEBHOOKS"], modules.Select(m => m.Code));
        Assert.All(modules, m => Assert.True(m.ConfigEnabled && m.DbActive && m.Effective));
    }

    [Fact]
    public async Task Listing_and_toggling_need_their_permissions()
    {
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        using HttpClient self = db.ClientAs(DevUsers.Self);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/modules")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.SendAsync(Patch("CUSTOMERS", false, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await self.SendAsync(Patch("CUSTOMERS", false, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync("/api/v1/modules")).StatusCode);
    }

    [Fact]
    public async Task Database_switch_round_trip_disabled_is_404_module_disabled_then_back()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        ModuleDto before = await Customers(admin);

        HttpResponseMessage off = await admin.SendAsync(Patch("customers", false, before.Version));
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        long version = long.Parse(off.Headers.ETag!.Tag.Trim('"'), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            HttpResponseMessage refused = await staff.GetAsync("/api/v1/customers");
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode); // not 403: existence is not revealed
            Assert.Equal("module.disabled", await Code(refused));
            ModuleDto state = await Customers(admin);
            Assert.True(state.ConfigEnabled);
            Assert.False(state.DbActive);
            Assert.False(state.Effective);
            Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync("/api/v1/customers")).StatusCode);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(Patch("CUSTOMERS", true, version))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/customers?limit=1")).StatusCode);
    }

    [Fact]
    public async Task Configuration_off_wins_over_an_active_database_row()
    {
        using var factory = db.Factory.WithWebHostBuilder(b => b.UseSetting("Modules:CUSTOMERS:Enabled", "false"));
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));

        HttpResponseMessage refused = await client.GetAsync("/api/v1/customers");
        ModuleDto state = (await client.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!.Single(m => m.Code == ModuleCodes.Customers);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("module.disabled", await Code(refused));
        Assert.False(state.ConfigEnabled);
        Assert.True(state.DbActive); // the database row is still on...
        Assert.False(state.Effective); // ...and does not matter
    }

    [Fact]
    public async Task Toggle_needs_a_current_if_match_and_protects_system_and_unknown_codes()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        ModuleDto current = await Customers(admin);

        HttpResponseMessage missing = await admin.SendAsync(Patch("CUSTOMERS", false, null));
        HttpResponseMessage stale = await admin.SendAsync(Patch("CUSTOMERS", false, current.Version + 1000));
        HttpResponseMessage unknown = await admin.SendAsync(Patch("NOPE", false, 1));
        ModuleDto system = (await admin.GetFromJsonAsync<ModuleDto[]>("/api/v1/modules"))!.Single(m => m.Code == ModuleCodes.System);
        HttpResponseMessage protectedSystem = await admin.SendAsync(Patch("SYSTEM", false, system.Version));

        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("module.not_found", await Code(unknown));
        Assert.Equal(HttpStatusCode.Conflict, protectedSystem.StatusCode);
        Assert.Equal("module.system_protected", await Code(protectedSystem));
        Assert.True((await Customers(admin)).Effective); // nothing above changed the module
    }
}
