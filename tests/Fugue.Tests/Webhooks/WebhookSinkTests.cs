using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;

namespace Fugue.Tests.Webhooks;

/// <summary>The Development sink implements the receiver algorithm: verify, reject stale timestamps, dedupe on the event id (ADR-011).</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class WebhookSinkTests(PostgresFixture db)
{
    private const string Secret = "whsec_sink_test_secret";
    private const string Path = "/api/v1/dev/webhook-sink";

    private async Task<HttpClient> ArmedAdminAsync()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Path + "/secret", new { secret = Secret })).StatusCode);
        return admin;
    }

    private HttpRequestMessage Signed(string eventId, long? timestamp = null, string? secret = null, string body = "{\"id\":\"x\"}")
    {
        long ts = timestamp ?? db.Clock.GetUtcNow().ToUnixTimeSeconds();
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new ByteArrayContent(bytes) };
        request.Headers.Add("X-Fugue-Event-Id", eventId);
        request.Headers.Add("X-Fugue-Event-Type", "customer.created.v1");
        request.Headers.Add("X-Fugue-Timestamp", ts.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("X-Fugue-Signature", WebhookHarness.ExpectedSignature(secret ?? Secret, ts.ToString(System.Globalization.CultureInfo.InvariantCulture), bytes));
        return request;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    [Fact]
    public async Task A_valid_delivery_is_accepted_once_and_a_repeat_is_reported_as_a_duplicate()
    {
        HttpClient admin = await ArmedAdminAsync();
        HttpClient anonymous = db.Anonymous();
        string id = Guid.NewGuid().ToString();

        HttpResponseMessage first = await anonymous.SendAsync(Signed(id));
        HttpResponseMessage again = await anonymous.SendAsync(Signed(id));

        Assert.Equal("accepted", JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
        Assert.Equal("duplicate", JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
        JsonElement[] listed = (await admin.GetFromJsonAsync<JsonElement[]>(Path))!;
        Assert.Single(listed, e => e.GetProperty("eventId").GetString() == id);
    }

    [Fact]
    public async Task A_bad_signature_is_rejected_and_not_stored()
    {
        HttpClient admin = await ArmedAdminAsync();

        HttpResponseMessage response = await db.Anonymous().SendAsync(Signed("bad-sig", secret: "whsec_someone_else"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("sink.bad_signature", await CodeAsync(response));
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement[]>(Path))!);
    }

    [Fact]
    public async Task A_tampered_body_fails_verification()
    {
        await ArmedAdminAsync();
        HttpRequestMessage request = Signed("tamper");
        request.Content = new StringContent("{\"id\":\"changed\"}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task A_correctly_signed_but_stale_timestamp_is_rejected()
    {
        await ArmedAdminAsync();
        long stale = db.Clock.GetUtcNow().AddMinutes(-6).ToUnixTimeSeconds();

        HttpResponseMessage response = await db.Anonymous().SendAsync(Signed("stale", stale));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("sink.stale_timestamp", await CodeAsync(response));
    }

    [Fact]
    public async Task Without_a_secret_the_sink_refuses_to_guess()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        await admin.DeleteAsync(Path);
        await using var factory = db.Factory.WithWebHostBuilder(_ => { });
        // A fresh host has a fresh singleton sink with no secret.
        HttpResponseMessage response = await factory.CreateClient().SendAsync(Signed("nosecret"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Outside_Development_the_sink_does_not_exist()
    {
        await using var production = db.Factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

        HttpResponseMessage response = await production.CreateClient().SendAsync(Signed("prod"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
