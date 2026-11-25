using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>Assigns a role to a user.</summary>
public sealed class UserRole : IAuditable
{
    public UserRoleId Id { get; set; }

    public UserId UserId { get; set; }

    public RoleId RoleId { get; set; }

    public UserId? AssignedByUserId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
