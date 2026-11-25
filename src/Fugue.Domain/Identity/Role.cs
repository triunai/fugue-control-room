using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>A global bundle of permissions.</summary>
public sealed class Role : IAuditable
{
    public RoleId Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
