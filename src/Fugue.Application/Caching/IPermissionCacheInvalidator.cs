using Fugue.Domain.Ids;

namespace Fugue.Application.Caching;

/// <summary>
/// Drops cached permission reads (today the <c>/me</c> answer) after a write that changes what a user may do. Write handlers call it
/// only after their write succeeded, and the call takes no cancellation token: a committed write must not leave a stale entry. Enforcement never reads this cache: the JWT check re-reads access on every request.
/// </summary>
public interface IPermissionCacheInvalidator
{
    /// <summary>One user's roles, groups, overrides or profile changed (tag <c>perm:user:{id}</c>).</summary>
    ValueTask UserChangedAsync(UserId userId);

    /// <summary>Something that changes everyone's permissions changed, such as a module switch (tag <c>perm:all</c>).</summary>
    ValueTask AllChangedAsync();
}
