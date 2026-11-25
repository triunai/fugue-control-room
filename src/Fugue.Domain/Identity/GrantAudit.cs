using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>Append-only record of a role, group-membership or override grant or revoke, written in the grant's transaction.</summary>
public sealed class GrantAudit
{
    public GrantAuditId Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Who did it: a user id or <c>system</c>.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>role, group, group_role, permission_override.</summary>
    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public Guid? TargetUserId { get; set; }

    /// <summary>granted, revoked, expired.</summary>
    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public string? CorrelationId { get; set; }

    public string? SagaId { get; set; }
}
