using Fugue.Domain.Events;

namespace Fugue.Application.Abstractions;

/// <summary>
/// Explicit outbox write for paths the EF change tracker cannot see (a Dapper insert), the counterpart of <see cref="IAuditTrail"/> (ADR-011).
/// The row runs on EF's connection and current transaction (<c>SqlExecutor</c>), so inside a unit of work it commits or rolls back with the change.
/// EF-tracked aggregates need no call: the SaveChanges interceptor drains their events in the same SaveChanges.
/// </summary>
public interface IOutbox
{
    /// <summary>Writes one outbox row per pending event of <paramref name="aggregate"/>, then clears them.</summary>
    Task EnqueueAsync(IHasDomainEvents aggregate, CancellationToken ct);
}

/// <summary>An outbox message as the relay hands it to a publisher. <see cref="Payload"/> is the masked JSON stored with the event.</summary>
public sealed record OutboxEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string AggregateType,
    string AggregateId,
    string Payload,
    string? CorrelationId,
    string SagaId,
    string? Traceparent);

/// <summary>
/// The broker seam (ADR-011, D-040). The relay calls every registered publisher for each claimed outbox message; a publisher throws to
/// ask for a retry with backoff. The webhook fan-out is the first adapter; a Kafka adapter would produce to a topic keyed by
/// <see cref="OutboxEvent.AggregateId"/> (per-aggregate order) with <see cref="OutboxEvent.Id"/> as the idempotency key. Delivery is at-least-once.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(OutboxEvent message, CancellationToken ct);
}
