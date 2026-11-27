using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Fugue.Tests.Persistence;
using Fugue.Tests.Webhooks;
using Testcontainers.Redpanda;

namespace Fugue.Tests.Messaging;

/// <summary>The Kafka adapter against a real broker and real Postgres (ADR-011 amendment): key, headers, order, and no ack means not dispatched.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class KafkaPublisherTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly RedpandaContainer _broker = new RedpandaBuilder("docker.redpanda.com/redpandadata/redpanda:v24.3.18").Build();

    public Task InitializeAsync() => _broker.StartAsync();

    public Task DisposeAsync() => _broker.DisposeAsync().AsTask();

    private static string NewType() => "claim.t" + Guid.NewGuid().ToString("N")[..10] + ".v1";

    private Task<WebhookHarness> StartAsync(string bootstrap) => WebhookHarness.StartAsync(db, settings: new Dictionary<string, string>
    {
        ["Events:Publisher"] = "Kafka",
        ["Events:Kafka:BootstrapServers"] = bootstrap,
        ["Events:Kafka:MessageTimeoutMs"] = "3000",
    });

    private List<ConsumeResult<string, string>> ConsumeKey(string topic, string key, int count)
    {
        using IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _broker.GetBootstrapAddress(),
            GroupId = Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(topic);
        var found = new List<ConsumeResult<string, string>>();
        DateTime deadline = DateTime.UtcNow.AddSeconds(45);
        while (found.Count < count && DateTime.UtcNow < deadline)
        {
            ConsumeResult<string, string>? result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result is not null && result.Message.Key == key)
            {
                found.Add(result);
            }
        }

        return found;
    }

    private static string Header(ConsumeResult<string, string> r, string name) => Encoding.UTF8.GetString(r.Message.Headers.GetLastBytes(name));

    [Fact]
    public async Task A_claim_event_reaches_the_claims_topic_keyed_by_the_claim_id_with_its_headers_and_in_order()
    {
        await using WebhookHarness h = await StartAsync(_broker.GetBootstrapAddress());
        string claimId = "CLM-" + Guid.NewGuid().ToString("N")[..8];
        string type = NewType();
        const string trace = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        Guid first = await h.AddMessageAsync(type, claimId, "{\"n\":1}", "corr-k", "saga-k", trace);
        db.Clock.Now = h.T0.AddSeconds(1);
        Guid second = await h.AddMessageAsync(type, claimId, "{\"n\":2}", "corr-k", "saga-k", trace);
        db.Clock.Now = h.T0.AddSeconds(2);

        await h.RunAsync();

        List<ConsumeResult<string, string>> got = ConsumeKey("fugue.claims.v1", claimId, 2);
        Assert.Equal(2, got.Count);
        Assert.Equal([first.ToString(), second.ToString()], got.Select(r => Header(r, "event-id")));
        Assert.Equal((type, "corr-k", "saga-k", trace), (Header(got[0], "event-type"), Header(got[0], "correlation-id"), Header(got[0], "saga-id"), Header(got[0], "traceparent")));
        using JsonDocument body = JsonDocument.Parse(got[1].Message.Value);
        Assert.Equal((second.ToString(), claimId, 2), (body.RootElement.GetProperty("id").GetString(), body.RootElement.GetProperty("aggregateId").GetString(), body.RootElement.GetProperty("data").GetProperty("n").GetInt32()));
        Assert.Equal("dispatched", await h.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @i", ("i", first)));
        Assert.Equal("dispatched", await h.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @i", ("i", second)));
    }

    [Fact]
    public async Task With_no_broker_the_outbox_row_is_not_marked_dispatched_and_is_retried_later()
    {
        await using WebhookHarness h = await StartAsync("127.0.0.1:1");
        Guid message = await h.AddMessageAsync(NewType(), "CLM-down");

        await h.Dispatcher.RunOnceAsync(default);

        Assert.Equal("pending", await h.ScalarAsync<string>("SELECT status FROM outbox_messages WHERE id = @i", ("i", message)));
        Assert.Equal(1, await h.ScalarAsync<int>("SELECT attempts FROM outbox_messages WHERE id = @i", ("i", message)));
        Assert.Contains("ProduceException", await h.ScalarAsync<string>("SELECT last_error FROM outbox_messages WHERE id = @i", ("i", message)));
    }
}
