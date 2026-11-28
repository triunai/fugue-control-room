namespace Fugue.Application.Features.Audit.Repos;

/// <summary>
/// Read side of "The Score" (D-064): every stored row one correlation id left behind, oldest first. Reads <c>audit_log</c>,
/// <c>outbox_messages</c> and <c>webhook_deliveries</c>; nothing is synthesised.
/// </summary>
public interface IAuditTraceReader
{
    Task<IReadOnlyList<AuditTraceEntry>> ReadAsync(string correlationId, CancellationToken ct);
}

/// <summary>
/// One stored row of a trace. <paramref name="Kind"/> is <see cref="AuditTraceKinds.Command"/> (an audit row), <see cref="AuditTraceKinds.Event"/>
/// (an outbox message) or <see cref="AuditTraceKinds.Delivery"/> (a webhook delivery of one of those messages).
/// </summary>
public sealed record AuditTraceEntry(
    string Kind,
    Guid Id,
    DateTimeOffset At,
    DateTimeOffset? EndAt,
    string Label,
    string? Detail,
    string Status,
    int? DurationMs,
    int? Attempts,
    int? StatusCode,
    string? ErrorCode,
    string? EntityType,
    string? EntityId);

public static class AuditTraceKinds
{
    public const string Command = "command";
    public const string Event = "event";
    public const string Delivery = "delivery";
}

/// <summary>
/// The trace of one correlation id, oldest first. <paramref name="SpanMs"/> is the time from the first row to the last moment any row records.
/// <paramref name="Status"/> is <c>failed</c> (a command failed or an event or delivery is dead), <c>pending</c> (an event or delivery is still
/// waiting) or <c>complete</c>.
/// </summary>
public sealed record AuditTraceDto(string CorrelationId, DateTimeOffset StartedAt, long SpanMs, string Status, IReadOnlyList<AuditTraceVoiceDto> Voices);

/// <summary>
/// One voice of the trace. <paramref name="AtMs"/> and <paramref name="EndMs"/> are milliseconds after the first row. For a command,
/// <paramref name="Label"/> is the audit action, <paramref name="Detail"/> the HTTP method and route, <paramref name="Status"/> the outcome and
/// <paramref name="DurationMs"/> the pipeline time (failure rows only). For an event, the event type, the outbox status and <paramref name="EndMs"/>
/// when it was relayed. For a delivery, the event type, the subscription name, the delivery status, attempts, the last HTTP status code and
/// <paramref name="EndMs"/> when it succeeded or was dead-lettered.
/// </summary>
public sealed record AuditTraceVoiceDto(
    string Kind,
    Guid Id,
    DateTimeOffset At,
    long AtMs,
    long? EndMs,
    string Label,
    string? Detail,
    string Status,
    int? DurationMs,
    int? Attempts,
    int? StatusCode,
    string? ErrorCode,
    string? EntityType,
    string? EntityId);
