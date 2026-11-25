using Fugue.Api.Auth;
using Fugue.Api.Modules;
using Fugue.Application.Features.Groups;
using Fugue.Application.Features.Groups.Commands;
using Fugue.Application.Features.Groups.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Groups: members inherit the group's roles. Minimal administration; every change writes a grant_audit row.</summary>
[Module(ModuleCodes.Identity)]
public sealed class GroupsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.GroupsView)]
    public Task<ActionResult<IReadOnlyList<GroupDto>>> List(CancellationToken ct) =>
        Send(new ListGroupsQuery(), ct);

    [HttpPut("{code}/members/{userId}")]
    [HasPermission(Permissions.GroupsManage)]
    public Task<IActionResult> AddMember(string code, UserId userId, CancellationToken ct) =>
        SendNoContent(new AddGroupMemberCommand(code, userId), ct);

    [HttpDelete("{code}/members/{userId}")]
    [HasPermission(Permissions.GroupsManage)]
    public Task<IActionResult> RemoveMember(string code, UserId userId, CancellationToken ct) =>
        SendNoContent(new RemoveGroupMemberCommand(code, userId), ct);
}
