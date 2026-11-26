using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Fugue.Application.Features.Webhooks;
using Fugue.Infrastructure.Webhooks;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Fugue.Tests.Webhooks;

internal sealed record StubRequest(IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    public string Header(string name) => Headers.TryGetValue(name, out string? v) ? v : string.Empty;

    public string BodyText => Encoding.UTF8.GetString(Body);
}

internal sealed record StubResponse(int Status, int DelayMs = 0, string? Location = null);

/// <summary>A real HTTP receiver on a loopback port that records what it gets and answers as scripted (request number, from 1, to response).</summary>
internal sealed class StubReceiver : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _count;

    private StubReceiver(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    public string Url { get; }

    public ConcurrentQueue<StubRequest> Requests { get; } = new();

    public Func<int, StubResponse> Respond { get; set; } = _ => new StubResponse(200);

    public static async Task<StubReceiver> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        StubReceiver? stub = null;
        app.Run(async context =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer);
            var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            stub!.Requests.Enqueue(new StubRequest(headers, buffer.ToArray()));
            StubResponse response = stub.Respond(Interlocked.Increment(ref stub._count));
            if (response.DelayMs > 0)
            {
                await Task.Delay(response.DelayMs, context.RequestAborted);
            }

            if (response.Location is not null)
            {
                context.Response.Headers.Location = response.Location;
            }

            context.Response.StatusCode = response.Status;
        });
        await app.StartAsync();
        stub = new StubReceiver(app, app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/') + "/hook");
        return stub;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

internal sealed record DeliveryRow(
    Guid Id, string Status, int Attempts, DateTimeOffset NextAttemptAt, DateTimeOffset? LockedUntil, int? LastStatusCode, string? LastError,
    DateTimeOffset? DeliveredAt, DateTimeOffset? DeadLetteredAt);

/// <summary>
/// A host with loopback receivers allowed and small timings, plus helpers to seed subscriptions and outbox messages straight in the database. Each test owns
/// the subscription table for its duration (it deletes the others first), so a pending message left by another test can only be marked dispatched, never sent
/// to a stray endpoint. The dispatcher uses the fixture's clock; <see cref="StartAsync"/> moves it far enough ahead that everything already pending is due.
/// </summary>
internal sealed class WebhookHarness : IAsyncDisposable
{
    private readonly PostgresFixture _db;
    private readonly DateTimeOffset _originalClock;

    private WebhookHarness(PostgresFixture db, WebApplicationFactory<Program> factory, StubReceiver stub)
    {
        _db = db;
        _originalClock = db.Clock.Now;
        Factory = factory;
        Stub = stub;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public StubReceiver Stub { get; }

    public DateTimeOffset T0 { get; } = new(2040, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public OutboxDispatcher Dispatcher => Factory.Services.GetRequiredService<OutboxDispatcher>();

    public static async Task<WebhookHarness> StartAsync(
        PostgresFixture db, Action<IServiceCollection>? services = null, IDictionary<string, string>? settings = null)
    {
        StubReceiver stub = await StubReceiver.StartAsync();
        WebApplicationFactory<Program> factory = db.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Webhooks:AllowInsecureLocal", "true");
            builder.UseSetting("Webhooks:MaxAttempts", "3");
            builder.UseSetting("Webhooks:BaseDelaySeconds", "10");
            builder.UseSetting("Webhooks:MaxDelaySeconds", "600");
            builder.UseSetting("Webhooks:AttemptTimeoutSeconds", "1");
            builder.UseSetting("Webhooks:LeaseSeconds", "30");
            builder.UseSetting("Webhooks:Dispatcher:BatchSize", "500");
            foreach ((string key, string value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }

            if (services is not null)
            {
                builder.ConfigureTestServices(services);
            }
        });

        var harness = new WebhookHarness(db, factory, stub);
        db.Clock.Now = harness.T0;
        await harness.SqlAsync("DELETE FROM webhook_subscriptions"); // the deliveries cascade
        return harness;
    }

    /// <summary>Runs cycles until one finds no work (a relay then a delivery pass is one cycle).</summary>
    public async Task<DispatchSummary> RunAsync()
    {
        DispatchSummary summary = await Dispatcher.RunOnceAsync(default);
        for (int i = 0; i < 5 && summary.Work > 0; i++)
        {
            summary = await Dispatcher.RunOnceAsync(default);
        }

        return summary;
    }

    public async Task<(Guid Id, string Secret)> AddSubscriptionAsync(string[] filters, string? url = null, bool active = true)
    {
        string secret = WebhookSecretFormat.Generate();
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        string ciphertext = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Protect(secret);
        Guid id = Guid.NewGuid();
        await SqlAsync(
            "INSERT INTO webhook_subscriptions (id, name, url, event_types, is_active, secret_ciphertext, created_at, created_by, updated_at, updated_by) " +
            "VALUES (@id, 'harness', @url, @types, @active, @c, now(), 'test', now(), 'test')",
            ("id", id), ("url", url ?? Stub.Url), ("types", filters), ("active", active), ("c", ciphertext));
        return (id, secret);
    }

    public async Task<Guid> AddMessageAsync(string eventType, string aggregateId, string payload = "{\"n\":1}", string? correlation = "corr-1", string saga = "saga-1", string? traceparent = null)
    {
        Guid id = Guid.NewGuid();
        await SqlAsync(
            "INSERT INTO outbox_messages (id, occurred_at, event_type, aggregate_type, aggregate_id, payload, correlation_id, saga_id, traceparent, status, attempts, next_attempt_at) " +
            "VALUES (@id, @at, @type, 'thing', @agg, CAST(@payload AS jsonb), @corr, @saga, @trace, 'pending', 0, @at)",
            ("id", id), ("at", _db.Clock.Now), ("type", eventType), ("agg", aggregateId), ("payload", payload),
            ("corr", (object?)correlation ?? DBNull.Value), ("saga", saga), ("trace", (object?)traceparent ?? DBNull.Value));
        return id;
    }

    public async Task<List<DeliveryRow>> DeliveriesAsync(Guid messageId)
    {
        await using var connection = new NpgsqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id, status, attempts, next_attempt_at, locked_until, last_status_code, last_error, delivered_at, dead_lettered_at FROM webhook_deliveries WHERE message_id = @m ORDER BY occurred_at, id",
            connection);
        command.Parameters.AddWithValue("m", messageId);
        var rows = new List<DeliveryRow>();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new DeliveryRow(
                r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetFieldValue<DateTimeOffset>(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
                r.IsDBNull(5) ? null : r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7),
                r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8)));
        }

        return rows;
    }

    public Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters) => QueryAsync<T>(sql, parameters);

    public async Task SqlAsync(string sql, params (string Name, object Value)[] parameters) => await QueryAsync<object>(sql, parameters, execute: true);

    private async Task<T> QueryAsync<T>(string sql, (string Name, object Value)[] parameters, bool execute = false)
    {
        await using var connection = new NpgsqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        if (execute)
        {
            await command.ExecuteNonQueryAsync();
            return default!;
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>The documented algorithm, written out independently of the production code: HMAC-SHA256 keyed with the secret string over "{timestamp}.{rawBody}", hex, prefixed v1=.</summary>
    public static string ExpectedSignature(string secret, string timestamp, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] message = [.. Encoding.UTF8.GetBytes(timestamp + "."), .. body];
        return "v1=" + Convert.ToHexString(hmac.ComputeHash(message)).ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        _db.Clock.Now = _originalClock;
        await Stub.DisposeAsync();
        await Factory.DisposeAsync();
    }
}
