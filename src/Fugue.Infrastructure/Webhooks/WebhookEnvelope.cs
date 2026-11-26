using System.Text.Json.Nodes;
using Fugue.Application.Abstractions;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// The JSON body of a delivery. It is built once per send and the signature covers exactly the bytes returned here, so what a receiver verifies is what it
/// received. <c>data</c> is the masked payload stored with the outbox row (ADR-011).
/// </summary>
internal static class WebhookEnvelope
{
    public static byte[] Build(OutboxEvent message)
    {
        var body = new JsonObject
        {
            ["id"] = message.Id.ToString(),
            ["type"] = message.EventType,
            ["occurredAt"] = message.OccurredAt.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["aggregateType"] = message.AggregateType,
            ["aggregateId"] = message.AggregateId,
            ["correlationId"] = message.CorrelationId,
            ["sagaId"] = message.SagaId,
            ["data"] = JsonNode.Parse(message.Payload),
        };
        return System.Text.Encoding.UTF8.GetBytes(body.ToJsonString());
    }
}
