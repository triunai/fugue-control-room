using Fugue.Api.Auth;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Groups.Commands;
using Fugue.Application.Features.Groups.Models;
using Fugue.Application.Features.Groups.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Groups: members inherit the group's roles. Minimal administration; every change writes a grant_audit row.</summary>
[Module(ModuleCodes.Identity)]
public sealed class GroupsController : ApiControllerBase
{
    /// <summary>List groups with their roles and member counts.</summary>
    [HttpGet]
    [HasPermission(Permissions.GroupsView)]
    [ProducesResponseType(typeof(IReadOnlyList<GroupDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<GroupDto>>> List(CancellationToken ct) =>
        Send(new ListGroupsQuery(), ct);

    /// <summary>Add a user to a group (idempotent). You cannot change your own membership.</summary>
    [HttpPut("{code}/members/{userId}")]
    [HasPermission(Permissions.GroupsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "group.not_found", "user.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "group.self_target")]
    public Task<IActionResult> AddMember(string code, UserId userId, CancellationToken ct) =>
        SendNoContent(new AddGroupMemberCommand(code, userId), ct);

    /// <summary>Remove a user from a group. You cannot change your own membership.</summary>
    [HttpDelete("{code}/members/{userId}")]
    [HasPermission(Permissions.GroupsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "group.not_found", "group.member_not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "group.self_target")]
    public Task<IActionResult> RemoveMember(string code, UserId userId, CancellationToken ct) =>
        SendNoContent(new RemoveGroupMemberCommand(code, userId), ct);
}
