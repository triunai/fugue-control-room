using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Users.Commands;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Users: the admin console directory, user creation and user edit. Never returns a password hash or any credential field.</summary>
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

    /// <summary>Get one user (same shape as a list row), with its <c>ETag</c>. An unknown or deleted user is 404.</summary>
    [HttpGet("{id}")]
    [HasPermission(Permissions.UsersView)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "user.not_found")]
    public Task<ActionResult<UserDto>> Get(UserId id, CancellationToken ct) =>
        Send(new GetUserByIdQuery(id), ct);

    /// <summary>
    /// Create a user with an initial password (12 to 128 characters, never returned) and at least one role. The email is unique among live
    /// users (409 `user.email_taken`); an unknown role is a 400 on `roles`; a role granting a permission the caller does not hold is 403
    /// `user.role_escalation`. The role grants are written to grant_audit with the user. Not `[Idempotent]` on purpose: the unique email
    /// already makes a retry safe (409), and the key store would keep a hash of a body that carries the password.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.UsersManage)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status403Forbidden, "user.role_escalation")]
    [ProblemResponse(StatusCodes.Status409Conflict, "user.email_taken")]
    public Task<ActionResult<UserDto>> Create(CreateUserRequest body, CancellationToken ct) =>
        SendCreated(new CreateUserCommand(body.Email, body.DisplayName, body.Password, body.Roles), nameof(Get), u => new { id = u.Id }, ct);

    /// <summary>
    /// Edit a user: display name, active flag and the full set of direct roles (the email and the password are not editable). Needs `If-Match`
    /// with the ETag from a read (428 without it, 412 `user.version_mismatch` when the user changed since). A role ADDED here follows the create
    /// rule (403 `user.role_escalation`); editing yourself cannot deactivate you or drop your last direct USERS_MANAGE role (409
    /// `user.self_lockout`). Every role added or removed is written to grant_audit in the same transaction.
    /// </summary>
    [HttpPut("{id}")]
    [HasPermission(Permissions.UsersManage)]
    [IfMatch]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status403Forbidden, "user.role_escalation")]
    [ProblemResponse(StatusCodes.Status404NotFound, "user.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "user.self_lockout")]
    public Task<ActionResult<UserDto>> Update(UserId id, UpdateUserRequest body, CancellationToken ct) =>
        Send(new UpdateUserCommand(id, body.DisplayName, body.IsActive, body.Roles, IfMatchVersion), ct);
}
