using Fugue.Domain.Audit;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Webhooks;

/// <summary>
/// One event waiting to leave the system (ADR-011). Written in the SAME transaction as the business change it describes, so the event
/// exists if and only if the change committed. Not <c>IAuditable</c>: it has <see cref="OccurredAt"/> and its lifecycle columns are
/// moved by the dispatcher; the immutable columns (everything except status, attempts, next attempt, error and dispatch time) are
/// frozen by a database trigger. <see cref="Payload"/> is masked before it is stored and never holds a secret.
/// </summary>
[NoAudit]
public sealed class OutboxMessage
{
    public OutboxMessageId Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Versioned name, for example <c>customer.created.v1</c>.</summary>
    public string EventType { get; set; } = string.Empty;

    public string AggregateType { get; set; } = string.Empty;

    public string AggregateId { get; set; } = string.Empty;

    /// <summary>jsonb: the masked, thin event data (ids and the changed state, partially masked personal data).</summary>
    public string Payload { get; set; } = "{}";

    public string? CorrelationId { get; set; }

    /// <summary>The business process the event belongs to; the aggregate id when the request carried none.</summary>
    public string SagaId { get; set; } = string.Empty;

    /// <summary>W3C <c>traceparent</c> of the operation that produced the event, so the receiver can continue the trace.</summary>
    public string? Traceparent { get; set; }

    public string Status { get; set; } = OutboxStatuses.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? DispatchedAt { get; set; }
}

public static class OutboxStatuses
{
    public const string Pending = "pending";
    public const string Dispatched = "dispatched";
    public const string Dead = "dead";
}
