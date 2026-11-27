namespace Fugue.Application.Features.Roles;

/// <summary>Read-only view of the roles and what they grant (EF, never Dapper).</summary>
public interface IRoleCatalog
{
    /// <summary>Every role ordered by code, each with its permissions.</summary>
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct);
}
