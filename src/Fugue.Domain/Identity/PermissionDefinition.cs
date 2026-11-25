using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>One resource x action pair.</summary>
public sealed class PermissionDefinition : IAuditable
{
    public PermissionId Id { get; set; }

    public ResourceId ResourceId { get; set; }

    public ActionId ActionId { get; set; }

    /// <summary><c>RESOURCE_ACTION</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
