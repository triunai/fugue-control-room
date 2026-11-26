using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Overrides;
using Fugue.Application.Features.Overrides.Commands;
using Fugue.Application.Features.Overrides.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Per-user permission overrides: an explicit allow or deny, optionally expiring. Deny wins over allow wins over role.</summary>
[Module(ModuleCodes.Identity)]
public sealed class OverridesController : ApiControllerBase
{
    /// <summary>List a user's permission overrides.</summary>
    [HttpGet("{userId}")]
    [HasPermission(Permissions.OverridesView)]
    [ProducesResponseType(typeof(IReadOnlyList<OverrideDto>), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "user.not_found")]
    public Task<ActionResult<IReadOnlyList<OverrideDto>>> List(UserId userId, CancellationToken ct) =>
        Send(new ListOverridesQuery(userId), ct);

    /// <summary>Create or replace an allow or deny override. Deny beats allow beats role. You cannot change your own.</summary>
    [HttpPut("{userId}/{permission}")]
    [HasPermission(Permissions.OverridesManage)]
    [IfMatch(Required = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "user.not_found", "permission.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "override.self_target")]
    public Task<IActionResult> Set(UserId userId, string permission, SetOverrideRequest body, CancellationToken ct) =>
        SendNoContent(new SetOverrideCommand(userId, permission, body.IsAllowed, body.Reason, body.ExpiresAt, IfMatchVersion), ct);

    /// <summary>Remove an override.</summary>
    [HttpDelete("{userId}/{permission}")]
    [HasPermission(Permissions.OverridesManage)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "override.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "override.self_target")]
    public Task<IActionResult> Remove(UserId userId, string permission, CancellationToken ct) =>
        SendNoContent(new RemoveOverrideCommand(userId, permission, IfMatchVersion), ct);
}
