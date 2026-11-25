using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>A person who can sign in.</summary>
public sealed class User : IAuditable, ISoftDeletable
{
    public UserId Id { get; set; }

    public TenantId? TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Canonical (<see cref="EmailKey"/>); unique among non-deleted users.</summary>
    public string Email { get; set; } = string.Empty;

    public string? ContactNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public bool LocalLoginEnabled { get; set; } = true;

    /// <summary>The encoded <c>PasswordHasher</c> output (salt and parameters inside).</summary>
    [AuditMask]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset PasswordLastChangedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
