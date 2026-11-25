namespace Fugue.Domain.Abstractions;

/// <summary>An entity that is retired by flagging, never removed, because other rows or history reference it.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }

    string? DeletedBy { get; set; }
}
