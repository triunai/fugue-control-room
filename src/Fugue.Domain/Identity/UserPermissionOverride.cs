using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>A per-user allow or deny of one permission.</summary>
public sealed class UserPermissionOverride : IAuditable
{
    public UserPermissionOverrideId Id { get; set; }

    public UserId UserId { get; set; }

    public PermissionId PermissionId { get; set; }

    /// <summary>True adds the permission, false removes it. Deny wins over allow wins over role.</summary>
    public bool IsAllowed { get; set; }

    public string? Reason { get; set; }

    /// <summary>Null means no expiry. <c>ExpiresAt &lt;= now</c> is expired and ignored.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
