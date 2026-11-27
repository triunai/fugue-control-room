using System.Text.Json.Serialization;
using Fugue.Domain.Audit;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// One <c>audit_log</c> row as the search index holds it. Only the fields the index needs, copied as the row stores them (already masked);
/// <c>changes</c> and <c>metadata</c> are deliberately not indexed. <c>_formatted</c> is filled by the engine on a highlighted search.
/// </summary>
internal sealed class AuditDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("occurredAt")]
    public string OccurredAt { get; set; } = string.Empty;

    /// <summary>Epoch milliseconds, the filterable and sortable form of <see cref="OccurredAt"/>.</summary>
    [JsonPropertyName("occurredAtMs")]
    public long OccurredAtMs { get; set; }

    [JsonPropertyName("actorUserId")]
    public string? ActorUserId { get; set; }

    [JsonPropertyName("actorDisplay")]
    public string? ActorDisplay { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("entityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("routeTemplate")]
    public string? RouteTemplate { get; set; }

    [JsonPropertyName("_formatted")]
    public AuditDocument? Formatted { get; set; }

    public static AuditDocument From(AuditLog row) => new()
    {
        Id = row.Id.Value.ToString(),
        OccurredAt = row.OccurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        OccurredAtMs = row.OccurredAt.ToUnixTimeMilliseconds(),
        ActorUserId = row.ActorUserId?.ToString(),
        ActorDisplay = row.ActorDisplay,
        Action = row.Action,
        EntityType = row.EntityType,
        EntityId = row.EntityId,
        Outcome = row.Outcome,
        ErrorCode = row.ErrorCode,
        ErrorMessage = row.ErrorMessage,
        RouteTemplate = row.RouteTemplate,
    };
}

/// <summary>The indexer's checkpoint, kept in the engine (index <c>audit_checkpoint</c>) so no migration is needed.</summary>
internal sealed class AuditCheckpoint
{
    public const string DocumentId = "audit";

    [JsonPropertyName("id")]
    public string Id { get; set; } = DocumentId;

    [JsonPropertyName("occurredAt")]
    public string OccurredAt { get; set; } = string.Empty;

    [JsonPropertyName("auditId")]
    public string AuditId { get; set; } = string.Empty;
}
