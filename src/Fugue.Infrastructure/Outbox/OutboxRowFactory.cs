using System.Diagnostics;
using System.Text.RegularExpressions;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Events;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;

namespace Fugue.Infrastructure.Outbox;

/// <summary>
/// Builds <see cref="OutboxMessage"/> rows with everything that is the same for every writer: the clock (the shared <see cref="IAuditStamp"/>),
/// the correlation and saga ids (ADR-005; the saga id is the aggregate id when the request carried none), the trace context and the masked payload.
/// The SaveChanges interceptor and <see cref="OutboxWriter"/> both use it, so rows look the same whichever path wrote them.
/// </summary>
internal sealed partial class OutboxRowFactory(IAuditStamp stamp, ICorrelationContext correlation, OutboxPayloadFactory payloads)
{
    [GeneratedRegex("^[0-9a-f]{2}-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$")]
    private static partial Regex W3cTraceparent();

    public OutboxMessage Create(IDomainEvent evt)
    {
        DateTimeOffset now = stamp.Capture().Now;
        string? traceparent = Activity.Current?.Id;
        string aggregateId = Clip(evt.AggregateId, 128)!;

        return new OutboxMessage
        {
            Id = OutboxMessageId.New(),
            OccurredAt = now,
            EventType = Clip(evt.EventType, 100)!,
            AggregateType = Clip(evt.AggregateType, 64)!,
            AggregateId = aggregateId,
            Payload = payloads.Build(evt),
            CorrelationId = Clip(string.IsNullOrEmpty(correlation.CorrelationId) ? null : correlation.CorrelationId, 64),
            SagaId = Clip(correlation.SagaId ?? aggregateId, 64)!,
            Traceparent = traceparent is not null && W3cTraceparent().IsMatch(traceparent) ? traceparent : null,
            Status = OutboxStatuses.Pending,
            NextAttemptAt = now,
        };
    }

    private static string? Clip(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
