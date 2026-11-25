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

    /// <summary>
    /// Creates the override (no <paramref name="expectedVersion"/>) or replaces it (the version from <c>If-Match</c>, mandatory when it
    /// exists). Fails <c>user.not_found</c>, <c>permission.not_found</c>, <c>override.if_match_required</c> or <c>override.version_mismatch</c>
    /// (also when a version was sent for a row that is gone, or a concurrent create won).
    /// </summary>
    Task<Result<OverrideDto>> SetAsync(
        UserId userId, string permission, bool isAllowed, string? reason, DateTimeOffset? expiresAt, long? expectedVersion, CancellationToken ct);

    /// <summary>Deletes the override at <paramref name="expectedVersion"/>. Fails <c>override.not_found</c>, <c>override.if_match_required</c> or <c>override.version_mismatch</c>.</summary>
    Task<Result> RemoveAsync(UserId userId, string permission, long? expectedVersion, CancellationToken ct);
}
