using Fugue.Domain.Ids;

namespace Fugue.Domain.Audit;

/// <summary>
/// One append-only record of who did what to which row, with the field-level change and the request it happened in.
/// Rows use <see cref="OccurredAt"/> and an actor instead of <c>IAuditable</c> (they are never updated); a database trigger
/// rejects UPDATE, DELETE and TRUNCATE. Sensitive values are masked BEFORE they are stored (ADR-010), so nothing here can be un-masked.
/// </summary>
[NoAudit]
public sealed class AuditLog
{
    public AuditLogId Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The authenticated user, or null for <c>system</c> work (seeders, background).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>user, system, service (reserved for machine clients; none exist yet).</summary>
    public string ActorType { get; set; } = AuditActorTypes.System;

    /// <summary>The actor's display name at the time (a snapshot; renaming a user does not rewrite history).</summary>
    public string? ActorDisplay { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>The <c>[HasPermission]</c> code(s) that authorized the request, comma separated.</summary>
    public string? PermissionUsed { get; set; }

    /// <summary><c>&lt;module&gt;.&lt;verb&gt;</c>, for example <c>customers.update</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    /// <summary>A string so composite keys (<c>a:b</c>) fit.</summary>
    public string? EntityId { get; set; }

    /// <summary>success or failure.</summary>
    public string Outcome { get; set; } = AuditOutcomes.Success;

    public string? ErrorCode { get; set; }

    /// <summary>jsonb: <c>{ "field": { "old": ..., "new": ... } }</c>; added rows carry only <c>new</c>, deleted rows only <c>old</c>.</summary>
    public string? Changes { get; set; }

    public string? CorrelationId { get; set; }

    public string? SagaId { get; set; }

    public string? TraceId { get; set; }

    public string? HttpMethod { get; set; }

    public string? RouteTemplate { get; set; }

    public string? ClientIp { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>jsonb extras (the command name, a seed count, ...).</summary>
    public string? Metadata { get; set; }
}

public static class AuditActorTypes
{
    public const string User = "user";
    public const string System = "system";
    public const string Service = "service";
}

public static class AuditOutcomes
{
    public const string Success = "success";
    public const string Failure = "failure";
}
