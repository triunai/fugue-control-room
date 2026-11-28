using Fugue.Application.Features.Roles.Models;

namespace Fugue.Application.Features.Roles.Repos;

/// <summary>Read-only view of the roles and what they grant (EF, never Dapper).</summary>
public interface IRoleCatalog
{
    /// <summary>Every role ordered by code, each with its permissions.</summary>
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct);
}
