using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>Append-only authentication and authorization event. Never holds a password or token; email is hashed.</summary>
public sealed class AccessAudit
{
    public AccessAuditId Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>login, refresh, refresh_reuse, logout, forbidden, module_disabled.</summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>success, failure, denied.</summary>
    public string Outcome { get; set; } = string.Empty;

    public string? ReasonCode { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>SHA-256 hex of the canonical email, so repeated attempts correlate without storing the address.</summary>
    public string? EmailHash { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }

    public string? CorrelationId { get; set; }
}
