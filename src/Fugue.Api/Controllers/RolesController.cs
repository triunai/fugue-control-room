using Fugue.Api.Auth;
using Fugue.Api.Modules;
using Fugue.Application.Features.Roles.Models;
using Fugue.Application.Features.Roles.Queries;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Roles: what each role grants, for a role by permission matrix. Read only.</summary>
[Module(ModuleCodes.Identity)]
public sealed class RolesController : ApiControllerBase
{
    /// <summary>List every role with the permission codes it grants and the module that owns each, ordered by role code.</summary>
    [HttpGet]
    [HasPermission(Permissions.UsersView)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct) =>
        Send(new ListRolesQuery(), ct);
}
