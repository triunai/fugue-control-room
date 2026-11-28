using Fugue.Application.Features.Me.Models;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Me.Repos;

/// <summary>
/// The cached <c>/me</c> answer, keyed by the authenticated user id only, so one user can never read another's entry. Concurrent
/// misses for one user run <c>load</c> once. A null from <c>load</c> (unknown or inactive user) is not cached.
/// </summary>
public interface IMeCache
{
    ValueTask<MeDto?> GetOrCreateAsync(UserId userId, Func<CancellationToken, ValueTask<MeDto?>> load, CancellationToken ct);
}
