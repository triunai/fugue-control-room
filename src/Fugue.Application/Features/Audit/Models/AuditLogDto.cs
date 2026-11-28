using System.Text.Json;

namespace Fugue.Application.Features.Audit.Models;

/// <summary>
/// One audit row as the read API returns it. Values were masked when the row was written (ADR-010), so nothing here can be un-masked.
/// <paramref name="ErrorType"/>, <paramref name="ErrorMessage"/> and <paramref name="DurationMs"/> are set on failure rows only (the message is an audit-safe summary: validation field names only, or the exception type name; never handler or exception text).
/// <paramref name="Changes"/> is <c>{ "field": { "old": ..., "new": ... } }</c>; <paramref name="Metadata"/> holds extras such as the command name.
/// </summary>
public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string ActorType,
    string? ActorDisplay,
    Guid? TenantId,
    string? PermissionUsed,
    string Action,
    string? EntityType,
    string? EntityId,
    string Outcome,
    string? ErrorCode,
    string? ErrorType,
    string? ErrorMessage,
    int? DurationMs,
    JsonElement? Changes,
    string? CorrelationId,
    string? SagaId,
    string? TraceId,
    string? HttpMethod,
    string? RouteTemplate,
    string? ClientIp,
    string? UserAgent,
    JsonElement? Metadata);

/// <summary>The filters of the audit query. Every filter is an exact match; <c>From</c> is inclusive and <c>To</c> exclusive.</summary>
public sealed record AuditLogFilter(
    string? EntityType, string? EntityId, Guid? ActorUserId, string? Action, string? CorrelationId, DateTimeOffset? From, DateTimeOffset? To);
