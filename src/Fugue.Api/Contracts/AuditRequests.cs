using Fugue.Application.Features.Audit.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Contracts;

/// <summary>Query parameters of the audit list, bound as one object. Every filter is optional and they combine.</summary>
public sealed record AuditListFilter
{
    [FromQuery(Name = "entityType")]
    public string? EntityType { get; init; }

    [FromQuery(Name = "entityId")]
    public string? EntityId { get; init; }

    [FromQuery(Name = "actorUserId")]
    public Guid? ActorUserId { get; init; }

    [FromQuery(Name = "action")]
    public string? Action { get; init; }

    [FromQuery(Name = "correlationId")]
    public string? CorrelationId { get; init; }

    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; init; }

    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    public ListAuditLogQuery ToQuery() => new(EntityType, EntityId, ActorUserId, Action, CorrelationId, From, To, Limit, Cursor);
}
