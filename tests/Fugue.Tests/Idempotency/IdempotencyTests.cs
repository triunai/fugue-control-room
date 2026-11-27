using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Customers.Models;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Idempotency;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;

namespace Fugue.Tests.Idempotency;

/// <summary>
/// <c>Idempotency-Key</c> on the two create POSTs, end to end against real Postgres (ADR-013, W-007): replay, reuse, concurrency, freeing, scope,
/// expiry, purge, encryption, the ambiguous-outcome paths and the claim fence.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class IdempotencyTests(PostgresFixture db)
{
    private const string Customers = "/api/v1/customers";
    private const string Subscriptions = "/api/v1/webhooks/subscriptions";
    private const string PublicUrl = "https://93.184.216.34/hook";

    private HttpClient Admin => db.ClientAs(DevUsers.Admin);

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.test";

    private static string CustomerJson(string email, string name = "Idem Customer") => JsonSerializer.Serialize(new { name, email });

    private static readonly string[] HookEvents = ["customer.*"];

    private static string HookJson(string name = "Idem hook") => JsonSerializer.Serialize(new { name, url = PublicUrl, eventTypes = HookEvents });

    private static HttpRequestMessage Post(string url, string json, string? keyHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (keyHeader is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", keyHeader);
        }

        return request;
    }

    private static string Quoted(string key) => $"\"{key}\"";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, string json, string? keyHeader)
    {
        using HttpRequestMessage request = Post(url, json, keyHeader);
        return await client.SendAsync(request);
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> UserIdAsync(string email) =>
        await ScalarAsync<Guid>("SELECT id FROM users WHERE email = @e", ("e", email));

    private Task<long> RowsForKeyAsync(string key) => ScalarAsync<long>("SELECT count(*) FROM idempotency_keys WHERE key = @k", ("k", key));

    private Task<long> CustomersWithEmailAsync(string email) => ScalarAsync<long>("SELECT count(*) FROM customers WHERE email = @e", ("e", email));

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.True("application/problem+json" == response.Content.Headers.ContentType?.MediaType, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(status, response.StatusCode);
    }

    private static bool IsReplayed(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Idempotent-Replayed", out IEnumerable<string>? values) && values.Single() == "true";

    [Fact]
    public async Task The_same_key_and_body_replays_the_first_response_with_one_row_one_audit_row_and_one_outbox_event()
    {
        string email = UniqueEmail();
        string json = CustomerJson(email);
        string key = Quoted(NewKey());

        HttpResponseMessage first = await SendAsync(Admin, Customers, json, key);
        HttpResponseMessage second = await SendAsync(Admin, Customers, json, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        string firstBody = await first.Content.ReadAsStringAsync();
        Assert.Equal(firstBody, await second.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
        Assert.Equal(first.Content.Headers.ContentType, second.Content.Headers.ContentType);
        Assert.False(IsReplayed(first));
        Assert.True(IsReplayed(second));
        Assert.Contains("no-store", second.Headers.CacheControl!.ToString(), StringComparison.Ordinal);

        string id = JsonDocument.Parse(firstBody).RootElement.GetProperty("id").GetString()!;
        Assert.Equal(1L, await CustomersWithEmailAsync(email));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE entity_id = @id AND outcome = 'success'", ("id", id)));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM outbox_messages WHERE aggregate_id = @id", ("id", id)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE entity_type IN ('IdempotencyRecord', 'idempotency_keys')"));
    }

    [Fact]
    public async Task A_bare_token_key_works_like_a_quoted_one()
    {
        string json = CustomerJson(UniqueEmail());
        string key = NewKey();

        HttpResponseMessage first = await SendAsync(Admin, Customers, json, key);
        HttpResponseMessage second = await SendAsync(Admin, Customers, json, Quoted(key)); // the same decoded key

        Assert.True(IsReplayed(second));
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_same_key_with_a_different_body_is_422_key_reused_and_creates_nothing()
    {
        string key = Quoted(NewKey());
        string firstEmail = UniqueEmail();
        string otherEmail = UniqueEmail();
        await SendAsync(Admin, Customers, CustomerJson(firstEmail), key);

        HttpResponseMessage reused = await SendAsync(Admin, Customers, CustomerJson(otherEmail), key);

        await AssertProblemAsync(reused, HttpStatusCode.UnprocessableEntity, "idempotency.key_reused");
        Assert.Equal(0L, await CustomersWithEmailAsync(otherEmail));
        Assert.Equal(1L, await CustomersWithEmailAsync(firstEmail));
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_and_body_execute_once()
    {
        string email = UniqueEmail();
        string json = CustomerJson(email);
        string key = Quoted(NewKey());

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SendAsync(Admin, Customers, json, key)));

        Assert.Equal(1L, await CustomersWithEmailAsync(email));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created && !IsReplayed(r));
        foreach (HttpResponseMessage other in responses.Where(r => r.StatusCode != HttpStatusCode.Created || IsReplayed(r)))
        {
            if (other.StatusCode == HttpStatusCode.Conflict)
            {
                await AssertProblemAsync(other, HttpStatusCode.Conflict, "idempotency.in_progress");
                Assert.Equal("1", other.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                Assert.Equal(HttpStatusCode.Created, other.StatusCode);
                Assert.True(IsReplayed(other));
            }
        }
    }

    [Fact]
    public async Task A_validation_400_frees_the_key_for_a_corrected_retry()
    {
        string key = Quoted(NewKey());
        string email = UniqueEmail();

        HttpResponseMessage invalid = await SendAsync(Admin, Customers, CustomerJson(email, name: ""), key);
        HttpResponseMessage corrected = await SendAsync(Admin, Customers, CustomerJson(email), key);

        await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        Assert.False(IsReplayed(corrected));
        Assert.Equal(1L, await CustomersWithEmailAsync(email));
    }

    [Fact]
    public async Task A_model_binding_400_frees_the_key()
    {
        string key = Quoted(NewKey());

        HttpResponseMessage malformed = await SendAsync(Admin, Customers, "{ not json", key);

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(0L, await RowsForKeyAsync(key.Trim('"')));
    }

    [Fact]
    public async Task A_409_email_taken_frees_the_key_for_a_corrected_retry()
    {
        string taken = UniqueEmail();
        await SendAsync(Admin, Customers, CustomerJson(taken), null);
        string key = Quoted(NewKey());

        HttpResponseMessage conflict = await SendAsync(Admin, Customers, CustomerJson(taken), key);
        string fresh = UniqueEmail();
        HttpResponseMessage corrected = await SendAsync(Admin, Customers, CustomerJson(fresh), key);

        await AssertProblemAsync(conflict, HttpStatusCode.Conflict, "customer.email_taken");
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        Assert.Equal(1L, await CustomersWithEmailAsync(fresh));
    }

    [Fact]
    public async Task Another_user_with_the_same_key_gets_their_own_execution()
    {
        string key = Quoted(NewKey());
        HttpClient staff = db.ClientAs(DevUsers.Staff);

        HttpResponseMessage admin = await SendAsync(Admin, Customers, CustomerJson(UniqueEmail()), key);
        HttpResponseMessage other = await SendAsync(staff, Customers, CustomerJson(UniqueEmail()), key);

        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        Assert.False(IsReplayed(other));
        Assert.Equal(2L, await RowsForKeyAsync(key.Trim('"')));
    }

    [Fact]
    public async Task The_same_key_on_the_other_endpoint_is_independent()
    {
        string key = Quoted(NewKey());

        HttpResponseMessage customer = await SendAsync(Admin, Customers, CustomerJson(UniqueEmail()), key);
        HttpResponseMessage hook = await SendAsync(Admin, Subscriptions, HookJson(), key);

        Assert.Equal(HttpStatusCode.Created, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Created, hook.StatusCode);
        Assert.False(IsReplayed(hook));
    }

    [Fact]
    public async Task An_expired_key_is_replaced_and_the_request_executes_again()
    {
        string key = NewKey();
        string firstEmail = UniqueEmail();
        string secondEmail = UniqueEmail();
        await SendAsync(Admin, Customers, CustomerJson(firstEmail), Quoted(key));
        await ExecAsync("UPDATE idempotency_keys SET expires_at = expires_at - interval '3 days' WHERE key = @k", ("k", key));

        HttpResponseMessage again = await SendAsync(Admin, Customers, CustomerJson(secondEmail), Quoted(key));

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.False(IsReplayed(again));
        Assert.Equal(1L, await CustomersWithEmailAsync(secondEmail));
        Assert.Equal(1L, await RowsForKeyAsync(key));
    }

    [Fact]
    public async Task Purge_removes_expired_rows_and_keeps_live_ones_without_audit_rows()
    {
        string expired = NewKey();
        string live = NewKey();
        await SendAsync(Admin, Customers, CustomerJson(UniqueEmail()), Quoted(expired));
        await SendAsync(Admin, Customers, CustomerJson(UniqueEmail()), Quoted(live));
        await ExecAsync("UPDATE idempotency_keys SET expires_at = expires_at - interval '3 days' WHERE key = @k", ("k", expired));
        long auditBefore = await ScalarAsync<long>("SELECT count(*) FROM audit_log");

        IdempotencyPurgeService purge = db.Factory.Services.GetServices<IHostedService>().OfType<IdempotencyPurgeService>().Single();
        int removed = await purge.PurgeOnceAsync(default);

        Assert.True(removed >= 1);
        Assert.Equal(0L, await RowsForKeyAsync(expired));
        Assert.Equal(1L, await RowsForKeyAsync(live));
        Assert.Equal(auditBefore, await ScalarAsync<long>("SELECT count(*) FROM audit_log"));
    }

    [Fact]
    public async Task A_webhook_create_replays_the_same_secret_and_the_stored_body_is_ciphertext()
    {
        string key = Quoted(NewKey());
        string json = HookJson();

        HttpResponseMessage first = await SendAsync(Admin, Subscriptions, json, key);
        HttpResponseMessage second = await SendAsync(Admin, Subscriptions, json, key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(IsReplayed(second));
        string secret = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("secret").GetString()!;
        Assert.Equal(secret, JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("secret").GetString());
        Assert.Contains("no-store", first.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        byte[] stored = await ScalarAsync<byte[]>("SELECT response_body FROM idempotency_keys WHERE key = @k", ("k", key.Trim('"')));
        Assert.DoesNotContain(secret, Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        Assert.False(stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)) >= 0);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM webhook_subscriptions WHERE name = 'Idem hook' AND created_at = (SELECT max(created_at) FROM webhook_subscriptions WHERE name = 'Idem hook')"));
    }

    [Fact]
    public async Task Secret_bearing_webhook_responses_are_never_cacheable_with_or_without_a_key()
    {
        HttpResponseMessage unkeyed = await SendAsync(Admin, Subscriptions, HookJson(), null);
        HttpResponseMessage keyed = await SendAsync(Admin, Subscriptions, HookJson(), Quoted(NewKey()));
        JsonElement created = JsonDocument.Parse(await unkeyed.Content.ReadAsStringAsync()).RootElement;
        using var rotate = new HttpRequestMessage(HttpMethod.Post, $"{Subscriptions}/{created.GetProperty("id").GetString()}/rotate-secret");
        rotate.Headers.TryAddWithoutValidation("If-Match", unkeyed.Headers.ETag!.Tag);
        HttpResponseMessage rotated = await Admin.SendAsync(rotate);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        foreach (HttpResponseMessage response in new[] { unkeyed, keyed, rotated })
        {
            Assert.True(response.Headers.CacheControl?.NoStore ?? false, $"{response.RequestMessage!.RequestUri} lacks Cache-Control: no-store");
        }
    }

    [Fact]
    public async Task Without_the_header_nothing_changes_and_no_key_row_is_written()
    {
        string email = UniqueEmail();
        long before = await ScalarAsync<long>("SELECT count(*) FROM idempotency_keys");

        HttpResponseMessage first = await SendAsync(Admin, Customers, CustomerJson(email), null);
        HttpResponseMessage dup = await SendAsync(Admin, Customers, CustomerJson(email), null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.False(IsReplayed(first));
        Assert.False(first.Headers.CacheControl?.NoStore ?? false);
        await AssertProblemAsync(dup, HttpStatusCode.Conflict, "customer.email_taken");
        Assert.Equal(before, await ScalarAsync<long>("SELECT count(*) FROM idempotency_keys"));
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"abc")]
    [InlineData("\"a\", \"b\"")]
    [InlineData("has space")]
    [InlineData("   ")]
    public async Task A_malformed_or_blank_key_is_400_key_invalid_and_nothing_runs(string header)
    {
        string email = UniqueEmail();

        HttpResponseMessage response = await SendAsync(Admin, Customers, CustomerJson(email), header);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "idempotency.key_invalid");
        Assert.Equal(0L, await CustomersWithEmailAsync(email));
    }

    [Fact]
    public async Task A_key_longer_than_255_characters_and_two_header_lines_are_400()
    {
        HttpResponseMessage tooLong = await SendAsync(Admin, Customers, CustomerJson(UniqueEmail()), Quoted(new string('k', 256)));
        using HttpRequestMessage twice = Post(Customers, CustomerJson(UniqueEmail()), null);
        twice.Headers.TryAddWithoutValidation("Idempotency-Key", ["\"a\"", "\"b\""]);
        HttpResponseMessage two = await Admin.SendAsync(twice);

        await AssertProblemAsync(tooLong, HttpStatusCode.BadRequest, "idempotency.key_invalid");
        await AssertProblemAsync(two, HttpStatusCode.BadRequest, "idempotency.key_invalid");
    }

    [Fact]
    public async Task A_body_over_the_limit_is_413_when_a_key_is_present_and_untouched_without_one()
    {
        string email = UniqueEmail();
        string huge = CustomerJson(email, name: new string('x', 70 * 1024));

        HttpResponseMessage keyed = await SendAsync(Admin, Customers, huge, Quoted(NewKey()));
        HttpResponseMessage unkeyed = await SendAsync(Admin, Customers, huge, null);

        await AssertProblemAsync(keyed, HttpStatusCode.RequestEntityTooLarge, "idempotency.body_too_large");
        Assert.Equal(HttpStatusCode.BadRequest, unkeyed.StatusCode); // the name is too long: the normal validation answer
        Assert.Equal(0L, await CustomersWithEmailAsync(email));
    }

    [Fact]
    public async Task A_young_pending_key_is_409_in_progress_and_an_old_one_is_409_outcome_unknown_and_neither_runs_again()
    {
        string email = UniqueEmail();
        string json = CustomerJson(email);
        string key = NewKey();
        Guid admin = await UserIdAsync(DevUsers.Admin);
        await using (AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope())
        {
            IdempotencyClaim claim = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
                .ClaimAsync(admin, "POST api/v1/customers", key, SHA256.HashData(Encoding.UTF8.GetBytes(json)), default);
            Assert.Equal(IdempotencyDecision.Execute, claim.Decision);
        }

        HttpResponseMessage young = await SendAsync(Admin, Customers, json, Quoted(key));
        await ExecAsync("UPDATE idempotency_keys SET updated_at = updated_at - interval '10 minutes' WHERE key = @k", ("k", key));
        HttpResponseMessage old = await SendAsync(Admin, Customers, json, Quoted(key));

        await AssertProblemAsync(young, HttpStatusCode.Conflict, "idempotency.in_progress");
        Assert.NotNull(young.Headers.RetryAfter);
        await AssertProblemAsync(old, HttpStatusCode.Conflict, "idempotency.outcome_unknown");
        Assert.Null(old.Headers.RetryAfter);
        Assert.Equal(0L, await CustomersWithEmailAsync(email));
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM idempotency_keys WHERE key = @k", ("k", key)));
    }

    [Fact]
    public async Task A_5xx_leaves_the_key_pending_and_a_later_retry_never_runs_the_action_again()
    {
        int calls = 0;
        WebApplicationFactory<Program> host = db.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWebhookUrlGuard>();
            services.AddSingleton<IWebhookUrlGuard>(new ThrowingGuard(() => calls++));
        }));
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));
        string key = NewKey();
        string json = HookJson();

        HttpResponseMessage failed = await SendAsync(client, Subscriptions, json, Quoted(key));
        HttpResponseMessage retry = await SendAsync(client, Subscriptions, json, Quoted(key));
        await ExecAsync("UPDATE idempotency_keys SET updated_at = updated_at - interval '10 minutes' WHERE key = @k", ("k", key));
        HttpResponseMessage later = await SendAsync(client, Subscriptions, json, Quoted(key));

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await AssertProblemAsync(retry, HttpStatusCode.Conflict, "idempotency.in_progress");
        await AssertProblemAsync(later, HttpStatusCode.Conflict, "idempotency.outcome_unknown");
        Assert.Equal(1, calls);
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM idempotency_keys WHERE key = @k", ("k", key)));
    }

    [Fact]
    public async Task A_response_over_the_size_limit_is_returned_but_not_stored_so_the_key_stays_pending()
    {
        WebApplicationFactory<Program> host = db.Factory.WithWebHostBuilder(builder => builder.UseSetting("Idempotency:MaxResponseBytes", "1"));
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));
        string email = UniqueEmail();
        string json = CustomerJson(email);
        string key = NewKey();

        HttpResponseMessage first = await SendAsync(client, Customers, json, Quoted(key));
        HttpResponseMessage retry = await SendAsync(client, Customers, json, Quoted(key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotEmpty(await first.Content.ReadAsStringAsync()); // the client still gets the response
        await AssertProblemAsync(retry, HttpStatusCode.Conflict, "idempotency.in_progress");
        Assert.Equal(1L, await CustomersWithEmailAsync(email));
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM idempotency_keys WHERE key = @k", ("k", key)));
    }

    [Fact]
    public async Task A_stale_claim_cannot_complete_or_release_a_newer_one()
    {
        Guid actor = await UserIdAsync(DevUsers.Admin);
        string key = NewKey();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("body"));
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        IIdempotencyStore store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        IdempotencyClaim stale = await store.ClaimAsync(actor, "POST api/v1/customers", key, hash, default);
        await ExecAsync("UPDATE idempotency_keys SET expires_at = expires_at - interval '3 days' WHERE key = @k", ("k", key));
        IdempotencyClaim current = await store.ClaimAsync(actor, "POST api/v1/customers", key, hash, default);

        bool staleCompleted = await store.CompleteAsync(stale.RecordId, stale.ClaimToken, 201, "{}", [1, 2, 3], default);
        bool staleReleased = await store.ReleaseAsync(stale.RecordId, stale.ClaimToken, default);

        Assert.Equal(IdempotencyDecision.Execute, current.Decision);
        Assert.False(staleCompleted);
        Assert.False(staleReleased);
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM idempotency_keys WHERE key = @k", ("k", key)));
        Assert.True(await store.CompleteAsync(current.RecordId, current.ClaimToken, 201, "{}", [1, 2, 3], default));
        Assert.Equal(IdempotencyDecision.Replay, (await store.ClaimAsync(actor, "POST api/v1/customers", key, hash, default)).Decision);
    }

    [Fact]
    public async Task A_corrupt_stored_body_answers_500_replay_unavailable_and_keeps_the_key()
    {
        string key = NewKey();
        string json = CustomerJson(UniqueEmail());
        await SendAsync(Admin, Customers, json, Quoted(key));
        await ExecAsync("UPDATE idempotency_keys SET response_body = @b WHERE key = @k", ("b", new byte[] { 1, 2, 3, 4 }), ("k", key));

        HttpResponseMessage replay = await SendAsync(Admin, Customers, json, Quoted(key));

        await AssertProblemAsync(replay, HttpStatusCode.InternalServerError, "idempotency.replay_unavailable");
        Assert.Equal(1L, await RowsForKeyAsync(key));
    }

    [Fact]
    public async Task An_unsafe_stored_header_is_refused_on_replay()
    {
        string key = NewKey();
        string json = CustomerJson(UniqueEmail());
        await SendAsync(Admin, Customers, json, Quoted(key));
        await using (var connection = new NpgsqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE idempotency_keys SET response_headers = CAST(@h AS jsonb) WHERE key = @k", connection);
            command.Parameters.AddWithValue("h", NpgsqlDbType.Text, "{\"Location\":\"/x\\r\\nSet-Cookie: a=b\"}");
            command.Parameters.AddWithValue("k", key);
            await command.ExecuteNonQueryAsync();
        }

        HttpResponseMessage replay = await SendAsync(Admin, Customers, json, Quoted(key));

        await AssertProblemAsync(replay, HttpStatusCode.InternalServerError, "idempotency.replay_unavailable");
    }

    [Fact]
    public async Task A_new_host_instance_with_the_same_key_ring_decrypts_the_stored_response()
    {
        string key = Quoted(NewKey());
        string json = CustomerJson(UniqueEmail());
        HttpResponseMessage first = await SendAsync(Admin, Customers, json, key);
        WebApplicationFactory<Program> secondHost = db.Factory.WithWebHostBuilder(_ => { }); // a new server: the key ring is read from the database again
        HttpClient client = secondHost.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));

        HttpResponseMessage replay = await SendAsync(client, Customers, json, key);

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.True(IsReplayed(replay));
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_idempotent_response_is_the_one_the_action_produced_for_a_customer()
    {
        string json = CustomerJson(UniqueEmail());
        string key = Quoted(NewKey());

        HttpResponseMessage first = await SendAsync(Admin, Customers, json, key);
        CustomerDto? dto = await first.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.NotNull(dto);
        Assert.Equal($"/api/v1/customers/{dto.Id}", first.Headers.Location!.AbsolutePath);
        Assert.Equal($"\"{dto.Version}\"", first.Headers.ETag!.Tag);
    }

    private sealed class ThrowingGuard(Action onCall) : IWebhookUrlGuard
    {
        public Task<FluentResults.Result> CheckAsync(string url, CancellationToken ct)
        {
            onCall();
            throw new InvalidOperationException("boom");
        }
    }
}
