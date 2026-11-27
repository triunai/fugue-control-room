using System.Text;
using Confluent.Kafka;
using Fugue.Application.Abstractions;
using Fugue.Infrastructure.Webhooks;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Messaging;

/// <summary>Topic names per event family (the first segment of the event type).</summary>
internal static class KafkaPublisherTopics
{
    private static readonly Dictionary<string, string> Families = new(StringComparer.Ordinal)
    {
        ["claim"] = "fugue.claims.v1",
        ["customer"] = "fugue.customers.v1",
    };

    public static string For(string eventType, string fallback)
    {
        int dot = eventType.IndexOf('.', StringComparison.Ordinal);
        string family = dot < 0 ? eventType : eventType[..dot];
        return Families.TryGetValue(family, out string? topic) ? topic : fallback;
    }
}

/// <summary>
/// The Kafka adapter of <see cref="IEventPublisher"/> (ADR-011 amendment). One topic per event family, message key = the aggregate id (per-aggregate order inside a
/// partition), value = the same JSON envelope the webhooks sign, headers carry the event type, event id, correlation id, saga id and traceparent.
/// The delivery report is AWAITED, so the relay marks the outbox row dispatched only after the broker acknowledged it (the producer is idempotent with acks=all).
/// A failed or timed out produce throws, which sends the message through the relay's retry with backoff. At least once: consumers dedupe on <c>event-id</c>.
/// </summary>
internal sealed class KafkaEventPublisher(IProducer<string, string> producer, IOptions<EventPublisherOptions> options) : IEventPublisher
{
    public async Task PublishAsync(OutboxEvent message, CancellationToken ct)
    {
        var headers = new Headers
        {
            { "event-type", Encoding.UTF8.GetBytes(message.EventType) },
            { "event-id", Encoding.UTF8.GetBytes(message.Id.ToString()) },
            { "saga-id", Encoding.UTF8.GetBytes(message.SagaId) },
        };
        if (message.CorrelationId is not null)
        {
            headers.Add("correlation-id", Encoding.UTF8.GetBytes(message.CorrelationId));
        }

        if (message.Traceparent is not null)
        {
            headers.Add("traceparent", Encoding.UTF8.GetBytes(message.Traceparent));
        }

        string topic = KafkaPublisherTopics.For(message.EventType, options.Value.Kafka.DefaultTopic);
        await producer.ProduceAsync(
            topic,
            new Message<string, string>
            {
                Key = message.AggregateId,
                Value = Encoding.UTF8.GetString(WebhookEnvelope.Build(message)),
                Headers = headers,
                Timestamp = new Timestamp(message.OccurredAt),
            },
            ct);
    }

    /// <summary>The producer settings that make the guarantee: idempotent, all in-sync replicas, bounded wait.</summary>
    internal static ProducerConfig ProducerConfig(KafkaOptions kafka) => new()
    {
        BootstrapServers = kafka.BootstrapServers,
        EnableIdempotence = true,
        Acks = Acks.All,
        MessageTimeoutMs = kafka.MessageTimeoutMs,
        ClientId = "fugue-api",
    };
}
