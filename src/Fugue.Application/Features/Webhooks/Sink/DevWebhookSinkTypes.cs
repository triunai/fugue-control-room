namespace Fugue.Application.Features.Webhooks.Sink;

/// <summary>One webhook the Development sink accepted: verified signature, fresh timestamp, first time this event id was seen.</summary>
public sealed record ReceivedWebhookDto(
    string EventId,
    string EventType,
    DateTimeOffset ReceivedAt,
    long Timestamp,
    string? CorrelationId,
    string? SagaId,
    string? Traceparent,
    string Body);

/// <summary>The sink's answer to a delivery: <c>accepted</c> the first time, <c>duplicate</c> when the event id was already seen (at-least-once delivery).</summary>
public sealed record DevWebhookReceiptDto(string Status, string EventId);

/// <summary>
/// The Development-only in-memory receiver state (ADR-011, D-040): the secret it trusts, the last N accepted events and the event ids it has seen.
/// Implemented in Infrastructure as a singleton; nothing here is persisted.
/// </summary>
public interface IDevWebhookSink
{
    string? Secret { get; set; }

    /// <summary>False when the event id was already seen (a duplicate delivery); the event is then not stored again.</summary>
    bool TryAdd(ReceivedWebhookDto received);

    IReadOnlyList<ReceivedWebhookDto> Latest(int take);

    void Clear();
}
