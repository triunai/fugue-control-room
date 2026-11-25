using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Overrides;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>Override endpoints: deny/allow take effect at once, expiry, replace, audit rows, no self-targeting.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OverridesTests(PostgresFixture db)
{
    private static readonly DateTimeOffset BaseClock = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static HttpRequestMessage Put(UserId user, string permission, object body, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/overrides/{user}/{permission}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    private static HttpRequestMessage Delete(UserId user, string permission, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/overrides/{user}/{permission}");
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    private async Task<List<(string Action, string? Before, string? After, string Actor)>> Audits(UserId target)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT action, before_json::text, after_json::text, actor FROM grant_audit WHERE target_user_id = @t AND subject_type = 'permission_override' ORDER BY occurred_at, id", connection);
        command.Parameters.AddWithValue("t", target.Value);
        var rows = new List<(string, string?, string?, string)>();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add((r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3)));
        }

        return rows;
    }

    [Fact]
    public async Task A_deny_override_takes_effect_at_once_and_removing_it_restores_access()
    {
        (string email, UserId user) = await db.NewUserAsync([RoleCodes.Staff]);
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        using HttpClient target = db.ClientAs(email);
        Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/api/v1/customers?limit=1")).StatusCode);

        HttpResponseMessage set = await admin.SendAsync(Put(user, "customers_view", new { isAllowed = false, reason = "audit hold" }));
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/v1/customers?limit=1")).StatusCode);

        OverrideDto listed = (await admin.GetFromJsonAsync<OverrideDto[]>($"/api/v1/overrides/{user}"))!.Single();
        Assert.Equal(("CUSTOMERS_VIEW", false, "audit hold", false), (listed.Permission, listed.IsAllowed, listed.Reason, listed.Expired));

        Assert.Equal($"\"{listed.Version}\"", set.Headers.ETag!.Tag); // the PUT answers with the version a later If-Match needs
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(Delete(user, "CUSTOMERS_VIEW", set.Headers.ETag!.Tag))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/api/v1/customers?limit=1")).StatusCode);

        var audits = await Audits(user);
        Assert.Equal(["granted", "revoked"], audits.Select(a => a.Action));
        Assert.Null(audits[0].Before);
        Assert.Contains("audit hold", audits[0].After, StringComparison.Ordinal);
        Assert.Contains("audit hold", audits[1].Before, StringComparison.Ordinal);
        Assert.NotEqual("system", audits[0].Actor);
    }

    [Fact]
    public async Task An_allow_override_with_an_expiry_works_until_the_instant_it_expires()
    {
        (string email, UserId user) = await db.NewUserAsync([RoleCodes.SelfService]);
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        using HttpClient target = db.ClientAs(email);
        DateTimeOffset expires = db.Clock.GetUtcNow().AddMinutes(10);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/v1/groups")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true, expiresAt = expires }))).StatusCode);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/api/v1/groups")).StatusCode);

            db.Clock.Now = expires - TimeSpan.FromSeconds(1);
            Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/api/v1/groups")).StatusCode);

            db.Clock.Now = expires; // expires_at <= now is expired
            Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/v1/groups")).StatusCode);
            Assert.True((await admin.GetFromJsonAsync<OverrideDto[]>($"/api/v1/overrides/{user}"))!.Single().Expired);
        }
        finally
        {
            db.Clock.Now = BaseClock;
        }
    }

    [Fact]
    public async Task Setting_again_replaces_the_row_and_the_audit_row_keeps_the_before_state()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        HttpResponseMessage first = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true, reason = "first" }));
        await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = false, reason = "second" }, first.Headers.ETag!.Tag));

        OverrideDto only = (await admin.GetFromJsonAsync<OverrideDto[]>($"/api/v1/overrides/{user}"))!.Single();
        Assert.False(only.IsAllowed);
        var audits = await Audits(user);
        Assert.Equal(2, audits.Count);
        Assert.Contains("first", audits[1].Before, StringComparison.Ordinal);
        Assert.Contains("second", audits[1].After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nobody_can_change_their_own_overrides_and_bad_input_is_rejected()
    {
        (string email, UserId me) = await db.NewUserAsync([RoleCodes.Admin]);
        (_, UserId other) = await db.NewUserAsync();
        using HttpClient admin = db.ClientAs(email);

        HttpResponseMessage self = await admin.SendAsync(Put(me, "CUSTOMERS_VIEW", new { isAllowed = false }));
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await self.Content.ReadAsStringAsync());
        Assert.Equal("override.self_target", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/overrides/{me}/CUSTOMERS_VIEW")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(Put(other, "CUSTOMERS_VIEW", new { isAllowed = true, expiresAt = db.Clock.GetUtcNow().AddMinutes(-1) }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(Put(other, "NOPE_NOPE", new { isAllowed = true }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(Put(UserId.New(), "CUSTOMERS_VIEW", new { isAllowed = true }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/overrides/{other}/CUSTOMERS_VIEW")).StatusCode);
        Assert.Empty(await Audits(me));
    }

    [Fact]
    public async Task Overrides_need_their_own_permissions()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient staff = db.ClientAs(DevUsers.Staff);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/v1/overrides/{user}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true }))).StatusCode);
    }

    [Fact]
    public async Task An_expiry_with_a_non_utc_offset_is_accepted()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        string local = db.Clock.GetUtcNow().AddHours(2).ToOffset(TimeSpan.FromHours(8)).ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        HttpResponseMessage response = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true, expiresAt = local }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Changing_an_existing_override_needs_the_current_etag()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        HttpResponseMessage created = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true, reason = "v1" }));
        string v1 = created.Headers.ETag!.Tag;

        HttpResponseMessage missing = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = false, reason = "blind" }));
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal("override.if_match_required", await Code(missing));

        HttpResponseMessage second = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = false, reason = "v2" }, v1));
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.NotEqual(v1, second.Headers.ETag!.Tag);

        // A stale writer (still holding v1) loses instead of silently overwriting v2.
        HttpResponseMessage stale = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true, reason = "stale" }, v1));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("override.version_mismatch", await Code(stale));
        OverrideDto current = (await admin.GetFromJsonAsync<OverrideDto[]>($"/api/v1/overrides/{user}"))!.Single();
        Assert.Equal("v2", current.Reason);
        Assert.Equal(second.Headers.ETag!.Tag, $"\"{current.Version}\"");
        Assert.Equal(2, (await Audits(user)).Count); // the refused write left no grant_audit row
    }

    [Fact]
    public async Task Removing_needs_the_current_etag_and_a_version_for_a_row_that_is_gone_is_a_mismatch()
    {
        (_, UserId user) = await db.NewUserAsync();
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        HttpResponseMessage created = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true }));
        string v1 = created.Headers.ETag!.Tag;

        HttpResponseMessage noHeader = await admin.SendAsync(Delete(user, "GROUPS_VIEW"));
        Assert.Equal(HttpStatusCode.PreconditionRequired, noHeader.StatusCode);
        HttpResponseMessage stale = await admin.SendAsync(Delete(user, "GROUPS_VIEW", "\"1\""));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Single(await admin.GetFromJsonAsync<OverrideDto[]>($"/api/v1/overrides/{user}") ?? []);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(Delete(user, "GROUPS_VIEW", v1))).StatusCode);

        // The caller still holds v1 of a row that no longer exists: re-creating with it is a mismatch, not a blind create.
        HttpResponseMessage ghost = await admin.SendAsync(Put(user, "GROUPS_VIEW", new { isAllowed = true }, v1));
        Assert.Equal(HttpStatusCode.PreconditionFailed, ghost.StatusCode);
    }

    private static async Task<string> Code(HttpResponseMessage response)
    {
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString()!;
    }
}
