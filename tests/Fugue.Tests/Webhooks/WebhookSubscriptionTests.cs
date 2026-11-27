using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Audit;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Webhooks;

/// <summary>Subscription CRUD, the secret lifecycle (shown once, encrypted, never audited) and the SSRF guard at subscribe time (ADR-011).</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class WebhookSubscriptionTests(PostgresFixture db)
{
    private const string PublicUrl = "https://93.184.216.34/hook"; // a public literal: no DNS needed

    private static readonly string[] Everything = ["*"];

    private HttpClient Admin => db.ClientAs(DevUsers.Admin);

    private async Task<(JsonElement Body, string ETag)> CreateAsync(string? url = null, string[]? types = null)
    {
        HttpResponseMessage response = await Admin.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new { name = "Test hook", url = url ?? PublicUrl, eventTypes = types ?? ["customer.*"] });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone(), response.Headers.ETag!.Tag);
    }

    private async Task<string> StoredCiphertextAsync(string id)
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        return await context.WebhookSubscriptions.AsNoTracking().Where(s => s.Id == Fugue.Domain.Ids.WebhookSubscriptionId.From(Guid.Parse(id))).Select(s => s.SecretCiphertext).SingleAsync();
    }

    [Fact]
    public async Task Create_returns_the_secret_once_and_stores_only_ciphertext_that_decrypts_to_it()
    {
        (JsonElement body, string tag) = await CreateAsync();
        string id = body.GetProperty("id").GetString()!;
        string secret = body.GetProperty("secret").GetString()!;

        Assert.StartsWith("whsec_", secret, StringComparison.Ordinal);
        Assert.Equal($"\"{body.GetProperty("version").GetInt64()}\"", tag);
        string ciphertext = await StoredCiphertextAsync(id);
        Assert.DoesNotContain(secret, ciphertext, StringComparison.Ordinal);
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        Assert.Equal(secret, scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(ciphertext));
    }

    [Fact]
    public async Task The_secret_is_never_returned_after_create_neither_by_get_nor_by_list()
    {
        (JsonElement created, _) = await CreateAsync();
        string id = created.GetProperty("id").GetString()!;
        string secret = created.GetProperty("secret").GetString()!;

        string one = await Admin.GetStringAsync($"/api/v1/webhooks/subscriptions/{id}");
        string list = await Admin.GetStringAsync("/api/v1/webhooks/subscriptions?limit=100");

        foreach (string text in new[] { one, list })
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase); // no field of that name either
            Assert.DoesNotContain("ciphertext", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_secret_and_its_ciphertext_never_reach_the_audit_log_but_the_create_is_audited()
    {
        (JsonElement body, _) = await CreateAsync();
        string id = body.GetProperty("id").GetString()!;
        string secret = body.GetProperty("secret").GetString()!;

        List<AuditRow> rows = await AuditProbe.ByEntityAsync(db, "WebhookSubscription", id);

        AuditRow row = Assert.Single(rows);
        Assert.Equal("webhooks.create", row.Action);
        string everything = row.Changes!.Value.GetRawText() + row.Metadata?.GetRawText();
        Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(await StoredCiphertextAsync(id), everything, StringComparison.Ordinal);
        Assert.DoesNotContain("secretCiphertext", row.Changes!.Value.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PublicUrl, row.Change("url").GetProperty("new").GetString());
    }

    [Fact]
    public async Task Rotate_issues_a_new_secret_once_and_the_old_one_stops_decrypting_as_current()
    {
        (JsonElement created, _) = await CreateAsync();
        string id = created.GetProperty("id").GetString()!;
        string oldSecret = created.GetProperty("secret").GetString()!;

        HttpResponseMessage rotated = await Admin.PostAsync($"/api/v1/webhooks/subscriptions/{id}/rotate-secret", null);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        JsonElement body = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync()).RootElement;
        string newSecret = body.GetProperty("secret").GetString()!;
        Assert.NotEqual(oldSecret, newSecret);
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        Assert.Equal(newSecret, scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(await StoredCiphertextAsync(id)));
        Assert.DoesNotContain(newSecret, await Admin.GetStringAsync($"/api/v1/webhooks/subscriptions/{id}"), StringComparison.Ordinal);
        string audit = string.Join(' ', (await AuditProbe.ByEntityAsync(db, "WebhookSubscription", id)).Select(r => r.Changes?.GetRawText()));
        Assert.DoesNotContain(newSecret, audit, StringComparison.Ordinal);
        Assert.DoesNotContain(oldSecret, audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_and_delete_follow_the_if_match_rules_and_update_does_not_touch_the_secret()
    {
        (JsonElement created, string tag) = await CreateAsync();
        string id = created.GetProperty("id").GetString()!;
        string before = await StoredCiphertextAsync(id);
        object edit = new { name = "Renamed", url = PublicUrl, eventTypes = new[] { "customer.created.v1" }, isActive = false };

        HttpResponseMessage noHeader = await Admin.PutAsJsonAsync($"/api/v1/webhooks/subscriptions/{id}", edit);
        HttpRequestMessage stale = new(HttpMethod.Put, $"/api/v1/webhooks/subscriptions/{id}") { Content = JsonContent.Create(edit) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        HttpRequestMessage ok = new(HttpMethod.Put, $"/api/v1/webhooks/subscriptions/{id}") { Content = JsonContent.Create(edit) };
        ok.Headers.TryAddWithoutValidation("If-Match", tag);
        HttpResponseMessage updated = await Admin.SendAsync(ok);

        Assert.Equal(HttpStatusCode.PreconditionRequired, noHeader.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Admin.SendAsync(stale)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        JsonElement read = JsonDocument.Parse(await Admin.GetStringAsync($"/api/v1/webhooks/subscriptions/{id}")).RootElement;
        Assert.Equal(("Renamed", false), (read.GetProperty("name").GetString(), read.GetProperty("isActive").GetBoolean()));
        Assert.Equal(before, await StoredCiphertextAsync(id));

        HttpResponseMessage deleteNoHeader = await Admin.DeleteAsync($"/api/v1/webhooks/subscriptions/{id}");
        HttpRequestMessage delete = new(HttpMethod.Delete, $"/api/v1/webhooks/subscriptions/{id}");
        delete.Headers.TryAddWithoutValidation("If-Match", updated.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.PreconditionRequired, deleteNoHeader.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Admin.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync($"/api/v1/webhooks/subscriptions/{id}")).StatusCode);
    }

    [Fact]
    public async Task Only_a_holder_of_WEBHOOKS_MANAGE_can_use_the_endpoints()
    {
        HttpClient staff = db.ClientAs(DevUsers.Staff);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/webhooks/subscriptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new { name = "x", url = PublicUrl, eventTypes = Everything })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync("/api/v1/webhooks/subscriptions")).StatusCode);
    }

    [Theory]
    [InlineData("https://10.0.0.5/hook")]
    [InlineData("https://192.168.1.10/hook")]
    [InlineData("https://172.16.0.1/hook")]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://localhost/hook")]
    [InlineData("https://[::1]/hook")]
    [InlineData("https://[::ffff:10.0.0.1]/hook")]
    [InlineData("https://[fd00::1]/hook")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://100.64.0.1/hook")]
    [InlineData("https://0.0.0.0/hook")]
    [InlineData("http://93.184.216.34/hook")]
    public async Task The_ssrf_guard_rejects_private_loopback_link_local_and_plain_http_destinations(string url)
    {
        HttpResponseMessage response = await Admin.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new { name = "Bad", url, eventTypes = Everything });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("webhook.url_rejected", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("10.0.0.5", problem.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://user:pw@93.184.216.34/hook")]
    [InlineData("ftp://93.184.216.34/hook")]
    [InlineData("not a url")]
    [InlineData("https://93.184.216.34/hook#frag")]
    public async Task Malformed_urls_and_credentials_in_the_url_are_validation_errors(string url)
    {
        HttpResponseMessage response = await Admin.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new { name = "Bad", url, eventTypes = Everything });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Customer.*")]
    [InlineData("customer..created")]
    [InlineData("**")]
    [InlineData("customer.*.v1")]
    [InlineData("")]
    public async Task Event_filters_are_validated(string filter)
    {
        HttpResponseMessage response = await Admin.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new { name = "Bad", url = PublicUrl, eventTypes = new[] { filter } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_public_destination_is_accepted()
    {
        (JsonElement body, _) = await CreateAsync(PublicUrl, ["*"]);

        Assert.True(body.GetProperty("isActive").GetBoolean());
    }
}
