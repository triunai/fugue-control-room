using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Idempotency;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Fugue.Tests.Idempotency;

/// <summary>
/// Failures injected AFTER the business commit (ADR-013): the work is done, the outcome is not recorded. In every case the key must stay pending (never
/// freed, never re-run), a later retry must be 409 <c>idempotency.outcome_unknown</c>, and there must be exactly one customer, one audit success row and one
/// outbox row. Also: a chunked body over the limit is refused without a Content-Length to rely on.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class IdempotencyFailureTests(PostgresFixture db)
{
    private const string Customers = "/api/v1/customers";

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

    private async Task<HttpClient> HostAsync(Action<IServiceCollection> configure)
    {
        WebApplicationFactory<Program> host = db.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(configure));
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));
        return client;
    }

    private static async Task<HttpResponseMessage?> TryPostAsync(HttpClient client, string json, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Customers) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"\"{key}\"");
        try
        {
            return await client.SendAsync(request);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or HttpRequestException)
        {
            return null; // the host surfaced the injected failure to the test client
        }
    }

    private async Task AssertCommittedOnceAndKeyPendingAsync(string email, string key)
    {
        string id = (await ScalarAsync<Guid>("SELECT id FROM customers WHERE email = @e", ("e", email))).ToString();
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM customers WHERE email = @e", ("e", email)));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE entity_id = @id AND outcome = 'success'", ("id", id)));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM outbox_messages WHERE aggregate_id = @id", ("id", id)));
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM idempotency_keys WHERE key = @k", ("k", key)));

        // Older than InProgressSeconds: the retry is told the outcome is unknown and nothing runs again.
        await using (var connection = new NpgsqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var age = new NpgsqlCommand("UPDATE idempotency_keys SET updated_at = updated_at - interval '10 minutes' WHERE key = @k", connection);
            age.Parameters.AddWithValue("k", key);
            await age.ExecuteNonQueryAsync();
        }

        HttpClient normal = db.ClientAs(DevUsers.Admin);
        HttpResponseMessage retry = (await TryPostAsync(normal, CustomerJson(email), key))!;
        using JsonDocument problem = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        Assert.Equal("idempotency.outcome_unknown", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM customers WHERE email = @e", ("e", email)));
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM idempotency_keys WHERE key = @k", ("k", key)));
    }

    private static string CustomerJson(string email) => JsonSerializer.Serialize(new { name = "Failure Customer", email });

    [Fact]
    public async Task A_completion_store_failure_after_the_commit_keeps_the_key_pending()
    {
        HttpClient client = await HostAsync(services => services.Replace(ServiceDescriptor.Scoped<IIdempotencyStore>(sp =>
            new FailingStore(ActivatorUtilities.CreateInstance<IdempotencyStore>(sp), new InvalidOperationException("completion store is down")))));
        string email = $"{Guid.NewGuid():N}@example.test";
        string key = Guid.NewGuid().ToString("N");

        HttpResponseMessage? first = await TryPostAsync(client, CustomerJson(email), key);

        Assert.Equal(HttpStatusCode.Created, first!.StatusCode); // the client still gets the response the action produced
        await AssertCommittedOnceAndKeyPendingAsync(email, key);
    }

    [Fact]
    public async Task A_cancellation_after_the_commit_keeps_the_key_pending()
    {
        HttpClient client = await HostAsync(services => services.Replace(ServiceDescriptor.Scoped<IIdempotencyStore>(sp =>
            new FailingStore(ActivatorUtilities.CreateInstance<IdempotencyStore>(sp), new OperationCanceledException("the client went away")))));
        string email = $"{Guid.NewGuid():N}@example.test";
        string key = Guid.NewGuid().ToString("N");

        _ = await TryPostAsync(client, CustomerJson(email), key);

        await AssertCommittedOnceAndKeyPendingAsync(email, key);
    }

    [Fact]
    public async Task An_exception_while_writing_the_response_keeps_the_key_pending()
    {
        HttpClient client = await HostAsync(services => services.Configure<MvcOptions>(o => o.Filters.Add(new ThrowAfterCreateFilter())));
        string email = $"{Guid.NewGuid():N}@example.test";
        string key = Guid.NewGuid().ToString("N");

        HttpResponseMessage? first = await TryPostAsync(client, CustomerJson(email), key);

        Assert.True(first is null || first.StatusCode == HttpStatusCode.InternalServerError);
        await AssertCommittedOnceAndKeyPendingAsync(email, key);
    }

    [Fact]
    public async Task A_chunked_body_over_the_limit_is_413_without_a_content_length()
    {
        HttpClient client = db.ClientAs(DevUsers.Admin);
        string email = $"{Guid.NewGuid():N}@example.test";
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { name = new string('x', 80 * 1024), email }));
        using var content = new StreamContent(new ForwardOnlyStream(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, Customers) { Content = content };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"\"{Guid.NewGuid():N}\"");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Null(request.Content.Headers.ContentLength);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM customers WHERE email = @e", ("e", email)));
    }

    private sealed class FailingStore(IIdempotencyStore inner, Exception failure) : IIdempotencyStore
    {
        public Task<IdempotencyClaim> ClaimAsync(Guid actorId, string endpoint, string key, byte[] requestHash, CancellationToken ct) =>
            inner.ClaimAsync(actorId, endpoint, key, requestHash, ct);

        public Task<bool> CompleteAsync(Guid recordId, Guid claimToken, int status, string headersJson, byte[] body, CancellationToken ct) =>
            Task.FromException<bool>(failure);

        public Task<bool> ReleaseAsync(Guid recordId, Guid claimToken, CancellationToken ct) => inner.ReleaseAsync(recordId, claimToken, ct);

        public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct) => inner.PurgeExpiredAsync(batchSize, ct);
    }

    /// <summary>Throws once the create has committed and its result is being executed.</summary>
    private sealed class ThrowAfterCreateFilter : IAsyncResultFilter
    {
        public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next) =>
            context.Result is CreatedAtActionResult ? throw new InvalidOperationException("writing the response failed") : next();
    }

    /// <summary>A readable stream with no length and no seeking, so HttpClient sends it chunked.</summary>
    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
