using FluentResults;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Overrides;

/// <summary>
/// Permission overrides (deny > allow > role). One row per (user, permission): setting again replaces it. Every change stages its
/// <c>grant_audit</c> row and commits it in the same <c>SaveChanges</c> as the change itself.
/// </summary>
public interface IOverrideAdmin
{
    Task<IReadOnlyList<OverrideDto>> ListAsync(UserId userId, CancellationToken ct);

    /// <summary>Creates or replaces the override. Fails <c>user.not_found</c> or <c>permission.not_found</c>.</summary>
    Task<Result> SetAsync(UserId userId, string permission, bool isAllowed, string? reason, DateTimeOffset? expiresAt, CancellationToken ct);

    /// <summary>Deletes the override. Fails <c>override.not_found</c>.</summary>
    Task<Result> RemoveAsync(UserId userId, string permission, CancellationToken ct);
}
