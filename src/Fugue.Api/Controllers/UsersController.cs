using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Application.Features.Users;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Users: a read-only directory for the admin console. Never returns a password hash or any credential field.</summary>
[Module(ModuleCodes.Identity)]
public sealed class UsersController : ApiControllerBase
{
    /// <summary>
    /// List users, keyset paged (oldest account first). Each row: id, email, display name, active, the roles assigned directly, the group codes
    /// and when the account was created. `q` matches the email or the display name.
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.UsersView)]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<UserDto>>> List([FromQuery] UserListFilter filter, CancellationToken ct) =>
        SendPaged(filter.ToQuery(), ct);
}
