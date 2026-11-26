using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Tests.Audit;
using Fugue.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Webhooks;

/// <summary>The dispatcher against a real receiver and real Postgres (ADR-011, D-039): signing, retry with backoff, dead letter, redeliver, concurrency, SSRF at connect time.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class WebhookDeliveryTests(PostgresFixture db)
{
    private static string NewType() => "t" + Guid.NewGuid().ToString("N")[..10] + ".thing.v1";

    [Fact]
    public async Task A_delivery_carries_a_valid_signature_and_every_documented_header()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        string type = NewType();
        (_, string secret) = await h.AddSubscriptionAsync([type]);
        const string trace = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        Guid message = await h.AddMessageAsync(type, "agg-1", "{\"n\":1}", "corr-9", "saga-9", trace);

        await h.RunAsync();

        StubRequest request = Assert.Single(h.Stub.Requests);
        string timestamp = request.Header("X-Fugue-Timestamp");
        Assert.Equal(WebhookHarness.ExpectedSignature(secret, timestamp, request.Body), request.Header("X-Fugue-Signature"));
        Assert.NotEqual(WebhookHarness.ExpectedSignature("whsec_wrong", timestamp, request.Body), request.Header("X-Fugue-Signature"));
        Assert.Equal(h.T0.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), timestamp);
        Assert.Equal((message.ToString(), type, "corr-9", "saga-9", trace),
            (request.Header("X-Fugue-Event-Id"), request.Header("X-Fugue-Event-Type"), request.Header("X-Correlation-Id"), request.Header("X-Saga-Id"), request.Header("traceparent")));
        using JsonDocument body = JsonDocument.Parse(request.Body);
        Assert.Equal((message.ToString(), type, 1), (body.RootElement.GetProperty("id").GetString(), body.RootElement.GetProperty("type").GetString(), body.RootElement.GetProperty("data").GetProperty("n").GetInt32()));
        DeliveryRow row = Assert.Single(await h.DeliveriesAsync(message));
        Assert.Equal(("succeeded", 1, 200), (row.Status, row.Attempts, row.LastStatusCode));
        Assert.Equal("dispatched", await h.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @i", ("i", message)));
    }

    [Fact]
    public async Task A_500_is_retried_after_a_backoff_and_then_succeeds()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        string type = NewType();
        await h.AddSubscriptionAsync([type]);
        Guid message = await h.AddMessageAsync(type, "agg-2");
        h.Stub.Respond = n => new StubResponse(n == 1 ? 500 : 200);

        await h.RunAsync();
        DeliveryRow first = Assert.Single(await h.DeliveriesAsync(message));
        await h.RunAsync(); // not due yet: no second request
        Assert.Single(h.Stub.Requests);

        Assert.Equal(("pending", 1, 500), (first.Status, first.Attempts, first.LastStatusCode));
        Assert.InRange((first.NextAttemptAt - h.T0).TotalSeconds, 5, 10); // base 10s, equal jitter: [5s, 10s]
        db.Clock.Now = h.T0.AddSeconds(11);
        await h.RunAsync();
        DeliveryRow second = Assert.Single(await h.DeliveriesAsync(message));
        Assert.Equal(("succeeded", 2), (second.Status, second.Attempts));
        Assert.Equal(2, h.Stub.Requests.Count);
    }

    [Fact]
    public async Task Failures_up_to_the_limit_dead_letter_the_delivery_and_redeliver_revives_it()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        string type = NewType();
        await h.AddSubscriptionAsync([type]);
        Guid message = await h.AddMessageAsync(type, "agg-3");
        h.Stub.Respond = _ => new StubResponse(500);

        for (int i = 0; i < 3; i++)
        {
            await h.RunAsync();
            db.Clock.Now = db.Clock.Now.AddHours(1);
        }

        await h.RunAsync(); // dead: nothing more is sent
        DeliveryRow dead = Assert.Single(await h.DeliveriesAsync(message));
        Assert.Equal(("dead", 3, "HTTP 500"), (dead.Status, dead.Attempts, dead.LastError));
        Assert.NotNull(dead.DeadLetteredAt);
        Assert.Equal(3, h.Stub.Requests.Count);

        HttpClient admin = db.ClientAs(DevUsers.Admin);
        h.Stub.Respond = _ => new StubResponse(200);
        HttpResponseMessage redeliver = await admin.PostAsync($"/api/v1/webhooks/deliveries/{dead.Id}/redeliver", null);
        Assert.Equal(HttpStatusCode.NoContent, redeliver.StatusCode);
        Assert.Equal(("pending", 0), ((await h.DeliveriesAsync(message))[0].Status, (await h.DeliveriesAsync(message))[0].Attempts));
        await h.RunAsync();
        Assert.Equal("succeeded", (await h.DeliveriesAsync(message))[0].Status);
        Assert.Equal(4, h.Stub.Requests.Count);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/webhooks/deliveries/{dead.Id}/redeliver", null)).StatusCode); // no longer dead
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/v1/webhooks/deliveries/{Guid.NewGuid()}/redeliver", null)).StatusCode);
        AuditRow audit = Assert.Single(await AuditProbe.ByEntityAsync(db, "WebhookDelivery", dead.Id.ToString()));
        Assert.Equal("webhooks.redeliver", audit.Action);
    }

    [Fact]
    public async Task Two_dispatchers_over_the_same_messages_deliver_each_exactly_once()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db, settings: new Dictionary<string, string> { ["Webhooks:Dispatcher:BatchSize"] = "7" });
        string type = NewType();
        await h.AddSubscriptionAsync([type]);
        var messages = new List<Guid>();
        for (int i = 0; i < 30; i++)
        {
            messages.Add(await h.AddMessageAsync(type, "agg-" + i));
        }

        for (int round = 0; round < 8; round++)
        {
            await Task.WhenAll(h.Dispatcher.RunOnceAsync(default), h.Dispatcher.RunOnceAsync(default));
        }

        string[] ids = h.Stub.Requests.Select(r => r.Header("X-Fugue-Event-Id")).ToArray();
        Assert.Equal(30, ids.Length);
        Assert.Equal(messages.Select(m => m.ToString()).Order(), ids.Order());
        foreach (Guid message in messages)
        {
            Assert.Single(await h.DeliveriesAsync(message));
        }
    }

    [Fact]
    public async Task A_slow_receiver_times_out_at_the_attempt_timeout_and_is_retried()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        string type = NewType();
        await h.AddSubscriptionAsync([type]);
        Guid message = await h.AddMessageAsync(type, "agg-4");
        h.Stub.Respond = _ => new StubResponse(200, DelayMs: 8000);

        await h.RunAsync();

        DeliveryRow row = Assert.Single(await h.DeliveriesAsync(message));
        Assert.Equal(("pending", 1), (row.Status, row.Attempts));
        Assert.Contains("Timeout", row.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_redirect_is_not_followed_and_counts_as_a_failure()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        await using StubReceiver elsewhere = await StubReceiver.StartAsync();
        string type = NewType();
        await h.AddSubscriptionAsync([type]);
        Guid message = await h.AddMessageAsync(type, "agg-5");
        h.Stub.Respond = _ => new StubResponse(302, Location: elsewhere.Url);

        await h.RunAsync();

        DeliveryRow row = Assert.Single(await h.DeliveriesAsync(message));
        Assert.Contains("redirects are not followed", row.LastError, StringComparison.Ordinal);
        Assert.Empty(elsewhere.Requests);
    }

    [Fact]
    public async Task Inactive_subscriptions_get_no_delivery_and_a_non_matching_filter_gets_none()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        string type = NewType();
        await h.AddSubscriptionAsync([type], active: false);
        await h.AddSubscriptionAsync(["other.*"]);
        Guid message = await h.AddMessageAsync(type, "agg-6");

        await h.RunAsync();

        Assert.Empty(await h.DeliveriesAsync(message));
        Assert.Empty(h.Stub.Requests);
        Assert.Equal("dispatched", await h.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @i", ("i", message)));
    }

    [Fact]
    public async Task A_publisher_that_throws_is_isolated_retried_and_finally_marks_the_message_dead()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db, services => services.AddScoped<IEventPublisher, PoisonPublisher>());
        Guid poison = await h.AddMessageAsync("poison.thing.v1", "agg-7");
        string type = NewType();
        await h.AddSubscriptionAsync([type]);
        Guid healthy = await h.AddMessageAsync(type, "agg-8");

        await h.Dispatcher.RunOnceAsync(default);

        Assert.Equal(("pending", 1), (await Row(h, poison)));
        Assert.Contains("boom", await h.ScalarAsync<string>("SELECT last_error FROM outbox_messages WHERE id = @i", ("i", poison)), StringComparison.Ordinal);
        Assert.Equal("succeeded", (await h.DeliveriesAsync(healthy))[0].Status); // the batch survived the savepoint rollback
        for (int i = 0; i < 2; i++)
        {
            db.Clock.Now = db.Clock.Now.AddHours(1);
            await h.Dispatcher.RunOnceAsync(default);
        }

        Assert.Equal(("dead", 3), (await Row(h, poison)));
    }

    private static async Task<(string, int)> Row(WebhookHarness h, Guid id) =>
        (await h.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @i", ("i", id)), await h.ScalarAsync<int>("SELECT attempts FROM outbox_messages WHERE id = @i", ("i", id)));

    [Fact]
    public async Task A_customer_change_flows_end_to_end_with_masked_data_and_the_request_correlation_id()
    {
        await using WebhookHarness h = await WebhookHarness.StartAsync(db);
        (_, string secret) = await h.AddSubscriptionAsync(["customer.*"]);
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string correlation = "e2e-" + Guid.NewGuid().ToString("N");
        string email = $"e{Guid.NewGuid():N}@example.test";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers") { Content = JsonContent.Create(new { name = "End To End", email, phone = "+14155550199" }) };
        request.Headers.Add("X-Correlation-Id", correlation);
        HttpResponseMessage created = await admin.SendAsync(request);
        string id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;

        await h.RunAsync();

        StubRequest delivered = Assert.Single(h.Stub.Requests, r => r.Header("X-Correlation-Id") == correlation);
        Assert.Equal("customer.created.v1", delivered.Header("X-Fugue-Event-Type"));
        Assert.Equal(id, delivered.Header("X-Saga-Id")); // saga defaults to the aggregate id
        Assert.Equal(WebhookHarness.ExpectedSignature(secret, delivered.Header("X-Fugue-Timestamp"), delivered.Body), delivered.Header("X-Fugue-Signature"));
        Assert.DoesNotContain(email, delivered.BodyText, StringComparison.Ordinal);
        Assert.DoesNotContain("4155550199", delivered.BodyText, StringComparison.Ordinal);

        HttpResponseMessage list = await admin.GetAsync($"/api/v1/webhooks/deliveries?status=succeeded&eventType=customer.created.v1&limit=200");
        Assert.Contains(id, await list.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, (await db.ClientAs(DevUsers.Staff).GetAsync("/api/v1/webhooks/deliveries")).StatusCode);
    }

    private sealed class PoisonPublisher : IEventPublisher
    {
        public Task PublishAsync(OutboxEvent message, CancellationToken ct) =>
            message.EventType == "poison.thing.v1" ? throw new InvalidOperationException("boom") : Task.CompletedTask;
    }
}

