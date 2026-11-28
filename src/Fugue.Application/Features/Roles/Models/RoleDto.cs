namespace Fugue.Application.Features.Roles.Models;

/// <summary>A role with every permission it grants, for a role by permission matrix. Permissions are ordered by module, then code.</summary>
public sealed record RoleDto(string Code, string Name, bool IsActive, IReadOnlyList<RolePermissionDto> Permissions);

/// <summary>A granted permission code and the module that owns its resource.</summary>
public sealed record RolePermissionDto(string Code, string Module);
