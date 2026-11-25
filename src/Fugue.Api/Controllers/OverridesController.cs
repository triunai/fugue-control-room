using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
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
    [HttpGet("{userId}")]
    [HasPermission(Permissions.OverridesView)]
    public Task<ActionResult<IReadOnlyList<OverrideDto>>> List(UserId userId, CancellationToken ct) =>
        Send(new ListOverridesQuery(userId), ct);

    [HttpPut("{userId}/{permission}")]
    [HasPermission(Permissions.OverridesManage)]
    public Task<IActionResult> Set(UserId userId, string permission, SetOverrideRequest body, CancellationToken ct) =>
        SendNoContent(new SetOverrideCommand(userId, permission, body.IsAllowed, body.Reason, body.ExpiresAt, IfMatchVersion), ct);

    [HttpDelete("{userId}/{permission}")]
    [HasPermission(Permissions.OverridesManage)]
    public Task<IActionResult> Remove(UserId userId, string permission, CancellationToken ct) =>
        SendNoContent(new RemoveOverrideCommand(userId, permission, IfMatchVersion), ct);
}
