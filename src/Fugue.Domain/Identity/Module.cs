using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>A feature area of the API; the unit that can be switched off.</summary>
public sealed class Module : IAuditable, ISoftDeletable
{
    public ModuleId Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The runtime switch; configuration <c>Modules:CODE:Enabled</c> is the hard switch (ADR-009).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