/// <summary>Connect-time SSRF guard: a name that resolves to a forbidden address never gets a connection, and an allowed one is dialled by its resolved IP.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class GuardedConnectTests(PostgresFixture db)
{
    [Fact]
    public async Task A_rebound_name_cannot_reach_loopback_unless_local_is_allowed()
    {
        await using StubReceiver stub = await StubReceiver.StartAsync();
        int port = new Uri(stub.Url).Port;
        var evt = new OutboxEvent(Guid.NewGuid(), DateTimeOffset.UnixEpoch, "x.y.v1", "x", "1", "{}", null, "1", null);

        foreach ((bool allowLocal, bool reachable) in new[] { (false, false), (true, true) })
        {
            var options = Microsoft.Extensions.Options.Options.Create(new Fugue.Infrastructure.Webhooks.WebhookOptions { AllowInsecureLocal = allowLocal });
            var connect = new Fugue.Infrastructure.Webhooks.GuardedConnect(new Rebinding(), options);
            using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, ConnectCallback = connect.ConnectAsync });
            var sender = new Fugue.Infrastructure.Webhooks.WebhookSender(http, db.Clock);

            Fugue.Infrastructure.Webhooks.SendResult result = await sender.SendAsync(new Uri($"http://rebind.example.test:{port}/hook"), "whsec_x", evt, default);

            Assert.Equal(reachable, result.Success);
            if (!reachable)
            {
                Assert.Contains("not allowed", result.Error, StringComparison.Ordinal);
            }
        }

        Assert.Single(stub.Requests);
    }

    private sealed class Rebinding : Fugue.Infrastructure.Webhooks.IHostResolver
    {
        public Task<System.Net.IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(new[] { System.Net.IPAddress.Loopback });
    }
}
