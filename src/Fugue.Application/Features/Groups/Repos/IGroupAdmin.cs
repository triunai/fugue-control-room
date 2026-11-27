using FluentResults;
using Fugue.Application.Features.Groups.Models;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Groups.Repos;

/// <summary>
/// Group administration. Membership changes write their <c>grant_audit</c> row in the SAME transaction as the change
/// (one <c>SaveChanges</c>, or the caller's unit of work), so an audit row can never be missing or orphaned.
/// </summary>
public interface IGroupAdmin
{
    Task<IReadOnlyList<GroupDto>> ListAsync(CancellationToken ct);

    /// <summary>Adds the user to the group. Already a member is a success and writes no audit row.</summary>
    Task<Result> AddMemberAsync(string groupCode, UserId userId, CancellationToken ct);

    /// <summary>Removes the member. Fails <c>group.member_not_found</c> when they are not in the group.</summary>
    Task<Result> RemoveMemberAsync(string groupCode, UserId userId, CancellationToken ct);
}
